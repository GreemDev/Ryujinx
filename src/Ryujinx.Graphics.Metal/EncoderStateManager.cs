using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Metal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct EncoderStateManager : IDisposable
    {
        private readonly Pipeline _pipeline;

        private readonly RenderPipelineCache _renderPipelineCache;
        private readonly DepthStencilCache _depthStencilCache;

        private EncoderState _currentState = new();
        private readonly Stack<EncoderState> _backStates = [];

        public readonly MTLBuffer IndexBuffer => _currentState.IndexBuffer;
        public readonly MTLIndexType IndexType => _currentState.IndexType;
        public readonly ulong IndexBufferOffset => _currentState.IndexBufferOffset;
        public readonly PrimitiveTopology Topology => _currentState.Topology;

        // RGBA32F is the biggest format
        private const int ZeroBufferSize = 4 * 4;
        private readonly MTLBuffer _zeroBuffer;

        public unsafe EncoderStateManager(MTLDevice device, Pipeline pipeline)
        {
            _pipeline = pipeline;
            _renderPipelineCache = new(device);
            _depthStencilCache = new(device);

            // Zero buffer
            byte[] zeros = new byte[ZeroBufferSize];
            fixed (byte* ptr = zeros)
            {
                _zeroBuffer = device.NewBuffer((IntPtr)ptr, ZeroBufferSize, MTLResourceOptions.ResourceStorageModeShared);
            }
        }

        public void Dispose()
        {
            // State
            _currentState.FrontFaceStencil.Dispose();
            _currentState.BackFaceStencil.Dispose();

            _renderPipelineCache.Dispose();
            _depthStencilCache.Dispose();
        }

        public void SaveState()
        {
            _backStates.Push(_currentState);
            _currentState = _currentState.Clone();
        }

        public void SaveAndResetState()
        {
            _backStates.Push(_currentState);
            _currentState = new();
        }

        public void RestoreState()
        {
            if (_backStates.Count > 0)
            {
                _currentState = _backStates.Pop();

                // Set all the inline state, since it might have changed
                var renderCommandEncoder = _pipeline.GetOrCreateRenderEncoder();
                SetDepthClamp(renderCommandEncoder);
                SetScissors(renderCommandEncoder);
                SetViewports(renderCommandEncoder);
                SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
                SetBuffers(renderCommandEncoder, _currentState.UniformBuffers, true);
                SetBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
                SetCullMode(renderCommandEncoder);
                SetFrontFace(renderCommandEncoder);
                SetStencilRefValue(renderCommandEncoder);

                // Mark the other state as dirty
                _currentState.Dirty.MarkAll();
            }
            else
            {
                Logger.Error?.Print(LogClass.Gpu, "No state to restore");
            }
        }

        public void SetClearLoadAction(bool clear)
        {
            _currentState.ClearLoadAction = clear;
        }

        public MTLRenderCommandEncoder CreateRenderCommandEncoder()
        {
            // Initialise Pass & State
            var renderPassDescriptor = new MTLRenderPassDescriptor();

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                if (_currentState.RenderTargets[i] != null)
                {
                    var passAttachment = renderPassDescriptor.ColorAttachments.Object((ulong)i);
                    passAttachment.Texture = _currentState.RenderTargets[i].MTLTexture;
                    passAttachment.LoadAction = _currentState.ClearLoadAction ? MTLLoadAction.Clear : MTLLoadAction.Load;
                    passAttachment.StoreAction = MTLStoreAction.Store;
                }
            }

            var depthAttachment = renderPassDescriptor.DepthAttachment;
            var stencilAttachment = renderPassDescriptor.StencilAttachment;

            if (_currentState.DepthStencil != null)
            {
                switch (_currentState.DepthStencil.MTLTexture.PixelFormat)
                {
                    // Depth Only Attachment
                    case MTLPixelFormat.Depth16Unorm:
                    case MTLPixelFormat.Depth32Float:
                        depthAttachment.Texture = _currentState.DepthStencil.MTLTexture;
                        depthAttachment.LoadAction = MTLLoadAction.Load;
                        depthAttachment.StoreAction = MTLStoreAction.Store;
                        break;

                    // Stencil Only Attachment
                    case MTLPixelFormat.Stencil8:
                        stencilAttachment.Texture = _currentState.DepthStencil.MTLTexture;
                        stencilAttachment.LoadAction = MTLLoadAction.Load;
                        stencilAttachment.StoreAction = MTLStoreAction.Store;
                        break;

                    // Combined Attachment
                    case MTLPixelFormat.Depth24UnormStencil8:
                    case MTLPixelFormat.Depth32FloatStencil8:
                        depthAttachment.Texture = _currentState.DepthStencil.MTLTexture;
                        depthAttachment.LoadAction = MTLLoadAction.Load;
                        depthAttachment.StoreAction = MTLStoreAction.Store;

                        stencilAttachment.Texture = _currentState.DepthStencil.MTLTexture;
                        stencilAttachment.LoadAction = MTLLoadAction.Load;
                        stencilAttachment.StoreAction = MTLStoreAction.Store;
                        break;
                    default:
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"Unsupported Depth/Stencil Format: {_currentState.DepthStencil.MTLTexture.PixelFormat}!");
                        break;
                }
            }

            // Initialise Encoder
            var renderCommandEncoder = _pipeline.CommandBuffer.RenderCommandEncoder(renderPassDescriptor);

            // Mark all state as dirty to ensure it is set on the encoder
            _currentState.Dirty.MarkAll();

            // Rebind all the state
            SetDepthClamp(renderCommandEncoder);
            SetCullMode(renderCommandEncoder);
            SetFrontFace(renderCommandEncoder);
            SetStencilRefValue(renderCommandEncoder);
            SetViewports(renderCommandEncoder);
            SetScissors(renderCommandEncoder);
            SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
            SetBuffers(renderCommandEncoder, _currentState.UniformBuffers, true);
            SetBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
            SetTextureAndSampler(renderCommandEncoder, ShaderStage.Vertex, _currentState.VertexTextures, _currentState.VertexSamplers);
            SetTextureAndSampler(renderCommandEncoder, ShaderStage.Fragment, _currentState.FragmentTextures, _currentState.FragmentSamplers);

            // Cleanup
            renderPassDescriptor.Dispose();

            return renderCommandEncoder;
        }

        public void RebindState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.Dirty.Pipeline)
            {
                SetPipelineState(renderCommandEncoder);
            }

            if (_currentState.Dirty.DepthStencil)
            {
                SetDepthStencilState(renderCommandEncoder);
            }

            // Clear the dirty flags
            _currentState.Dirty.Clear();
        }

        private readonly void SetPipelineState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor();

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                if (_currentState.RenderTargets[i] != null)
                {
                    var pipelineAttachment = renderPipelineDescriptor.ColorAttachments.Object((ulong)i);
                    pipelineAttachment.PixelFormat = _currentState.RenderTargets[i].MTLTexture.PixelFormat;
                    pipelineAttachment.SourceAlphaBlendFactor = MTLBlendFactor.SourceAlpha;
                    pipelineAttachment.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
                    pipelineAttachment.SourceRGBBlendFactor = MTLBlendFactor.SourceAlpha;
                    pipelineAttachment.DestinationRGBBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
                    pipelineAttachment.WriteMask = _currentState.RenderTargetMasks[i];

                    if (_currentState.BlendDescriptors[i] != null)
                    {
                        var blendDescriptor = _currentState.BlendDescriptors[i].Value;
                        pipelineAttachment.SetBlendingEnabled(blendDescriptor.Enable);
                        pipelineAttachment.AlphaBlendOperation = blendDescriptor.AlphaOp.Convert();
                        pipelineAttachment.RgbBlendOperation = blendDescriptor.ColorOp.Convert();
                        pipelineAttachment.SourceAlphaBlendFactor = blendDescriptor.AlphaSrcFactor.Convert();
                        pipelineAttachment.DestinationAlphaBlendFactor = blendDescriptor.AlphaDstFactor.Convert();
                        pipelineAttachment.SourceRGBBlendFactor = blendDescriptor.ColorSrcFactor.Convert();
                        pipelineAttachment.DestinationRGBBlendFactor = blendDescriptor.ColorDstFactor.Convert();
                    }
                }
            }

            if (_currentState.DepthStencil != null)
            {
                switch (_currentState.DepthStencil.MTLTexture.PixelFormat)
                {
                    // Depth Only Attachment
                    case MTLPixelFormat.Depth16Unorm:
                    case MTLPixelFormat.Depth32Float:
                        renderPipelineDescriptor.DepthAttachmentPixelFormat = _currentState.DepthStencil.MTLTexture.PixelFormat;
                        break;

                    // Stencil Only Attachment
                    case MTLPixelFormat.Stencil8:
                        renderPipelineDescriptor.StencilAttachmentPixelFormat = _currentState.DepthStencil.MTLTexture.PixelFormat;
                        break;

                    // Combined Attachment
                    case MTLPixelFormat.Depth24UnormStencil8:
                    case MTLPixelFormat.Depth32FloatStencil8:
                        renderPipelineDescriptor.DepthAttachmentPixelFormat = _currentState.DepthStencil.MTLTexture.PixelFormat;
                        renderPipelineDescriptor.StencilAttachmentPixelFormat = _currentState.DepthStencil.MTLTexture.PixelFormat;
                        break;
                    default:
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"Unsupported Depth/Stencil Format: {_currentState.DepthStencil.MTLTexture.PixelFormat}!");
                        break;
                }
            }

            var vertexDescriptor = BuildVertexDescriptor(_currentState.VertexBuffers, _currentState.VertexAttribs);
            renderPipelineDescriptor.VertexDescriptor = vertexDescriptor;

            try
            {
                if (_currentState.VertexFunction != null)
                {
                    renderPipelineDescriptor.VertexFunction = _currentState.VertexFunction.Value;
                }
                else
                {
                    return;
                }

                if (_currentState.FragmentFunction != null)
                {
                    renderPipelineDescriptor.FragmentFunction = _currentState.FragmentFunction.Value;
                }

                var pipelineState = _renderPipelineCache.GetOrCreate(renderPipelineDescriptor);

                renderCommandEncoder.SetRenderPipelineState(pipelineState);

                renderCommandEncoder.SetBlendColor(
                    _currentState.BlendColor.Red,
                    _currentState.BlendColor.Green,
                    _currentState.BlendColor.Blue,
                    _currentState.BlendColor.Alpha);
            }
            finally
            {
                // Cleanup
                renderPipelineDescriptor.Dispose();
                vertexDescriptor.Dispose();
            }
        }

        public void UpdateIndexBuffer(BufferRange buffer, IndexType type)
        {
            if (buffer.Handle != BufferHandle.Null)
            {
                _currentState.IndexType = type.Convert();
                _currentState.IndexBufferOffset = (ulong)buffer.Offset;
                var handle = buffer.Handle;
                _currentState.IndexBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref handle));
            }
        }

        public void UpdatePrimitiveTopology(PrimitiveTopology topology)
        {
            _currentState.Topology = topology;
        }

        public void UpdateProgram(IProgram program)
        {
            Program prg = (Program)program;

            if (prg.VertexFunction == IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, "Invalid Vertex Function!");
                return;
            }

            _currentState.VertexFunction = prg.VertexFunction;
            _currentState.FragmentFunction = prg.FragmentFunction;

            // Mark dirty
            _currentState.Dirty.Pipeline = true;
        }

        public void UpdateRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _currentState.RenderTargets = new Texture[Constants.MaxColorAttachments];

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] is not Texture tex)
                {
                    continue;
                }

                _currentState.RenderTargets[i] = tex;
            }

            if (depthStencil is Texture depthTexture)
            {
                _currentState.DepthStencil = depthTexture;
            }
            else if (depthStencil == null)
            {
                _currentState.DepthStencil = null;
            }

            // Requires recreating pipeline
            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        public void UpdateRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            _currentState.RenderTargetMasks = new MTLColorWriteMask[Constants.MaxColorAttachments];

            for (int i = 0; i < componentMask.Length; i++)
            {
                bool red = (componentMask[i] & (0x1 << 0)) != 0;
                bool green = (componentMask[i] & (0x1 << 1)) != 0;
                bool blue = (componentMask[i] & (0x1 << 2)) != 0;
                bool alpha = (componentMask[i] & (0x1 << 3)) != 0;

                var mask = MTLColorWriteMask.None;

                mask |= red ? MTLColorWriteMask.Red : 0;
                mask |= green ? MTLColorWriteMask.Green : 0;
                mask |= blue ? MTLColorWriteMask.Blue : 0;
                mask |= alpha ? MTLColorWriteMask.Alpha : 0;

                _currentState.RenderTargetMasks[i] = mask;
            }

            // Requires recreating pipeline
            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        public void UpdateVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            _currentState.VertexAttribs = vertexAttribs.ToArray();

            // Mark dirty
            _currentState.Dirty.Pipeline = true;
        }

        public void UpdateBlendDescriptors(int index, BlendDescriptor blend)
        {
            _currentState.BlendDescriptors[index] = blend;
            _currentState.BlendColor = blend.BlendConstant;
        }

        // Inlineable
        public void UpdateStencilState(StencilTestDescriptor stencilTest)
        {
            // Cleanup old state
            _currentState.FrontFaceStencil.Dispose();
            _currentState.BackFaceStencil.Dispose();

            _currentState.FrontFaceStencil = new MTLStencilDescriptor
            {
                StencilFailureOperation = stencilTest.FrontSFail.Convert(),
                DepthFailureOperation = stencilTest.FrontDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.FrontDpPass.Convert(),
                StencilCompareFunction = stencilTest.FrontFunc.Convert(),
                ReadMask = (uint)stencilTest.FrontFuncMask,
                WriteMask = (uint)stencilTest.FrontMask
            };

            _currentState.BackFaceStencil = new MTLStencilDescriptor
            {
                StencilFailureOperation = stencilTest.BackSFail.Convert(),
                DepthFailureOperation = stencilTest.BackDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.BackDpPass.Convert(),
                StencilCompareFunction = stencilTest.BackFunc.Convert(),
                ReadMask = (uint)stencilTest.BackFuncMask,
                WriteMask = (uint)stencilTest.BackMask
            };

            _currentState.StencilTestEnabled = stencilTest.TestEnable;

            var descriptor = new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _currentState.DepthCompareFunction,
                DepthWriteEnabled = _currentState.DepthWriteEnabled
            };

            if (_currentState.StencilTestEnabled)
            {
                descriptor.BackFaceStencil = _currentState.BackFaceStencil;
                descriptor.FrontFaceStencil = _currentState.FrontFaceStencil;
            }

            _currentState.DepthStencilState = _depthStencilCache.GetOrCreate(descriptor);

            UpdateStencilRefValue(stencilTest.FrontFuncRef, stencilTest.BackFuncRef);

            // Mark dirty
            _currentState.Dirty.DepthStencil = true;

            // Cleanup
            descriptor.Dispose();
        }

        // Inlineable
        public void UpdateDepthState(DepthTestDescriptor depthTest)
        {
            _currentState.DepthCompareFunction = depthTest.TestEnable ? depthTest.Func.Convert() : MTLCompareFunction.Always;
            _currentState.DepthWriteEnabled = depthTest.WriteEnable;

            var descriptor = new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _currentState.DepthCompareFunction,
                DepthWriteEnabled = _currentState.DepthWriteEnabled
            };

            if (_currentState.StencilTestEnabled)
            {
                descriptor.BackFaceStencil = _currentState.BackFaceStencil;
                descriptor.FrontFaceStencil = _currentState.FrontFaceStencil;
            }

            _currentState.DepthStencilState = _depthStencilCache.GetOrCreate(descriptor);

            // Mark dirty
            _currentState.Dirty.DepthStencil = true;

            // Cleanup
            descriptor.Dispose();
        }

        // Inlineable
        public void UpdateDepthClamp(bool clamp)
        {
            _currentState.DepthClipMode = clamp ? MTLDepthClipMode.Clamp : MTLDepthClipMode.Clip;

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetDepthClamp(renderCommandEncoder);
            }
        }

        // Inlineable
        public void UpdateScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            int maxScissors = Math.Min(regions.Length, _currentState.Viewports.Length);

            _currentState.Scissors = new MTLScissorRect[maxScissors];

            for (int i = 0; i < maxScissors; i++)
            {
                var region = regions[i];

                _currentState.Scissors[i] = new MTLScissorRect
                {
                    height = (ulong)region.Height,
                    width = (ulong)region.Width,
                    x = (ulong)region.X,
                    y = (ulong)region.Y
                };
            }

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetScissors(renderCommandEncoder);
            }
        }

        // Inlineable
        public void UpdateViewports(ReadOnlySpan<Viewport> viewports)
        {
            static float Clamp(float value)
            {
                return Math.Clamp(value, 0f, 1f);
            }

            _currentState.Viewports = new MTLViewport[viewports.Length];

            for (int i = 0; i < viewports.Length; i++)
            {
                var viewport = viewports[i];
                // Y coordinate is inverted
                _currentState.Viewports[i] = new MTLViewport
                {
                    originX = viewport.Region.X,
                    originY = viewport.Region.Y + viewport.Region.Height,
                    width = viewport.Region.Width,
                    height = -viewport.Region.Height,
                    znear = Clamp(viewport.DepthNear),
                    zfar = Clamp(viewport.DepthFar)
                };
            }

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetViewports(renderCommandEncoder);
            }
        }

        public void UpdateVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            _currentState.VertexBuffers = vertexBuffers.ToArray();

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
            }

            // Mark dirty
            _currentState.Dirty.Pipeline = true;
        }

        // Inlineable
        public void UpdateUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _currentState.UniformBuffers = [];

            foreach (BufferAssignment buffer in buffers)
            {
                if (buffer.Range.Size != 0)
                {
                    _currentState.UniformBuffers.Add(new BufferInfo
                    {
                        Handle = buffer.Range.Handle.ToIntPtr(),
                        Offset = buffer.Range.Offset,
                        Index = buffer.Binding
                    });
                }
            }

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetBuffers(renderCommandEncoder, _currentState.UniformBuffers, true);
            }
        }

        // Inlineable
        public void UpdateStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _currentState.StorageBuffers = [];

            foreach (BufferAssignment buffer in buffers)
            {
                if (buffer.Range.Size != 0)
                {
                    // TODO: DONT offset the binding by 15
                    _currentState.StorageBuffers.Add(new BufferInfo
                    {
                        Handle = buffer.Range.Handle.ToIntPtr(),
                        Offset = buffer.Range.Offset,
                        Index = buffer.Binding + 15
                    });
                }
            }

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
            }
        }

        // Inlineable
        public void UpdateCullMode(bool enable, Face face)
        {
            _currentState.CullMode = enable ? face.Convert() : MTLCullMode.None;

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetCullMode(renderCommandEncoder);
            }
        }

        // Inlineable
        public void UpdateFrontFace(FrontFace frontFace)
        {
            _currentState.Winding = frontFace.Convert();

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetFrontFace(renderCommandEncoder);
            }
        }

        private void UpdateStencilRefValue(int frontRef, int backRef)
        {
            _currentState.FrontRefValue = frontRef;
            _currentState.BackRefValue = backRef;

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetStencilRefValue(renderCommandEncoder);
            }
        }

        // Inlineable
        public readonly void UpdateTextureAndSampler(ShaderStage stage, ulong binding, MTLTexture texture, MTLSamplerState sampler)
        {
            switch (stage)
            {
                case ShaderStage.Fragment:
                    _currentState.FragmentTextures[binding] = texture;
                    _currentState.FragmentSamplers[binding] = sampler;
                    break;
                case ShaderStage.Vertex:
                    _currentState.VertexTextures[binding] = texture;
                    _currentState.VertexSamplers[binding] = sampler;
                    break;
            }

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                // TODO: Only update the new ones
                SetTextureAndSampler(renderCommandEncoder, ShaderStage.Vertex, _currentState.VertexTextures, _currentState.VertexSamplers);
                SetTextureAndSampler(renderCommandEncoder, ShaderStage.Fragment, _currentState.FragmentTextures, _currentState.FragmentSamplers);
            }
        }

        private readonly void SetDepthStencilState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.DepthStencilState != null)
            {
                renderCommandEncoder.SetDepthStencilState(_currentState.DepthStencilState.Value);
            }
        }

        private readonly void SetDepthClamp(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetDepthClipMode(_currentState.DepthClipMode);
        }

        private unsafe void SetScissors(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.Scissors.Length > 0)
            {
                fixed (MTLScissorRect* pMtlScissors = _currentState.Scissors)
                {
                    renderCommandEncoder.SetScissorRects((IntPtr)pMtlScissors, (ulong)_currentState.Scissors.Length);
                }
            }
        }

        private unsafe void SetViewports(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.Viewports.Length > 0)
            {
                fixed (MTLViewport* pMtlViewports = _currentState.Viewports)
                {
                    renderCommandEncoder.SetViewports((IntPtr)pMtlViewports, (ulong)_currentState.Viewports.Length);
                }
            }
        }

        private readonly MTLVertexDescriptor BuildVertexDescriptor(VertexBufferDescriptor[] bufferDescriptors, VertexAttribDescriptor[] attribDescriptors)
        {
            var vertexDescriptor = new MTLVertexDescriptor();
            uint indexMask = 0;

            for (int i = 0; i < attribDescriptors.Length; i++)
            {
                if (attribDescriptors[i].IsZero)
                {
                    var attrib = vertexDescriptor.Attributes.Object((ulong)i);
                    attrib.Format = attribDescriptors[i].Format.Convert();
                    indexMask |= 1u << bufferDescriptors.Length;
                    attrib.BufferIndex = (ulong)bufferDescriptors.Length;
                    attrib.Offset = 0;
                }
                else
                {
                    var attrib = vertexDescriptor.Attributes.Object((ulong)i);
                    attrib.Format = attribDescriptors[i].Format.Convert();
                    indexMask |= 1u << attribDescriptors[i].BufferIndex;
                    attrib.BufferIndex = (ulong)attribDescriptors[i].BufferIndex;
                    attrib.Offset = (ulong)attribDescriptors[i].Offset;
                }
            }

            for (int i = 0; i < bufferDescriptors.Length; i++)
            {
                var layout = vertexDescriptor.Layouts.Object((ulong)i);
                layout.Stride = (indexMask & (1u << i)) != 0 ? (ulong)bufferDescriptors[i].Stride : 0;
            }

            // Zero buffer
            if ((indexMask & (1u << bufferDescriptors.Length)) != 0)
            {
                var layout = vertexDescriptor.Layouts.Object((ulong)bufferDescriptors.Length);
                layout.Stride = ZeroBufferSize;
            }

            return vertexDescriptor;
        }

        private void SetVertexBuffers(MTLRenderCommandEncoder renderCommandEncoder, VertexBufferDescriptor[] bufferDescriptors)
        {
            var buffers = new List<BufferInfo>();

            for (int i = 0; i < bufferDescriptors.Length; i++)
            {
                if (bufferDescriptors[i].Buffer.Handle.ToIntPtr() != IntPtr.Zero)
                {
                    buffers.Add(new BufferInfo
                    {
                        Handle = bufferDescriptors[i].Buffer.Handle.ToIntPtr(),
                        Offset = bufferDescriptors[i].Buffer.Offset,
                        Index = i
                    });
                }
            }

            // Zero buffer
            buffers.Add(new BufferInfo
            {
                Handle = _zeroBuffer.NativePtr,
                Offset = 0,
                Index = bufferDescriptors.Length
            });

            SetBuffers(renderCommandEncoder, buffers);
        }

        private readonly void SetBuffers(MTLRenderCommandEncoder renderCommandEncoder, List<BufferInfo> buffers, bool fragment = false)
        {
            foreach (var buffer in buffers)
            {
                renderCommandEncoder.SetVertexBuffer(new MTLBuffer(buffer.Handle), (ulong)buffer.Offset, (ulong)buffer.Index);

                if (fragment)
                {
                    renderCommandEncoder.SetFragmentBuffer(new MTLBuffer(buffer.Handle), (ulong)buffer.Offset, (ulong)buffer.Index);
                }
            }
        }

        private readonly void SetCullMode(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetCullMode(_currentState.CullMode);
        }

        private readonly void SetFrontFace(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetFrontFacingWinding(_currentState.Winding);
        }

        private readonly void SetStencilRefValue(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetStencilReferenceValues((uint)_currentState.FrontRefValue, (uint)_currentState.BackRefValue);
        }

        private static void SetTextureAndSampler(MTLRenderCommandEncoder renderCommandEncoder, ShaderStage stage, MTLTexture[] textures, MTLSamplerState[] samplers)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                var texture = textures[i];
                if (texture != IntPtr.Zero)
                {
                    switch (stage)
                    {
                        case ShaderStage.Vertex:
                            renderCommandEncoder.SetVertexTexture(texture, (ulong)i);
                            break;
                        case ShaderStage.Fragment:
                            renderCommandEncoder.SetFragmentTexture(texture, (ulong)i);
                            break;
                    }
                }
            }

            for (int i = 0; i < samplers.Length; i++)
            {
                var sampler = samplers[i];
                if (sampler != IntPtr.Zero)
                {
                    switch (stage)
                    {
                        case ShaderStage.Vertex:
                            renderCommandEncoder.SetVertexSamplerState(sampler, (ulong)i);
                            break;
                        case ShaderStage.Fragment:
                            renderCommandEncoder.SetFragmentSamplerState(sampler, (ulong)i);
                            break;
                    }
                }
            }
        }
    }
}
