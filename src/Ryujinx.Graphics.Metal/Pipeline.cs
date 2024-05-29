using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    enum EncoderType
    {
        Blit,
        Compute,
        Render,
        None
    }

    [SupportedOSPlatform("macos")]
    class Pipeline : IPipeline, IDisposable
    {
        private readonly MTLDevice _device;
        private readonly MTLCommandQueue _commandQueue;
        private readonly HelperShader _helperShader;

        private MTLCommandBuffer _commandBuffer;
        public MTLCommandBuffer CommandBuffer => _commandBuffer;

        private MTLCommandEncoder? _currentEncoder;
        public MTLCommandEncoder? CurrentEncoder => _currentEncoder;

        private EncoderType _currentEncoderType = EncoderType.None;
        public EncoderType CurrentEncoderType => _currentEncoderType;

        private EncoderStateManager _encoderStateManager;

        public Pipeline(MTLDevice device, MTLCommandQueue commandQueue)
        {
            _device = device;
            _commandQueue = commandQueue;
            _helperShader = new HelperShader(_device, this);

            _commandBuffer = _commandQueue.CommandBuffer();
            _encoderStateManager = new EncoderStateManager(_device, this);
        }

        public void SaveState()
        {
            _encoderStateManager.SaveState();
        }

        public void SaveAndResetState()
        {
            _encoderStateManager.SaveAndResetState();
        }

        public void RestoreState()
        {
            _encoderStateManager.RestoreState();
        }

        public void SetClearLoadAction(bool clear)
        {
            _encoderStateManager.SetClearLoadAction(clear);
        }

        public MTLRenderCommandEncoder GetOrCreateRenderEncoder()
        {
            MTLRenderCommandEncoder renderCommandEncoder;
            if (_currentEncoder == null || _currentEncoderType != EncoderType.Render)
            {
                renderCommandEncoder = BeginRenderPass();
            }
            else
            {
                renderCommandEncoder = new MTLRenderCommandEncoder(_currentEncoder.Value);
            }

            _encoderStateManager.RebindRenderState(renderCommandEncoder);

            return renderCommandEncoder;
        }

        public MTLBlitCommandEncoder GetOrCreateBlitEncoder()
        {
            if (_currentEncoder != null)
            {
                if (_currentEncoderType == EncoderType.Blit)
                {
                    return new MTLBlitCommandEncoder(_currentEncoder.Value);
                }
            }

            return BeginBlitPass();
        }

        public MTLComputeCommandEncoder GetOrCreateComputeEncoder()
        {
            MTLComputeCommandEncoder computeCommandEncoder;
            if (_currentEncoder == null || _currentEncoderType != EncoderType.Compute)
            {
                computeCommandEncoder = BeginComputePass();
            }
            else
            {
                computeCommandEncoder = new MTLComputeCommandEncoder(_currentEncoder.Value);
            }

            _encoderStateManager.RebindComputeState(computeCommandEncoder);

            return computeCommandEncoder;
        }

        public void EndCurrentPass()
        {
            if (_currentEncoder != null)
            {
                switch (_currentEncoderType)
                {
                    case EncoderType.Blit:
                        new MTLBlitCommandEncoder(_currentEncoder.Value).EndEncoding();
                        _currentEncoder = null;
                        break;
                    case EncoderType.Compute:
                        new MTLComputeCommandEncoder(_currentEncoder.Value).EndEncoding();
                        _currentEncoder = null;
                        break;
                    case EncoderType.Render:
                        new MTLRenderCommandEncoder(_currentEncoder.Value).EndEncoding();
                        _currentEncoder = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _currentEncoderType = EncoderType.None;
            }
        }

        public MTLRenderCommandEncoder BeginRenderPass()
        {
            EndCurrentPass();

            var renderCommandEncoder = _encoderStateManager.CreateRenderCommandEncoder();

            _currentEncoder = renderCommandEncoder;
            _currentEncoderType = EncoderType.Render;

            return renderCommandEncoder;
        }

        public MTLBlitCommandEncoder BeginBlitPass()
        {
            EndCurrentPass();

            var descriptor = new MTLBlitPassDescriptor();
            var blitCommandEncoder = _commandBuffer.BlitCommandEncoder(descriptor);

            _currentEncoder = blitCommandEncoder;
            _currentEncoderType = EncoderType.Blit;
            return blitCommandEncoder;
        }

        public MTLComputeCommandEncoder BeginComputePass()
        {
            EndCurrentPass();

            var computeCommandEncoder = _encoderStateManager.CreateComputeCommandEncoder();

            _currentEncoder = computeCommandEncoder;
            _currentEncoderType = EncoderType.Compute;
            return computeCommandEncoder;
        }

        public void Present(CAMetalDrawable drawable, Texture src, Extents2D srcRegion, Extents2D dstRegion, bool isLinear)
        {
            // TODO: Clean this up
            var textureInfo = new TextureCreateInfo((int)drawable.Texture.Width, (int)drawable.Texture.Height, (int)drawable.Texture.Depth, (int)drawable.Texture.MipmapLevelCount, (int)drawable.Texture.SampleCount, 0, 0, 0, Format.B8G8R8A8Unorm, 0, Target.Texture2D, SwizzleComponent.Red, SwizzleComponent.Green, SwizzleComponent.Blue, SwizzleComponent.Alpha);
            var dst = new Texture(_device, this, textureInfo, drawable.Texture, 0, 0);

            _helperShader.BlitColor(src, dst, srcRegion, dstRegion, isLinear);

            EndCurrentPass();

            _commandBuffer.PresentDrawable(drawable);
            _commandBuffer.Commit();

            _commandBuffer = _commandQueue.CommandBuffer();

            // Cleanup
            dst.Dispose();
        }

        public void BlitColor(
            ITexture src,
            ITexture dst,
            Extents2D srcRegion,
            Extents2D dstRegion,
            bool linearFilter)
        {
            _helperShader.BlitColor(src, dst, srcRegion, dstRegion, linearFilter);
        }

        public void Barrier()
        {

            if (_currentEncoderType == EncoderType.Render)
            {
                var renderCommandEncoder = GetOrCreateRenderEncoder();

                var scope = MTLBarrierScope.Buffers | MTLBarrierScope.Textures | MTLBarrierScope.RenderTargets;
                MTLRenderStages stages = MTLRenderStages.RenderStageVertex | MTLRenderStages.RenderStageFragment;
                renderCommandEncoder.MemoryBarrier(scope, stages, stages);
            }
            else if (_currentEncoderType == EncoderType.Compute)
            {
                var computeCommandEncoder = GetOrCreateComputeEncoder();

                // TODO: Should there be a barrier on render targets?
                var scope = MTLBarrierScope.Buffers | MTLBarrierScope.Textures;
                computeCommandEncoder.MemoryBarrier(scope);
            }
            else
            {
                Logger.Warning?.Print(LogClass.Gpu, "Barrier called outside of a render or compute pass");
            }
        }

        public void ClearBuffer(BufferHandle destination, int offset, int size, uint value)
        {
            var blitCommandEncoder = GetOrCreateBlitEncoder();

            // Might need a closer look, range's count, lower, and upper bound
            // must be a multiple of 4
            MTLBuffer mtlBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref destination));
            blitCommandEncoder.FillBuffer(mtlBuffer,
                new NSRange
                {
                    location = (ulong)offset,
                    length = (ulong)size
                },
                (byte)value);
        }

        public void ClearRenderTargetColor(int index, int layer, int layerCount, uint componentMask, ColorF color)
        {
            float[] colors = [color.Red, color.Green, color.Blue, color.Alpha];

            _helperShader.ClearColor(index, colors);
        }

        public void ClearRenderTargetDepthStencil(int layer, int layerCount, float depthValue, bool depthMask, int stencilValue, int stencilMask)
        {
            _helperShader.ClearDepthStencil(depthValue, depthMask, stencilValue, stencilMask);
        }

        public void CommandBufferBarrier()
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void CopyBuffer(BufferHandle source, BufferHandle destination, int srcOffset, int dstOffset, int size)
        {
            var blitCommandEncoder = GetOrCreateBlitEncoder();

            MTLBuffer sourceBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref source));
            MTLBuffer destinationBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref destination));

            blitCommandEncoder.CopyFromBuffer(
                sourceBuffer,
                (ulong)srcOffset,
                destinationBuffer,
                (ulong)dstOffset,
                (ulong)size);
        }

        public void DispatchCompute(int groupsX, int groupsY, int groupsZ, int groupSizeX, int groupSizeY, int groupSizeZ)
        {
            var computeCommandEncoder = GetOrCreateComputeEncoder();

            computeCommandEncoder.DispatchThreadgroups(
                new MTLSize{width = (ulong)groupsX, height = (ulong)groupsY, depth = (ulong)groupsZ},
                new MTLSize{width = (ulong)groupSizeX, height = (ulong)groupSizeY, depth = (ulong)groupSizeZ});
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        {
            var renderCommandEncoder = GetOrCreateRenderEncoder();

            // TODO: Support topology re-indexing to provide support for TriangleFans
            var primitiveType = _encoderStateManager.Topology.Convert();

            renderCommandEncoder.DrawPrimitives(
                primitiveType,
                (ulong)firstVertex,
                (ulong)vertexCount,
                (ulong)instanceCount,
                (ulong)firstInstance);
        }

        public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int firstVertex, int firstInstance)
        {
            var renderCommandEncoder = GetOrCreateRenderEncoder();

            // TODO: Support topology re-indexing to provide support for TriangleFans
            var primitiveType = _encoderStateManager.Topology.Convert();

            renderCommandEncoder.DrawIndexedPrimitives(
                primitiveType,
                (ulong)indexCount,
                _encoderStateManager.IndexType,
                _encoderStateManager.IndexBuffer,
                _encoderStateManager.IndexBufferOffset,
                (ulong)instanceCount,
                firstVertex,
                (ulong)firstInstance);
        }

        public void DrawIndexedIndirect(BufferRange indirectBuffer)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndexedIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirect(BufferRange indirectBuffer)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawTexture(ITexture texture, ISampler sampler, Extents2DF srcRegion, Extents2DF dstRegion)
        {
            _helperShader.DrawTexture(texture, sampler, srcRegion, dstRegion);
        }

        public void SetAlphaTest(bool enable, float reference, CompareOp op)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetBlendState(AdvancedBlendDescriptor blend)
        {
            // Metal does not support advanced blend.
        }

        public void SetBlendState(int index, BlendDescriptor blend)
        {
            _encoderStateManager.UpdateBlendDescriptors(index, blend);
        }

        public void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetDepthClamp(bool clamp)
        {
            _encoderStateManager.UpdateDepthClamp(clamp);
        }

        public void SetDepthMode(DepthMode mode)
        {
            // Metal does not support depth clip control.
        }

        public void SetDepthTest(DepthTestDescriptor depthTest)
        {
            _encoderStateManager.UpdateDepthState(depthTest);
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            _encoderStateManager.UpdateCullMode(enable, face);
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            _encoderStateManager.UpdateFrontFace(frontFace);
        }

        public void SetIndexBuffer(BufferRange buffer, IndexType type)
        {
            _encoderStateManager.UpdateIndexBuffer(buffer, type);
        }

        public void SetImage(ShaderStage stage, int binding, ITexture texture, Format imageFormat)
        {
            if (texture is TextureBase tex)
            {
                _encoderStateManager.UpdateTexture(stage, (ulong)binding, tex);
            }
        }

        public void SetImageArray(ShaderStage stage, int binding, IImageArray array)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetLineParameters(float width, bool smooth)
        {
            // Metal does not support wide-lines.
        }

        public void SetLogicOpState(bool enable, LogicalOp op)
        {
            // Metal does not support logic operations.
        }

        public void SetMultisampleState(MultisampleDescriptor multisample)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPatchParameters(int vertices, ReadOnlySpan<float> defaultOuterLevel, ReadOnlySpan<float> defaultInnerLevel)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPointParameters(float size, bool isProgramPointSize, bool enablePointSprite, Origin origin)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPolygonMode(PolygonMode frontMode, PolygonMode backMode)
        {
            // Metal does not support polygon mode.
        }

        public void SetPrimitiveRestart(bool enable, int index)
        {
            // TODO: Supported for LineStrip and TriangleStrip
            // https://github.com/gpuweb/gpuweb/issues/1220#issuecomment-732483263
            // https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515520-drawindexedprimitives
            // https://stackoverflow.com/questions/70813665/how-to-render-multiple-trianglestrips-using-metal
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            _encoderStateManager.UpdatePrimitiveTopology(topology);
        }

        public void SetProgram(IProgram program)
        {
            _encoderStateManager.UpdateProgram(program);
        }

        public void SetRasterizerDiscard(bool discard)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            _encoderStateManager.UpdateRenderTargetColorMasks(componentMask);
        }

        public void SetRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _encoderStateManager.UpdateRenderTargets(colors, depthStencil);
        }

        public void SetScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            _encoderStateManager.UpdateScissors(regions);
        }

        public void SetStencilTest(StencilTestDescriptor stencilTest)
        {
            _encoderStateManager.UpdateStencilState(stencilTest);
        }

        public void SetUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _encoderStateManager.UpdateUniformBuffers(buffers);
        }

        public void SetStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _encoderStateManager.UpdateStorageBuffers(buffers);
        }

        public void SetTextureAndSampler(ShaderStage stage, int binding, ITexture texture, ISampler sampler)
        {
            if (texture is TextureBase tex)
            {
                if (sampler is Sampler samp)
                {
                    var mtlSampler = samp.GetSampler();
                    var index = (ulong)binding;

                    _encoderStateManager.UpdateTextureAndSampler(stage, index, tex, mtlSampler);
                }
            }
        }

        public void SetTextureArray(ShaderStage stage, int binding, ITextureArray array)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetUserClipDistance(int index, bool enableClip)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            _encoderStateManager.UpdateVertexAttribs(vertexAttribs);
        }

        public void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            _encoderStateManager.UpdateVertexBuffers(vertexBuffers);
        }

        public void SetViewports(ReadOnlySpan<Viewport> viewports)
        {
            _encoderStateManager.UpdateViewports(viewports);
        }

        public void TextureBarrier()
        {
            var renderCommandEncoder = GetOrCreateRenderEncoder();

            renderCommandEncoder.MemoryBarrier(MTLBarrierScope.Textures, MTLRenderStages.RenderStageFragment, MTLRenderStages.RenderStageFragment);
        }

        public void TextureBarrierTiled()
        {
            TextureBarrier();
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ulong compare, bool isEqual)
        {
            // TODO: Implementable via indirect draw commands
            return false;
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ICounterEvent compare, bool isEqual)
        {
            // TODO: Implementable via indirect draw commands
            return false;
        }

        public void EndHostConditionalRendering()
        {
            // TODO: Implementable via indirect draw commands
        }

        public void BeginTransformFeedback(PrimitiveTopology topology)
        {
            // Metal does not support transform feedback.
        }

        public void EndTransformFeedback()
        {
            // Metal does not support transform feedback.
        }

        public void SetTransformFeedbackBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            // Metal does not support transform feedback.
        }

        public void Dispose()
        {
            EndCurrentPass();
            _encoderStateManager.Dispose();
        }
    }
}
