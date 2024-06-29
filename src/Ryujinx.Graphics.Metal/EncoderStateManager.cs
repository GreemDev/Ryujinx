using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Metal.State;
using Ryujinx.Graphics.Shader;
using SharpMetal.Metal;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BufferAssignment = Ryujinx.Graphics.GAL.BufferAssignment;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct EncoderStateManager : IDisposable
    {
        private readonly MTLDevice _device;
        private readonly Pipeline _pipeline;
        private readonly BufferManager _bufferManager;

        private readonly DepthStencilCache _depthStencilCache;

        private readonly EncoderState _mainState = new();
        private EncoderState _currentState;

        public readonly Auto<DisposableBuffer> IndexBuffer => _currentState.IndexBuffer;
        public readonly MTLIndexType IndexType => _currentState.IndexType;
        public readonly ulong IndexBufferOffset => _currentState.IndexBufferOffset;
        public readonly PrimitiveTopology Topology => _currentState.Topology;
        public readonly Texture[] RenderTargets => _currentState.RenderTargets;
        public readonly Texture DepthStencil => _currentState.DepthStencil;
        public readonly ComputeSize ComputeLocalSize => _currentState.ComputeProgram.ComputeLocalSize;

        // RGBA32F is the biggest format
        private const int ZeroBufferSize = 4 * 4;
        private readonly BufferHandle _zeroBuffer;

        public unsafe EncoderStateManager(MTLDevice device, BufferManager bufferManager, Pipeline pipeline)
        {
            _device = device;
            _pipeline = pipeline;
            _bufferManager = bufferManager;

            _depthStencilCache = new(device);
            _currentState = _mainState;

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

            _depthStencilCache.Dispose();
        }

        public EncoderState SwapState(EncoderState state, DirtyFlags flags = DirtyFlags.All)
        {
            _currentState = state ?? _mainState;

            _currentState.Dirty |= flags;

            return _mainState;
        }

        public PredrawState SavePredrawState()
        {
            return new PredrawState
            {
                CullMode = _currentState.CullMode,
                DepthStencilUid = _currentState.DepthStencilUid,
                Topology = _currentState.Topology,
                Viewports = _currentState.Viewports.ToArray(),
            };
        }

        public void RestorePredrawState(PredrawState state)
        {
            _currentState.CullMode = state.CullMode;
            _currentState.DepthStencilUid = state.DepthStencilUid;
            _currentState.Topology = state.Topology;
            _currentState.Viewports = state.Viewports;

            _currentState.Dirty |= DirtyFlags.CullMode | DirtyFlags.DepthStencil | DirtyFlags.Viewports;
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
            _currentState.Dirty |= DirtyFlags.RenderAll;

            // Cleanup
            renderPassDescriptor.Dispose();

            return renderCommandEncoder;
        }

        public MTLComputeCommandEncoder CreateComputeCommandEncoder()
        {
            var descriptor = new MTLComputePassDescriptor();
            var computeCommandEncoder = _pipeline.CommandBuffer.ComputeCommandEncoder(descriptor);

            // Mark all state as dirty to ensure it is set on the encoder
            _currentState.Dirty |= DirtyFlags.ComputeAll;

            // Cleanup
            descriptor.Dispose();

            return computeCommandEncoder;
        }

        public void RebindRenderState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.Dirty.HasFlag(DirtyFlags.RenderPipeline))
            {
                SetRenderPipelineState(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.DepthStencil))
            {
                SetDepthStencilState(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.DepthClamp))
            {
                SetDepthClamp(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.DepthBias))
            {
                SetDepthBias(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.CullMode))
            {
                SetCullMode(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.FrontFace))
            {
                SetFrontFace(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.StencilRef))
            {
                SetStencilRefValue(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.Viewports))
            {
                SetViewports(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.Scissors))
            {
                SetScissors(renderCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.VertexBuffers))
            {
                SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.Buffers))
            {
                SetRenderBuffers(renderCommandEncoder, _currentState.UniformBuffers, _currentState.StorageBuffers);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.VertexTextures))
            {
                SetRenderTextures(renderCommandEncoder, ShaderStage.Vertex, _currentState.VertexTextures, _currentState.VertexSamplers);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.FragmentTextures))
            {
                SetRenderTextures(renderCommandEncoder, ShaderStage.Fragment, _currentState.FragmentTextures, _currentState.FragmentSamplers);
            }

            _currentState.Dirty &= ~DirtyFlags.RenderAll;
        }

        public void RebindComputeState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if (_currentState.Dirty.HasFlag(DirtyFlags.ComputePipeline))
            {
                SetComputePipelineState(computeCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.Buffers))
            {
                SetComputeBuffers(computeCommandEncoder, _currentState.UniformBuffers, _currentState.StorageBuffers);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.ComputeTextures))
            {
                SetComputeTextures(computeCommandEncoder, _currentState.ComputeTextures, _currentState.ComputeSamplers);
            }
        }

        private void SetRenderPipelineState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            MTLRenderPipelineState pipelineState = _currentState.Pipeline.CreateRenderPipeline(_device, _currentState.RenderProgram);

            renderCommandEncoder.SetRenderPipelineState(pipelineState);

            renderCommandEncoder.SetBlendColor(
                _currentState.BlendColor.Red,
                _currentState.BlendColor.Green,
                _currentState.BlendColor.Blue,
                _currentState.BlendColor.Alpha);
        }

        private void SetComputePipelineState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if (_currentState.ComputeProgram == null)
            {
                return;
            }

            var pipelineState = PipelineState.CreateComputePipeline(_device, _currentState.ComputeProgram);

            computeCommandEncoder.SetComputePipelineState(pipelineState);
        }

        public void UpdateIndexBuffer(BufferRange buffer, IndexType type)
        {
            if (buffer.Handle != BufferHandle.Null)
            {
                if (type == GAL.IndexType.UByte)
                {
                    _currentState.IndexType = MTLIndexType.UInt16;
                    _currentState.IndexBufferOffset = 0;
                    _currentState.IndexBuffer = _bufferManager.GetBufferI8ToI16(_pipeline.Cbs, buffer.Handle, buffer.Offset, buffer.Size);
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
                _currentState.RenderProgram = prg;

                _currentState.Dirty |= DirtyFlags.RenderPipeline;
            }
            else if (prg.ComputeFunction != IntPtr.Zero)
            {
                _currentState.ComputeProgram = prg;

                _currentState.Dirty |= DirtyFlags.ComputePipeline;
            }
        }

        public void UpdateRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _currentState.FramebufferUsingColorWriteMask = false;
            UpdateRenderTargetsInternal(colors, depthStencil);
        }

        public void UpdateRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            ref var blendState = ref _currentState.Pipeline.Internal.ColorBlendState;

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

                ref ColorBlendStateUid mtlBlend = ref blendState[i];

                // When color write mask is 0, remove all blend state to help the pipeline cache.
                // Restore it when the mask becomes non-zero.
                if (mtlBlend.WriteMask != mask)
                {
                    if (mask == 0)
                    {
                        _currentState.StoredBlend[i] = mtlBlend;

                        mtlBlend = new ColorBlendStateUid();
                    }
                    else if (mtlBlend.WriteMask == 0)
                    {
                        mtlBlend = _currentState.StoredBlend[i];
                    }
                }

                blendState[i].WriteMask = mask;
            }

            if (_currentState.FramebufferUsingColorWriteMask)
            {
                UpdateRenderTargetsInternal(_currentState.PreMaskRenderTargets, _currentState.PreMaskDepthStencil);
            }
            else
            {
                // Requires recreating pipeline
                if (_pipeline.CurrentEncoderType == EncoderType.Render)
                {
                    _pipeline.EndCurrentPass();
                }
            }
        }

        private void UpdateRenderTargetsInternal(ITexture[] colors, ITexture depthStencil)
        {
            // TBDR GPUs don't work properly if the same attachment is bound to multiple targets,
            // due to each attachment being a copy of the real attachment, rather than a direct write.
            //
            // Just try to remove duplicate attachments.
            // Save a copy of the array to rebind when mask changes.

            // Look for textures that are masked out.

            ref PipelineState pipeline = ref _currentState.Pipeline;
            ref var blendState = ref pipeline.Internal.ColorBlendState;

            pipeline.ColorBlendAttachmentStateCount = (uint)colors.Length;

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] == null)
                {
                    continue;
                }

                var mtlMask = blendState[i].WriteMask;

                for (int j = 0; j < i; j++)
                {
                    // Check each binding for a duplicate binding before it.

                    if (colors[i] == colors[j])
                    {
                        // Prefer the binding with no write mask.

                        var mtlMask2 = blendState[j].WriteMask;

                        if (mtlMask == 0)
                        {
                            colors[i] = null;
                            MaskOut(colors, depthStencil);
                        }
                        else if (mtlMask2 == 0)
                        {
                            colors[j] = null;
                            MaskOut(colors, depthStencil);
                        }
                    }
                }
            }

            _currentState.RenderTargets = new Texture[Constants.MaxColorAttachments];

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] is not Texture tex)
                {
                    blendState[i].PixelFormat = MTLPixelFormat.Invalid;

                    continue;
                }

                blendState[i].PixelFormat = tex.GetHandle().PixelFormat; // TODO: cache this
                _currentState.RenderTargets[i] = tex;
            }

            if (depthStencil is Texture depthTexture)
            {
                pipeline.DepthStencilFormat = depthTexture.GetHandle().PixelFormat; // TODO: cache this
                _currentState.DepthStencil = depthTexture;
            }
            else if (depthStencil == null)
            {
                pipeline.DepthStencilFormat = MTLPixelFormat.Invalid;
                _currentState.DepthStencil = null;
            }

            // Requires recreating pipeline
            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        private void MaskOut(ITexture[] colors, ITexture depthStencil)
        {
            if (!_currentState.FramebufferUsingColorWriteMask)
            {
                _currentState.PreMaskRenderTargets = colors;
                _currentState.PreMaskDepthStencil = depthStencil;
            }

            // If true, then the framebuffer must be recreated when the mask changes.
            _currentState.FramebufferUsingColorWriteMask = true;
        }

        public void UpdateVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            _currentState.VertexAttribs = vertexAttribs.ToArray();

            // Update the buffers on the pipeline
            UpdatePipelineVertexState(_currentState.VertexBuffers, _currentState.VertexAttribs);

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.RenderPipeline;
        }

        public void UpdateBlendDescriptors(int index, BlendDescriptor blend)
        {
            ref var blendState = ref _currentState.Pipeline.Internal.ColorBlendState[index];

            blendState.Enable = blend.Enable;
            blendState.AlphaBlendOperation = blend.AlphaOp.Convert();
            blendState.RgbBlendOperation = blend.ColorOp.Convert();
            blendState.SourceAlphaBlendFactor = blend.AlphaSrcFactor.Convert();
            blendState.DestinationAlphaBlendFactor = blend.AlphaDstFactor.Convert();
            blendState.SourceRGBBlendFactor = blend.ColorSrcFactor.Convert();
            blendState.DestinationRGBBlendFactor = blend.ColorDstFactor.Convert();

            if (blendState.WriteMask == 0)
            {
                _currentState.StoredBlend[index] = blendState;

                blendState = new ColorBlendStateUid();
            }

            _currentState.BlendColor = blend.BlendConstant;

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.RenderPipeline;
        }

        // Inlineable
        public void UpdateStencilState(StencilTestDescriptor stencilTest)
        {
            ref DepthStencilUid uid = ref _currentState.DepthStencilUid;

            uid.FrontFace = new StencilUid
            {
                StencilFailureOperation = stencilTest.FrontSFail.Convert(),
                DepthFailureOperation = stencilTest.FrontDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.FrontDpPass.Convert(),
                StencilCompareFunction = stencilTest.FrontFunc.Convert(),
                ReadMask = (uint)stencilTest.FrontFuncMask,
                WriteMask = (uint)stencilTest.FrontMask
            };

            uid.BackFace = new StencilUid
            {
                StencilFailureOperation = stencilTest.BackSFail.Convert(),
                DepthFailureOperation = stencilTest.BackDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.BackDpPass.Convert(),
                StencilCompareFunction = stencilTest.BackFunc.Convert(),
                ReadMask = (uint)stencilTest.BackFuncMask,
                WriteMask = (uint)stencilTest.BackMask
            };

            uid.StencilTestEnabled = stencilTest.TestEnable;

            UpdateStencilRefValue(stencilTest.FrontFuncRef, stencilTest.BackFuncRef);

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthStencil;
        }

        public void UpdateDepthState(DepthTestDescriptor depthTest)
        {
            ref DepthStencilUid uid = ref _currentState.DepthStencilUid;

            uid.DepthCompareFunction = depthTest.TestEnable ? depthTest.Func.Convert() : MTLCompareFunction.Always;
            uid.DepthWriteEnabled = depthTest.TestEnable && depthTest.WriteEnable;

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthStencil;
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
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthClamp;
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
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthBias;
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
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.Scissors;
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
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.Viewports;
        }

        public void UpdateVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            _currentState.VertexBuffers = vertexBuffers.ToArray();

            // Update the buffers on the pipeline
            UpdatePipelineVertexState(_currentState.VertexBuffers, _currentState.VertexAttribs);

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.RenderPipeline | DirtyFlags.VertexBuffers;
        }

        public void UpdateUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            foreach (BufferAssignment assignment in buffers)
            {
                var buffer = assignment.Range;
                int index = assignment.Binding;

                Auto<DisposableBuffer> mtlBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(buffer.Handle, buffer.Write);

                _currentState.UniformBuffers[index] = new BufferRef(mtlBuffer, ref buffer);
            }

            _currentState.Dirty |= DirtyFlags.Buffers;
        }

        public void UpdateStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            foreach (BufferAssignment assignment in buffers)
            {
                var buffer = assignment.Range;
                int index = assignment.Binding;

                Auto<DisposableBuffer> mtlBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(buffer.Handle, buffer.Write);

                _currentState.StorageBuffers[index] = new BufferRef(mtlBuffer, ref buffer);
            }

            _currentState.Dirty |= DirtyFlags.Buffers;
        }

        public void UpdateStorageBuffers(int first, ReadOnlySpan<Auto<DisposableBuffer>> buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var mtlBuffer = buffers[i];
                int index = first + i;

                _currentState.StorageBuffers[index] = new BufferRef(mtlBuffer);
            }

            _currentState.Dirty |= DirtyFlags.Buffers;
        }

        // Inlineable
        public void UpdateCullMode(bool enable, Face face)
        {
            var dirtyScissor = (face == Face.FrontAndBack) != _currentState.CullBoth;

            _currentState.CullMode = enable ? face.Convert() : MTLCullMode.None;
            _currentState.CullBoth = face == Face.FrontAndBack;

            // Inline update
            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetCullMode(renderCommandEncoder);
                SetScissors(renderCommandEncoder);
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.CullMode;

            if (dirtyScissor)
            {
                _currentState.Dirty |= DirtyFlags.Scissors;
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
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.FrontFace;
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

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.StencilRef;
        }

        public void UpdateTexture(ShaderStage stage, ulong binding, TextureBase texture)
        {
            if (binding > Constants.MaxTexturesPerStage)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Texture binding ({binding}) must be <= {Constants.MaxTexturesPerStage}");
                return;
            }

            switch (stage)
            {
                case ShaderStage.Fragment:
                    _currentState.FragmentTextures[binding] = texture;
                    _currentState.Dirty |= DirtyFlags.FragmentTextures;
                    break;
                case ShaderStage.Vertex:
                    _currentState.VertexTextures[binding] = texture;
                    _currentState.Dirty |= DirtyFlags.VertexTextures;
                    break;
                case ShaderStage.Compute:
                    _currentState.ComputeTextures[binding] = texture;
                    _currentState.Dirty |= DirtyFlags.ComputeTextures;
                    break;
            }
        }

        public void UpdateSampler(ShaderStage stage, ulong binding, MTLSamplerState sampler)
        {
            if (binding > Constants.MaxTexturesPerStage)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Sampler binding ({binding}) must be <= {Constants.MaxTexturesPerStage}");
                return;
            }
            switch (stage)
            {
                case ShaderStage.Fragment:
                    _currentState.FragmentSamplers[binding] = sampler;
                    _currentState.Dirty |= DirtyFlags.FragmentTextures;
                    break;
                case ShaderStage.Vertex:
                    _currentState.VertexSamplers[binding] = sampler;
                    _currentState.Dirty |= DirtyFlags.VertexTextures;
                    break;
                case ShaderStage.Compute:
                    _currentState.ComputeSamplers[binding] = sampler;
                    _currentState.Dirty |= DirtyFlags.ComputeTextures;
                    break;
            }
        }

        public void UpdateTextureAndSampler(ShaderStage stage, ulong binding, TextureBase texture, Sampler sampler)
        {
            UpdateTexture(stage, binding, texture);

            if (sampler != null)
            {
                UpdateSampler(stage, binding, sampler.GetSampler());
            }
        }

        private readonly void SetDepthStencilState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            MTLDepthStencilState state = _depthStencilCache.GetOrCreate(_currentState.DepthStencilUid);

            renderCommandEncoder.SetDepthStencilState(state);
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
            var isTriangles = (_currentState.Topology == PrimitiveTopology.Triangles) ||
                              (_currentState.Topology == PrimitiveTopology.TriangleStrip);

            if (_currentState.CullBoth && isTriangles)
            {
                renderCommandEncoder.SetScissorRect(new MTLScissorRect { x = 0, y = 0, width = 0, height = 0});
            }
            else
            {
                if (_currentState.Scissors.Length > 0)
                {
                    fixed (MTLScissorRect* pMtlScissors = _currentState.Scissors)
                    {
                        renderCommandEncoder.SetScissorRects((IntPtr)pMtlScissors, (ulong)_currentState.Scissors.Length);
                    }
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

        private void UpdatePipelineVertexState(VertexBufferDescriptor[] bufferDescriptors, VertexAttribDescriptor[] attribDescriptors)
        {
            ref PipelineState pipeline = ref _currentState.Pipeline;
            uint indexMask = 0;

            for (int i = 0; i < attribDescriptors.Length; i++)
            {
                ref var attrib = ref pipeline.Internal.VertexAttributes[i];

                if (attribDescriptors[i].IsZero)
                {
                    attrib.Format = attribDescriptors[i].Format.Convert();
                    indexMask |= 1u << (int)Constants.ZeroBufferIndex;
                    attrib.BufferIndex = Constants.ZeroBufferIndex;
                    attrib.Offset = 0;
                }
                else
                {
                    attrib.Format = attribDescriptors[i].Format.Convert();
                    indexMask |= 1u << attribDescriptors[i].BufferIndex;
                    attrib.BufferIndex = (ulong)attribDescriptors[i].BufferIndex;
                    attrib.Offset = (ulong)attribDescriptors[i].Offset;
                }
            }

            for (int i = 0; i < bufferDescriptors.Length; i++)
            {
                ref var layout = ref pipeline.Internal.VertexBindings[i];

                if ((indexMask & (1u << i)) != 0)
                {
                    layout.Stride = (uint)bufferDescriptors[i].Stride;

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
                            layout.StepRate = (uint)bufferDescriptors[i].Divisor;
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
                    layout = new();
                }
            }

            // Zero buffer
            if ((indexMask & (1u << (int)Constants.ZeroBufferIndex)) != 0)
            {
                ref var layout = ref pipeline.Internal.VertexBindings[(int)Constants.ZeroBufferIndex];
                layout.Stride = 1;
                layout.StepFunction = MTLVertexStepFunction.Constant;
                layout.StepRate = 0;
            }

            pipeline.VertexAttributeDescriptionsCount = (uint)attribDescriptors.Length;
            pipeline.VertexBindingDescriptionsCount = Constants.ZeroBufferIndex + 1; // TODO: move this out?
        }

        private void SetVertexBuffers(MTLRenderCommandEncoder renderCommandEncoder, VertexBufferDescriptor[] bufferDescriptors)
        {
            for (int i = 0; i < bufferDescriptors.Length; i++)
            {
                Auto<DisposableBuffer> autoBuffer = bufferDescriptors[i].Buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(bufferDescriptors[i].Buffer.Handle, bufferDescriptors[i].Buffer.Write);

                var range = bufferDescriptors[i].Buffer;
                var offset = range.Offset;

                if (autoBuffer == null)
                {
                    continue;
                }

                var mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Size, range.Write).Value;
                renderCommandEncoder.SetVertexBuffer(mtlBuffer, (ulong)offset, (ulong)i);
            }

            Auto<DisposableBuffer> autoZeroBuffer = _zeroBuffer == BufferHandle.Null
                ? null
                : _bufferManager.GetBuffer(_zeroBuffer, false);

            if (autoZeroBuffer == null)
            {
                return;
            }

            var zeroMtlBuffer = autoZeroBuffer.Get(_pipeline.Cbs).Value;
            renderCommandEncoder.SetVertexBuffer(zeroMtlBuffer, 0, Constants.ZeroBufferIndex);
        }

        private readonly void SetRenderBuffers(MTLRenderCommandEncoder renderCommandEncoder, BufferRef[] uniformBuffers, BufferRef[] storageBuffers)
        {
            var uniformArgBufferRange = CreateArgumentBufferForRenderEncoder(renderCommandEncoder, uniformBuffers, true);
            var uniformArgBuffer = _bufferManager.GetBuffer(uniformArgBufferRange.Handle, false).Get(_pipeline.Cbs).Value;

            renderCommandEncoder.SetVertexBuffer(uniformArgBuffer, (ulong)uniformArgBufferRange.Offset, Constants.ConstantBuffersIndex);
            renderCommandEncoder.SetFragmentBuffer(uniformArgBuffer, (ulong)uniformArgBufferRange.Offset, Constants.ConstantBuffersIndex);

            var storageArgBufferRange = CreateArgumentBufferForRenderEncoder(renderCommandEncoder, storageBuffers, false);
            var storageArgBuffer = _bufferManager.GetBuffer(storageArgBufferRange.Handle, true).Get(_pipeline.Cbs).Value;

            renderCommandEncoder.SetVertexBuffer(storageArgBuffer, (ulong)storageArgBufferRange.Offset, Constants.StorageBuffersIndex);
            renderCommandEncoder.SetFragmentBuffer(storageArgBuffer, (ulong)storageArgBufferRange.Offset, Constants.StorageBuffersIndex);
        }

        private readonly void SetComputeBuffers(MTLComputeCommandEncoder computeCommandEncoder, BufferRef[] uniformBuffers, BufferRef[] storageBuffers)
        {
            var uniformArgBufferRange = CreateArgumentBufferForComputeEncoder(computeCommandEncoder, uniformBuffers, true);
            var uniformArgBuffer = _bufferManager.GetBuffer(uniformArgBufferRange.Handle, false).Get(_pipeline.Cbs).Value;

            computeCommandEncoder.SetBuffer(uniformArgBuffer, (ulong)uniformArgBufferRange.Offset, Constants.ConstantBuffersIndex);


            var storageArgBufferRange = CreateArgumentBufferForComputeEncoder(computeCommandEncoder, storageBuffers, false);
            var storageArgBuffer = _bufferManager.GetBuffer(storageArgBufferRange.Handle, true).Get(_pipeline.Cbs).Value;

            computeCommandEncoder.SetBuffer(storageArgBuffer, (ulong)storageArgBufferRange.Offset, Constants.StorageBuffersIndex);
        }

        private readonly BufferRange CreateArgumentBufferForRenderEncoder(MTLRenderCommandEncoder renderCommandEncoder, BufferRef[] buffers, bool constant)
        {
            var usage = constant ? MTLResourceUsage.Read : MTLResourceUsage.Write;

            Span<ulong> resourceIds = stackalloc ulong[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                var range = buffers[i].Range;
                var autoBuffer = buffers[i].Buffer;
                var offset = 0;

                if (autoBuffer == null)
                {
                    continue;
                }

                MTLBuffer mtlBuffer;

                if (range.HasValue)
                {
                    offset = range.Value.Offset;
                    mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Value.Size, range.Value.Write).Value;

                }
                else
                {
                    mtlBuffer = autoBuffer.Get(_pipeline.Cbs).Value;
                }

                renderCommandEncoder.UseResource(new MTLResource(mtlBuffer.NativePtr), usage, MTLRenderStages.RenderStageFragment | MTLRenderStages.RenderStageVertex);
                resourceIds[i] = mtlBuffer.GpuAddress + (ulong)offset;
            }

            var sizeOfArgumentBuffer = sizeof(ulong) * buffers.Length;

            var argBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, sizeOfArgumentBuffer);
            argBuffer.Holder.SetDataUnchecked(argBuffer.Offset, MemoryMarshal.AsBytes(resourceIds));

            return argBuffer.Range;
        }

        private readonly BufferRange CreateArgumentBufferForComputeEncoder(MTLComputeCommandEncoder computeCommandEncoder, BufferRef[] buffers, bool constant)
        {
            var usage = constant ? MTLResourceUsage.Read : MTLResourceUsage.Write;

            Span<ulong> resourceIds = stackalloc ulong[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                var range = buffers[i].Range;
                var autoBuffer = buffers[i].Buffer;
                var offset = 0;

                if (autoBuffer == null)
                {
                    continue;
                }

                MTLBuffer mtlBuffer;

                if (range.HasValue)
                {
                    offset = range.Value.Offset;
                    mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Value.Size, range.Value.Write).Value;

                }
                else
                {
                    mtlBuffer = autoBuffer.Get(_pipeline.Cbs).Value;
                }

                computeCommandEncoder.UseResource(new MTLResource(mtlBuffer.NativePtr), usage);
                resourceIds[i] = mtlBuffer.GpuAddress + (ulong)offset;
            }

            var sizeOfArgumentBuffer = sizeof(ulong) * buffers.Length;

            var argBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, sizeOfArgumentBuffer);
            argBuffer.Holder.SetDataUnchecked(argBuffer.Offset, MemoryMarshal.AsBytes(resourceIds));

            return argBuffer.Range;
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

        private readonly void SetRenderTextures(MTLRenderCommandEncoder renderCommandEncoder, ShaderStage stage, TextureBase[] textures, MTLSamplerState[] samplers)
        {
            var argBufferRange = CreateArgumentBufferForRenderEncoder(renderCommandEncoder, stage, textures, samplers);
            var argBuffer = _bufferManager.GetBuffer(argBufferRange.Handle, false).Get(_pipeline.Cbs).Value;

            switch (stage)
            {
                case ShaderStage.Vertex:
                    renderCommandEncoder.SetVertexBuffer(argBuffer, (ulong)argBufferRange.Offset, Constants.TexturesIndex);
                    break;
                case ShaderStage.Fragment:
                    renderCommandEncoder.SetFragmentBuffer(argBuffer, (ulong)argBufferRange.Offset, Constants.TexturesIndex);
                    break;
            }
        }

        private readonly void SetComputeTextures(MTLComputeCommandEncoder computeCommandEncoder, TextureBase[] textures, MTLSamplerState[] samplers)
        {
            var argBufferRange = CreateArgumentBufferForComputeEncoder(computeCommandEncoder, textures, samplers);
            var argBuffer = _bufferManager.GetBuffer(argBufferRange.Handle, false).Get(_pipeline.Cbs).Value;

            computeCommandEncoder.SetBuffer(argBuffer, (ulong)argBufferRange.Offset, Constants.TexturesIndex);
        }

        private readonly BufferRange CreateArgumentBufferForRenderEncoder(MTLRenderCommandEncoder renderCommandEncoder, ShaderStage stage, TextureBase[] textures, MTLSamplerState[] samplers)
        {
            var renderStage = stage == ShaderStage.Vertex ? MTLRenderStages.RenderStageVertex : MTLRenderStages.RenderStageFragment;

            Span<ulong> resourceIds = stackalloc ulong[textures.Length + samplers.Length];

            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null)
                {
                    continue;
                }

                var mtlTexture = textures[i].GetHandle();

                renderCommandEncoder.UseResource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, renderStage);
                resourceIds[i] = mtlTexture.GpuResourceID._impl;
            }

            for (int i = 0; i < samplers.Length; i++)
            {
                if (samplers[i].NativePtr == IntPtr.Zero)
                {
                    continue;
                }

                var sampler = samplers[i];

                resourceIds[i + textures.Length] = sampler.GpuResourceID._impl;
            }

            var sizeOfArgumentBuffer = sizeof(ulong) * (textures.Length + samplers.Length);

            var argBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, sizeOfArgumentBuffer);
            argBuffer.Holder.SetDataUnchecked(argBuffer.Offset, MemoryMarshal.AsBytes(resourceIds));

            return argBuffer.Range;
        }

        private readonly BufferRange CreateArgumentBufferForComputeEncoder(MTLComputeCommandEncoder computeCommandEncoder, TextureBase[] textures, MTLSamplerState[] samplers)
        {
            Span<ulong> resourceIds = stackalloc ulong[textures.Length + samplers.Length];

            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null)
                {
                    continue;
                }

                var mtlTexture = textures[i].GetHandle();

                computeCommandEncoder.UseResource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read);
                resourceIds[i] = mtlTexture.GpuResourceID._impl;
            }

            for (int i = 0; i < samplers.Length; i++)
            {
                if (samplers[i].NativePtr == IntPtr.Zero)
                {
                    continue;
                }

                var sampler = samplers[i];

                resourceIds[i + textures.Length] = sampler.GpuResourceID._impl;
            }

            var sizeOfArgumentBuffer = sizeof(ulong) * (textures.Length + samplers.Length);

            var argBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, sizeOfArgumentBuffer);
            argBuffer.Holder.SetDataUnchecked(argBuffer.Offset, MemoryMarshal.AsBytes(resourceIds));

            return argBuffer.Range;
        }
    }
}
