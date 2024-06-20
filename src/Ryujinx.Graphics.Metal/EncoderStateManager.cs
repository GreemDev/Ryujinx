using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Metal;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using BufferAssignment = Ryujinx.Graphics.GAL.BufferAssignment;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct EncoderStateManager : IDisposable
    {
        private readonly Pipeline _pipeline;
        private readonly BufferManager _bufferManager;

        private readonly RenderPipelineCache _renderPipelineCache;
        private readonly ComputePipelineCache _computePipelineCache;
        private readonly DepthStencilCache _depthStencilCache;

        private EncoderState _currentState = new();
        private readonly Stack<EncoderState> _backStates = [];

        public readonly Auto<DisposableBuffer> IndexBuffer => _currentState.IndexBuffer;
        public readonly MTLIndexType IndexType => _currentState.IndexType;
        public readonly ulong IndexBufferOffset => _currentState.IndexBufferOffset;
        public readonly PrimitiveTopology Topology => _currentState.Topology;
        public readonly Texture RenderTarget => _currentState.RenderTargets[0];
        public readonly Texture DepthStencil => _currentState.DepthStencil;

        // RGBA32F is the biggest format
        private const int ZeroBufferSize = 4 * 4;
        private readonly BufferHandle _zeroBuffer;

        public unsafe EncoderStateManager(MTLDevice device, BufferManager bufferManager, Pipeline pipeline)
        {
            _pipeline = pipeline;
            _bufferManager = bufferManager;

            _renderPipelineCache = new(device);
            _computePipelineCache = new(device);
            _depthStencilCache = new(device);

            // Zero buffer
            byte[] zeros = new byte[ZeroBufferSize];
            fixed (byte* ptr = zeros)
            {
                _zeroBuffer = _bufferManager.Create((IntPtr)ptr, ZeroBufferSize);
            }
        }

        public void Dispose()
        {
            // State
            _currentState.FrontFaceStencil.Dispose();
            _currentState.BackFaceStencil.Dispose();

            _renderPipelineCache.Dispose();
            _computePipelineCache.Dispose();
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
                SetDepthBias(renderCommandEncoder);
                SetScissors(renderCommandEncoder);
                SetViewports(renderCommandEncoder);
                SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
                SetRenderBuffers(renderCommandEncoder, _currentState.UniformBuffers, true);
                SetRenderBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
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
                    passAttachment.Texture = _currentState.RenderTargets[i].GetHandle();
                    passAttachment.LoadAction = _currentState.ClearLoadAction ? MTLLoadAction.Clear : MTLLoadAction.Load;
                    passAttachment.StoreAction = MTLStoreAction.Store;
                }
            }

            var depthAttachment = renderPassDescriptor.DepthAttachment;
            var stencilAttachment = renderPassDescriptor.StencilAttachment;

            if (_currentState.DepthStencil != null)
            {
                switch (_currentState.DepthStencil.GetHandle().PixelFormat)
                {
                    // Depth Only Attachment
                    case MTLPixelFormat.Depth16Unorm:
                    case MTLPixelFormat.Depth32Float:
                        depthAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        depthAttachment.LoadAction = MTLLoadAction.Load;
                        depthAttachment.StoreAction = MTLStoreAction.Store;
                        break;

                    // Stencil Only Attachment
                    case MTLPixelFormat.Stencil8:
                        stencilAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        stencilAttachment.LoadAction = MTLLoadAction.Load;
                        stencilAttachment.StoreAction = MTLStoreAction.Store;
                        break;

                    // Combined Attachment
                    case MTLPixelFormat.Depth24UnormStencil8:
                    case MTLPixelFormat.Depth32FloatStencil8:
                        depthAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        depthAttachment.LoadAction = MTLLoadAction.Load;
                        depthAttachment.StoreAction = MTLStoreAction.Store;

                        stencilAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        stencilAttachment.LoadAction = MTLLoadAction.Load;
                        stencilAttachment.StoreAction = MTLStoreAction.Store;
                        break;
                    default:
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"Unsupported Depth/Stencil Format: {_currentState.DepthStencil.GetHandle().PixelFormat}!");
                        break;
                }
            }

            // Initialise Encoder
            var renderCommandEncoder = _pipeline.CommandBuffer.RenderCommandEncoder(renderPassDescriptor);

            // Mark all state as dirty to ensure it is set on the encoder
            _currentState.Dirty.MarkAll();

            // Rebind all the state
            SetDepthClamp(renderCommandEncoder);
            SetDepthBias(renderCommandEncoder);
            SetCullMode(renderCommandEncoder);
            SetFrontFace(renderCommandEncoder);
            SetStencilRefValue(renderCommandEncoder);
            SetViewports(renderCommandEncoder);
            SetScissors(renderCommandEncoder);
            SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
            SetRenderBuffers(renderCommandEncoder, _currentState.UniformBuffers, true);
            SetRenderBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
            for (ulong i = 0; i < Constants.MaxTextures; i++)
            {
                SetRenderTexture(renderCommandEncoder, ShaderStage.Vertex, i, _currentState.VertexTextures[i]);
                SetRenderTexture(renderCommandEncoder, ShaderStage.Fragment, i, _currentState.FragmentTextures[i]);
            }
            for (ulong i = 0; i < Constants.MaxSamplers; i++)
            {
                SetRenderSampler(renderCommandEncoder, ShaderStage.Vertex, i, _currentState.VertexSamplers[i]);
                SetRenderSampler(renderCommandEncoder, ShaderStage.Fragment, i, _currentState.FragmentSamplers[i]);
            }

            // Cleanup
            renderPassDescriptor.Dispose();

            return renderCommandEncoder;
        }

        public readonly MTLComputeCommandEncoder CreateComputeCommandEncoder()
        {
            var descriptor = new MTLComputePassDescriptor();
            var computeCommandEncoder = _pipeline.CommandBuffer.ComputeCommandEncoder(descriptor);

            // Rebind all the state
            SetComputeBuffers(computeCommandEncoder, _currentState.UniformBuffers);
            SetComputeBuffers(computeCommandEncoder, _currentState.StorageBuffers);
            for (ulong i = 0; i < Constants.MaxTextures; i++)
            {
                SetComputeTexture(computeCommandEncoder, i, _currentState.ComputeTextures[i]);
            }
            for (ulong i = 0; i < Constants.MaxSamplers; i++)
            {
                SetComputeSampler(computeCommandEncoder, i, _currentState.ComputeSamplers[i]);
            }

            // Cleanup
            descriptor.Dispose();

            return computeCommandEncoder;
        }

        public void RebindRenderState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.Dirty.RenderPipeline)
            {
                SetRenderPipelineState(renderCommandEncoder);
            }

            if (_currentState.Dirty.DepthStencil)
            {
                SetDepthStencilState(renderCommandEncoder);
            }

            // Clear the dirty flags
            _currentState.Dirty.RenderPipeline = false;
            _currentState.Dirty.DepthStencil = false;
        }

        public void RebindComputeState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if (_currentState.Dirty.ComputePipeline)
            {
                SetComputePipelineState(computeCommandEncoder);
            }

            // Clear the dirty flags
            _currentState.Dirty.ComputePipeline = false;
        }

        private readonly void SetRenderPipelineState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor();

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                if (_currentState.RenderTargets[i] != null)
                {
                    var pipelineAttachment = renderPipelineDescriptor.ColorAttachments.Object((ulong)i);
                    pipelineAttachment.PixelFormat = _currentState.RenderTargets[i].GetHandle().PixelFormat;
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
                switch (_currentState.DepthStencil.GetHandle().PixelFormat)
                {
                    // Depth Only Attachment
                    case MTLPixelFormat.Depth16Unorm:
                    case MTLPixelFormat.Depth32Float:
                        renderPipelineDescriptor.DepthAttachmentPixelFormat = _currentState.DepthStencil.GetHandle().PixelFormat;
                        break;

                    // Stencil Only Attachment
                    case MTLPixelFormat.Stencil8:
                        renderPipelineDescriptor.StencilAttachmentPixelFormat = _currentState.DepthStencil.GetHandle().PixelFormat;
                        break;

                    // Combined Attachment
                    case MTLPixelFormat.Depth24UnormStencil8:
                    case MTLPixelFormat.Depth32FloatStencil8:
                        renderPipelineDescriptor.DepthAttachmentPixelFormat = _currentState.DepthStencil.GetHandle().PixelFormat;
                        renderPipelineDescriptor.StencilAttachmentPixelFormat = _currentState.DepthStencil.GetHandle().PixelFormat;
                        break;
                    default:
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"Unsupported Depth/Stencil Format: {_currentState.DepthStencil.GetHandle().PixelFormat}!");
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

        private readonly void SetComputePipelineState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if (_currentState.ComputeFunction == null)
            {
                return;
            }

            var pipelineState = _computePipelineCache.GetOrCreate(_currentState.ComputeFunction.Value);

            computeCommandEncoder.SetComputePipelineState(pipelineState);
        }

        public void UpdateIndexBuffer(BufferRange buffer, IndexType type)
        {
            if (buffer.Handle != BufferHandle.Null)
            {
                if (type == GAL.IndexType.UByte)
                {
                    _currentState.IndexType = MTLIndexType.UInt16;
                    _currentState.IndexBufferOffset = (ulong)buffer.Offset;
                    _currentState.IndexBuffer = _bufferManager.GetBufferI8ToI16(_pipeline.CurrentCommandBuffer, buffer.Handle, buffer.Offset, buffer.Size);
                }
                else
                {
                    _currentState.IndexType = type.Convert();
                    _currentState.IndexBufferOffset = (ulong)buffer.Offset;
                    _currentState.IndexBuffer = _bufferManager.GetBuffer(buffer.Handle, false);
                }
            }
        }

        public void UpdatePrimitiveTopology(PrimitiveTopology topology)
        {
            _currentState.Topology = topology;
        }

        public void UpdateProgram(IProgram program)
        {
            Program prg = (Program)program;

            if (prg.VertexFunction == IntPtr.Zero && prg.ComputeFunction == IntPtr.Zero)
            {
                if (prg.FragmentFunction == IntPtr.Zero)
                {
                    Logger.Error?.PrintMsg(LogClass.Gpu, "No compute function");
                }
                else
                {
                    Logger.Error?.PrintMsg(LogClass.Gpu, "No vertex function");
                }
                return;
            }

            if (prg.VertexFunction != IntPtr.Zero)
            {
                _currentState.VertexFunction = prg.VertexFunction;
                _currentState.FragmentFunction = prg.FragmentFunction;

                // Mark dirty
                _currentState.Dirty.RenderPipeline = true;
            }
            if (prg.ComputeFunction != IntPtr.Zero)
            {
                _currentState.ComputeFunction = prg.ComputeFunction;

                // Mark dirty
                _currentState.Dirty.ComputePipeline = true;
            }
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
            _currentState.Dirty.RenderPipeline = true;
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
        public void UpdateDepthBias(float depthBias, float slopeScale, float clamp)
        {
            _currentState.DepthBias = depthBias;
            _currentState.SlopeScale = slopeScale;
            _currentState.Clamp = clamp;

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetDepthBias(renderCommandEncoder);
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
            _currentState.Dirty.RenderPipeline = true;
        }

        // Inlineable
        public void UpdateUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _currentState.UniformBuffers = new BufferRef[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                var assignment = buffers[i];
                var buffer = assignment.Range;
                int index = assignment.Binding;

                Auto<DisposableBuffer> mtlBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(buffer.Handle, buffer.Write);

                _currentState.UniformBuffers[i] = new BufferRef(mtlBuffer, index, ref buffer);
            }

            // Inline update
            if (_pipeline.CurrentEncoder != null)
            {
                if (_pipeline.CurrentEncoderType == EncoderType.Render)
                {
                    var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetRenderBuffers(renderCommandEncoder, _currentState.UniformBuffers, true);
                }
                else if (_pipeline.CurrentEncoderType == EncoderType.Compute)
                {
                    var computeCommandEncoder = new MTLComputeCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetComputeBuffers(computeCommandEncoder, _currentState.UniformBuffers);
                }
            }
        }

        // Inlineable
        public void UpdateStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _currentState.StorageBuffers = new BufferRef[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                var assignment = buffers[i];
                var buffer = assignment.Range;
                // TODO: Dont do this
                int index = assignment.Binding + 15;

                Auto<DisposableBuffer> mtlBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(buffer.Handle, buffer.Write);

                _currentState.StorageBuffers[i] = new BufferRef(mtlBuffer, index, ref buffer);
            }

            // Inline update
            if (_pipeline.CurrentEncoder != null)
            {
                if (_pipeline.CurrentEncoderType == EncoderType.Render)
                {
                    var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetRenderBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
                }
                else if (_pipeline.CurrentEncoderType == EncoderType.Compute)
                {
                    var computeCommandEncoder = new MTLComputeCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetComputeBuffers(computeCommandEncoder, _currentState.StorageBuffers);
                }
            }
        }

        // Inlineable
        public void UpdateStorageBuffers(int first, ReadOnlySpan<Auto<DisposableBuffer>> buffers)
        {
            _currentState.StorageBuffers = new BufferRef[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                var mtlBuffer = buffers[i];
                int index = first + i;

                _currentState.StorageBuffers[i] = new BufferRef(mtlBuffer, index);
            }

            // Inline update
            if (_pipeline.CurrentEncoder != null)
            {
                if (_pipeline.CurrentEncoderType == EncoderType.Render)
                {
                    var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetRenderBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
                }
                else if (_pipeline.CurrentEncoderType == EncoderType.Compute)
                {
                    var computeCommandEncoder = new MTLComputeCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetComputeBuffers(computeCommandEncoder, _currentState.StorageBuffers);
                }
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
        public readonly void UpdateTexture(ShaderStage stage, ulong binding, TextureBase texture)
        {
            if (binding > 30)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Texture binding ({binding}) must be <= 30");
                return;
            }
            switch (stage)
            {
                case ShaderStage.Fragment:
                    _currentState.FragmentTextures[binding] = texture;
                    break;
                case ShaderStage.Vertex:
                    _currentState.VertexTextures[binding] = texture;
                    break;
                case ShaderStage.Compute:
                    _currentState.ComputeTextures[binding] = texture;
                    break;
            }

            if (_pipeline.CurrentEncoder != null)
            {
                if (_pipeline.CurrentEncoderType == EncoderType.Render)
                {
                    var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetRenderTexture(renderCommandEncoder, ShaderStage.Vertex, binding, texture);
                    SetRenderTexture(renderCommandEncoder, ShaderStage.Fragment, binding, texture);
                }
                else if (_pipeline.CurrentEncoderType == EncoderType.Compute)
                {
                    var computeCommandEncoder = new MTLComputeCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetComputeTexture(computeCommandEncoder, binding, texture);
                }
            }
        }

        // Inlineable
        public readonly void UpdateSampler(ShaderStage stage, ulong binding, MTLSamplerState sampler)
        {
            if (binding > 15)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Sampler binding ({binding}) must be <= 15");
                return;
            }
            switch (stage)
            {
                case ShaderStage.Fragment:
                    _currentState.FragmentSamplers[binding] = sampler;
                    break;
                case ShaderStage.Vertex:
                    _currentState.VertexSamplers[binding] = sampler;
                    break;
                case ShaderStage.Compute:
                    _currentState.ComputeSamplers[binding] = sampler;
                    break;
            }

            if (_pipeline.CurrentEncoder != null)
            {
                if (_pipeline.CurrentEncoderType == EncoderType.Render)
                {
                    var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetRenderSampler(renderCommandEncoder, ShaderStage.Vertex, binding, sampler);
                    SetRenderSampler(renderCommandEncoder, ShaderStage.Fragment, binding, sampler);
                }
                else if (_pipeline.CurrentEncoderType == EncoderType.Compute)
                {
                    var computeCommandEncoder = new MTLComputeCommandEncoder(_pipeline.CurrentEncoder.Value);
                    SetComputeSampler(computeCommandEncoder, binding, sampler);
                }
            }
        }

        // Inlineable
        public readonly void UpdateTextureAndSampler(ShaderStage stage, ulong binding, TextureBase texture, MTLSamplerState sampler)
        {
            UpdateTexture(stage, binding, texture);
            UpdateSampler(stage, binding, sampler);
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

        private readonly void SetDepthBias(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetDepthBias(_currentState.DepthBias, _currentState.SlopeScale, _currentState.Clamp);
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

                if ((indexMask & (1u << i)) != 0)
                {
                    layout.Stride = (ulong)bufferDescriptors[i].Stride;

                    if (layout.Stride == 0)
                    {
                        layout.Stride = 1;
                        layout.StepFunction = MTLVertexStepFunction.Constant;
                        layout.StepRate = 0;
                    }
                    else
                    {
                        if (bufferDescriptors[i].Divisor > 0)
                        {
                            layout.StepFunction = MTLVertexStepFunction.PerInstance;
                            layout.StepRate = (ulong)bufferDescriptors[i].Divisor;
                        }
                        else
                        {
                            layout.StepFunction = MTLVertexStepFunction.PerVertex;
                            layout.StepRate = 1;
                        }
                    }
                }
                else
                {
                    layout.Stride = 0;
                }
            }

            // Zero buffer
            if ((indexMask & (1u << bufferDescriptors.Length)) != 0)
            {
                var layout = vertexDescriptor.Layouts.Object((ulong)bufferDescriptors.Length);
                layout.Stride = 1;
                layout.StepFunction = MTLVertexStepFunction.Constant;
                layout.StepRate = 0;
            }

            return vertexDescriptor;
        }

        private void SetVertexBuffers(MTLRenderCommandEncoder renderCommandEncoder, VertexBufferDescriptor[] bufferDescriptors)
        {
            var buffers = new List<BufferRef>();

            for (int i = 0; i < bufferDescriptors.Length; i++)
            {
                Auto<DisposableBuffer> mtlBuffer = bufferDescriptors[i].Buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(bufferDescriptors[i].Buffer.Handle, bufferDescriptors[i].Buffer.Write);

                var range = bufferDescriptors[i].Buffer;

                buffers.Add(new BufferRef(mtlBuffer, i, ref range));
            }

            var zeroBufferRange = new BufferRange(_zeroBuffer, 0, ZeroBufferSize);

            Auto<DisposableBuffer> zeroBuffer = _zeroBuffer == BufferHandle.Null
                ? null
                : _bufferManager.GetBuffer(_zeroBuffer, false);

            // Zero buffer
            buffers.Add(new BufferRef(zeroBuffer, bufferDescriptors.Length, ref zeroBufferRange));

            SetRenderBuffers(renderCommandEncoder, buffers.ToArray());
        }

        private readonly void SetRenderBuffers(MTLRenderCommandEncoder renderCommandEncoder, BufferRef[] buffers, bool fragment = false)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var range = buffers[i].Range;
                var autoBuffer = buffers[i].Buffer;
                var offset = 0;
                var index = buffers[i].Index;

                if (autoBuffer == null)
                {
                    continue;
                }

                MTLBuffer mtlBuffer;

                if (range.HasValue)
                {
                    offset = range.Value.Offset;
                    mtlBuffer = autoBuffer.Get(_pipeline.CurrentCommandBuffer, offset, range.Value.Size, range.Value.Write).Value;

                }
                else
                {
                    mtlBuffer = autoBuffer.Get(_pipeline.CurrentCommandBuffer).Value;
                }

                renderCommandEncoder.SetVertexBuffer(mtlBuffer, (ulong)offset, (ulong)index);

                if (fragment)
                {
                    renderCommandEncoder.SetFragmentBuffer(mtlBuffer, (ulong)offset, (ulong)index);
                }
            }
        }

        private readonly void SetComputeBuffers(MTLComputeCommandEncoder computeCommandEncoder, BufferRef[] buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var range = buffers[i].Range;
                var autoBuffer = buffers[i].Buffer;
                var offset = 0;
                var index = buffers[i].Index;

                if (autoBuffer == null)
                {
                    continue;
                }

                MTLBuffer mtlBuffer;

                if (range.HasValue)
                {
                    offset = range.Value.Offset;
                    mtlBuffer = autoBuffer.Get(_pipeline.CurrentCommandBuffer, offset, range.Value.Size, range.Value.Write).Value;

                }
                else
                {
                    mtlBuffer = autoBuffer.Get(_pipeline.CurrentCommandBuffer).Value;
                }

                computeCommandEncoder.SetBuffer(mtlBuffer, (ulong)offset, (ulong)index);
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

        private static void SetRenderTexture(MTLRenderCommandEncoder renderCommandEncoder, ShaderStage stage, ulong binding, TextureBase texture)
        {
            if (texture == null)
            {
                return;
            }

            var textureHandle = texture.GetHandle();
            if (textureHandle != IntPtr.Zero)
            {
                switch (stage)
                {
                    case ShaderStage.Vertex:
                        renderCommandEncoder.SetVertexTexture(textureHandle, binding);
                        break;
                    case ShaderStage.Fragment:
                        renderCommandEncoder.SetFragmentTexture(textureHandle, binding);
                        break;
                }
            }
        }

        private static void SetRenderSampler(MTLRenderCommandEncoder renderCommandEncoder, ShaderStage stage, ulong binding, MTLSamplerState sampler)
        {
            if (sampler != IntPtr.Zero)
            {
                switch (stage)
                {
                    case ShaderStage.Vertex:
                        renderCommandEncoder.SetVertexSamplerState(sampler, binding);
                        break;
                    case ShaderStage.Fragment:
                        renderCommandEncoder.SetFragmentSamplerState(sampler, binding);
                        break;
                }
            }
        }

        private static void SetComputeTexture(MTLComputeCommandEncoder computeCommandEncoder, ulong binding, TextureBase texture)
        {
            if (texture == null)
            {
                return;
            }

            var textureHandle = texture.GetHandle();
            if (textureHandle != IntPtr.Zero)
            {
                computeCommandEncoder.SetTexture(textureHandle, binding);
            }
        }

        private static void SetComputeSampler(MTLComputeCommandEncoder computeCommandEncoder, ulong binding, MTLSamplerState sampler)
        {
            if (sampler != IntPtr.Zero)
            {
                computeCommandEncoder.SetSamplerState(sampler, binding);
            }
        }
    }
}
