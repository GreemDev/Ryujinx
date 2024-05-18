using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct EncoderStateManager
    {
        private readonly MTLDevice _device;
        private Pipeline _pipeline;

        private EncoderState _currentState = new();
        private EncoderState _backState = new();

        // Public accessors
        public MTLBuffer IndexBuffer => _currentState.IndexBuffer;
        public MTLIndexType IndexType => _currentState.IndexType;
        public ulong IndexBufferOffset => _currentState.IndexBufferOffset;
        public PrimitiveTopology Topology => _currentState.Topology;

        public EncoderStateManager(MTLDevice device, Pipeline pipeline)
        {
            _device = device;
            _pipeline = pipeline;
        }

        public void SwapStates()
        {
            (_currentState, _backState) = (_backState, _currentState);

            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        public MTLRenderCommandEncoder CreateRenderCommandEncoder()
        {
            // Initialise Pass & State

            var renderPassDescriptor = new MTLRenderPassDescriptor();
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor();

            const int MaxColorAttachments = 8;
            for (int i = 0; i < MaxColorAttachments; i++)
            {
                if (_currentState.RenderTargets[i] != IntPtr.Zero)
                {
                    var passAttachment = renderPassDescriptor.ColorAttachments.Object((ulong)i);
                    passAttachment.Texture = _currentState.RenderTargets[i];
                    passAttachment.LoadAction = MTLLoadAction.Load;

                    var pipelineAttachment = renderPipelineDescriptor.ColorAttachments.Object((ulong)i);
                    pipelineAttachment.SetBlendingEnabled(true);
                    pipelineAttachment.PixelFormat = _currentState.RenderTargets[i].PixelFormat;
                    pipelineAttachment.SourceAlphaBlendFactor = MTLBlendFactor.SourceAlpha;
                    pipelineAttachment.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
                    pipelineAttachment.SourceRGBBlendFactor = MTLBlendFactor.SourceAlpha;
                    pipelineAttachment.DestinationRGBBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
                }
            }

            var depthAttachment = renderPassDescriptor.DepthAttachment;
            depthAttachment.Texture = _currentState.DepthStencil;
            depthAttachment.LoadAction = MTLLoadAction.Load;

            // var stencilAttachment = renderPassDescriptor.StencilAttachment;
            // stencilAttachment.Texture =
            // stencilAttachment.LoadAction = MTLLoadAction.Load;

            renderPipelineDescriptor.DepthAttachmentPixelFormat = _currentState.DepthStencil.PixelFormat;
            // renderPipelineDescriptor.StencilAttachmentPixelFormat =

            renderPipelineDescriptor.VertexDescriptor = _currentState.VertexDescriptor;

            if (_currentState.VertexFunction != null)
            {
                renderPipelineDescriptor.VertexFunction = _currentState.VertexFunction.Value;
            }
            else
            {
                return new (IntPtr.Zero);
            }

            if (_currentState.FragmentFunction != null)
            {
                renderPipelineDescriptor.FragmentFunction = _currentState.FragmentFunction.Value;
            }

            var error = new NSError(IntPtr.Zero);
            var pipelineState = _device.NewRenderPipelineState(renderPipelineDescriptor, ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Render Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
            }

            // Initialise Encoder

            var renderCommandEncoder = _pipeline.CommandBuffer.RenderCommandEncoder(renderPassDescriptor);

            renderCommandEncoder.SetRenderPipelineState(pipelineState);

            SetDepthStencilState(renderCommandEncoder, _currentState.DepthStencilState);
            SetScissors(renderCommandEncoder, _currentState.Scissors);
            SetViewports(renderCommandEncoder, _currentState.Viewports);
            SetBuffers(renderCommandEncoder, _currentState.VertexBuffers);
            SetBuffers(renderCommandEncoder, _currentState.UniformBuffers, true);
            SetBuffers(renderCommandEncoder, _currentState.StorageBuffers, true);
            SetCullMode(renderCommandEncoder, _currentState.CullMode);
            SetFrontFace(renderCommandEncoder, _currentState.Winding);
            SetTextureAndSampler(renderCommandEncoder, ShaderStage.Vertex, _currentState.VertexTextures, _currentState.VertexSamplers);
            SetTextureAndSampler(renderCommandEncoder, ShaderStage.Fragment, _currentState.FragmentTextures, _currentState.FragmentSamplers);

            return renderCommandEncoder;
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

            // Requires recreating pipeline
            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        public void UpdateRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _currentState.RenderTargets = new MTLTexture[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] is not Texture tex)
                {
                    continue;
                }

                _currentState.RenderTargets[i] = tex.MTLTexture;
            }

            if (depthStencil is Texture depthTexture)
            {
                _currentState.DepthStencil = depthTexture.MTLTexture;
            }

            // Requires recreating pipeline
            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        public void UpdateVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            for (int i = 0; i < vertexAttribs.Length; i++)
            {
                if (!vertexAttribs[i].IsZero)
                {
                    // TODO: Format should not be hardcoded
                    var attrib = _currentState.VertexDescriptor.Attributes.Object((ulong)i);
                    attrib.Format = MTLVertexFormat.Float4;
                    attrib.BufferIndex = (ulong)vertexAttribs[i].BufferIndex;
                    attrib.Offset = (ulong)vertexAttribs[i].Offset;

                    var layout = _currentState.VertexDescriptor.Layouts.Object((ulong)vertexAttribs[i].BufferIndex);
                    layout.Stride = 1;
                }
            }

            // Requires recreating pipeline
            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        // Inlineable
        public void UpdateStencilState(StencilTestDescriptor stencilTest)
        {
            _currentState.BackFaceStencil = new MTLStencilDescriptor
            {
                StencilFailureOperation = stencilTest.BackSFail.Convert(),
                DepthFailureOperation = stencilTest.BackDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.BackDpPass.Convert(),
                StencilCompareFunction = stencilTest.BackFunc.Convert(),
                ReadMask = (uint)stencilTest.BackFuncMask,
                WriteMask = (uint)stencilTest.BackMask
            };

            _currentState.FrontFaceStencil = new MTLStencilDescriptor
            {
                StencilFailureOperation = stencilTest.FrontSFail.Convert(),
                DepthFailureOperation = stencilTest.FrontDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.FrontDpPass.Convert(),
                StencilCompareFunction = stencilTest.FrontFunc.Convert(),
                ReadMask = (uint)stencilTest.FrontFuncMask,
                WriteMask = (uint)stencilTest.FrontMask
            };

            _currentState.DepthStencilState = _device.NewDepthStencilState(new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _currentState.DepthCompareFunction,
                DepthWriteEnabled = _currentState.DepthWriteEnabled,
                BackFaceStencil = stencilTest.TestEnable ? _currentState.BackFaceStencil : new MTLStencilDescriptor(IntPtr.Zero),
                FrontFaceStencil = stencilTest.TestEnable ? _currentState.FrontFaceStencil : new MTLStencilDescriptor(IntPtr.Zero)
            });

            // Inline Update

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetDepthStencilState(renderCommandEncoder, _currentState.DepthStencilState);
            }
        }

        // Inlineable
        public void UpdateDepthState(DepthTestDescriptor depthTest)
        {
            _currentState.DepthCompareFunction = depthTest.TestEnable ? depthTest.Func.Convert() : MTLCompareFunction.Always;
            _currentState.DepthWriteEnabled = depthTest.WriteEnable;

            _currentState.DepthStencilState = _device.NewDepthStencilState(new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _currentState.DepthCompareFunction,
                DepthWriteEnabled = _currentState.DepthWriteEnabled,
                BackFaceStencil = _currentState.BackFaceStencil,
                FrontFaceStencil = _currentState.FrontFaceStencil
            });

            // Inline Update

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetDepthStencilState(renderCommandEncoder, _currentState.DepthStencilState);
            }
        }

        // Inlineable
        public void UpdateScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            int maxScissors = Math.Min(regions.Length, _currentState.Viewports.Length);

            if (maxScissors == 0)
            {
                return;
            }

            _currentState.Scissors = new MTLScissorRect[maxScissors];

            for (int i = 0; i < maxScissors; i++)
            {
                var region = regions[i];

                _currentState.Scissors[i] = new MTLScissorRect
                {
                    height = Math.Clamp((ulong)region.Height, 0, (ulong)_currentState.Viewports[i].height),
                    width = Math.Clamp((ulong)region.Width, 0, (ulong)_currentState.Viewports[i].width),
                    x = (ulong)region.X,
                    y = (ulong)region.Y
                };
            }

            // Inline Update

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetScissors(renderCommandEncoder, _currentState.Scissors);
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
                _currentState.Viewports[i] = new MTLViewport
                {
                    originX = viewport.Region.X,
                    originY = viewport.Region.Y,
                    width = viewport.Region.Width,
                    height = viewport.Region.Height,
                    znear = Clamp(viewport.DepthNear),
                    zfar = Clamp(viewport.DepthFar)
                };
            }

            // Inline Update

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetViewports(renderCommandEncoder, _currentState.Viewports);
            }
        }

        // Inlineable
        public void UpdateVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            _currentState.VertexBuffers = [];

            for (int i = 0; i < vertexBuffers.Length; i++)
            {
                if (vertexBuffers[i].Stride != 0)
                {
                    var layout = _currentState.VertexDescriptor.Layouts.Object((ulong)i);
                    layout.Stride = (ulong)vertexBuffers[i].Stride;

                    _currentState.VertexBuffers.Add(new BufferInfo
                    {
                        Handle = vertexBuffers[i].Buffer.Handle.ToIntPtr(),
                        Offset = vertexBuffers[i].Buffer.Offset,
                        Index = i
                    });
                }
            }

            // Inline Update

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetBuffers(renderCommandEncoder, _currentState.VertexBuffers);
            }
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

            // Inline Update

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

            // Inline Update

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

            // Inline Update

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetCullMode(renderCommandEncoder, _currentState.CullMode);
            }
        }

        // Inlineable
        public void UpdateFrontFace(FrontFace frontFace)
        {
            _currentState.Winding = frontFace.Convert();

            // Inline Update

            if (_pipeline.CurrentEncoderType == EncoderType.Render && _pipeline.CurrentEncoder != null)
            {
                var renderCommandEncoder = new MTLRenderCommandEncoder(_pipeline.CurrentEncoder.Value);
                SetFrontFace(renderCommandEncoder, _currentState.Winding);
            }
        }

        // Inlineable
        public void UpdateTextureAndSampler(ShaderStage stage, ulong binding, MTLTexture texture, MTLSamplerState sampler)
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
                SetTextureAndSampler(renderCommandEncoder, ShaderStage.Vertex, _currentState.VertexTextures, _currentState.VertexSamplers);
                SetTextureAndSampler(renderCommandEncoder, ShaderStage.Fragment, _currentState.FragmentTextures, _currentState.FragmentSamplers);
            }
        }

        private static void SetDepthStencilState(MTLRenderCommandEncoder renderCommandEncoder, MTLDepthStencilState? depthStencilState)
        {
            if (depthStencilState != null)
            {
                renderCommandEncoder.SetDepthStencilState(depthStencilState.Value);
            }
        }

        private unsafe static void SetScissors(MTLRenderCommandEncoder renderCommandEncoder, MTLScissorRect[] scissors)
        {
            if (scissors.Length > 0)
            {
                fixed (MTLScissorRect* pMtlScissors = scissors)
                {
                    renderCommandEncoder.SetScissorRects((IntPtr)pMtlScissors, (ulong)scissors.Length);
                }
            }
        }

        private unsafe static void SetViewports(MTLRenderCommandEncoder renderCommandEncoder, MTLViewport[] viewports)
        {
            if (viewports.Length > 0)
            {
                fixed (MTLViewport* pMtlViewports = viewports)
                {
                    renderCommandEncoder.SetViewports((IntPtr)pMtlViewports, (ulong)viewports.Length);
                }
            }
        }

        private static void SetBuffers(MTLRenderCommandEncoder renderCommandEncoder, List<BufferInfo> buffers, bool fragment = false)
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

        private static void SetCullMode(MTLRenderCommandEncoder renderCommandEncoder, MTLCullMode cullMode)
        {
            renderCommandEncoder.SetCullMode(cullMode);
        }

        private static void SetFrontFace(MTLRenderCommandEncoder renderCommandEncoder, MTLWinding winding)
        {
            renderCommandEncoder.SetFrontFacingWinding(winding);
        }

        private static void SetTextureAndSampler(MTLRenderCommandEncoder renderCommandEncoder, ShaderStage stage, Dictionary<ulong, MTLTexture> textures, Dictionary<ulong, MTLSamplerState> samplers)
        {
            foreach (var texture in textures)
            {
                switch (stage)
                {
                    case ShaderStage.Vertex:
                        renderCommandEncoder.SetVertexTexture(texture.Value, texture.Key);
                        break;
                    case ShaderStage.Fragment:
                        renderCommandEncoder.SetFragmentTexture(texture.Value, texture.Key);
                        break;
                }
            }

            foreach (var sampler in samplers)
            {
                switch (stage)
                {
                    case ShaderStage.Vertex:
                        renderCommandEncoder.SetVertexSamplerState(sampler.Value, sampler.Key);
                        break;
                    case ShaderStage.Fragment:
                        renderCommandEncoder.SetFragmentSamplerState(sampler.Value, sampler.Key);
                        break;
                }
            }
        }
    }
}
