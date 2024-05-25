using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class HelperShader : IDisposable
    {
        private const string ShadersSourcePath = "/Ryujinx.Graphics.Metal/Shaders";
        private readonly Pipeline _pipeline;
        private MTLDevice _device;

        private readonly IProgram _programColorBlit;
        private readonly List<IProgram> _programsColorClear = new();
        private readonly IProgram _programDepthStencilClear;

        public HelperShader(MTLDevice device, Pipeline pipeline)
        {
            _device = device;
            _pipeline = pipeline;

            var blitSource = ReadMsl("Blit.metal");
            _programColorBlit = new Program(
            [
                new ShaderSource(blitSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSource, ShaderStage.Vertex, TargetLanguage.Msl)
            ], device);

            var colorClearSource = ReadMsl("ColorClear.metal");
            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                var crntSource = colorClearSource.Replace("COLOR_ATTACHMENT_INDEX", i.ToString());
                _programsColorClear.Add(new Program(
                [
                    new ShaderSource(crntSource, ShaderStage.Fragment, TargetLanguage.Msl),
                    new ShaderSource(crntSource, ShaderStage.Vertex, TargetLanguage.Msl)
                ], device));
            }

            var depthStencilClearSource = ReadMsl("DepthStencilClear.metal");
            _programDepthStencilClear = new Program(
            [
                new ShaderSource(depthStencilClearSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(depthStencilClearSource, ShaderStage.Vertex, TargetLanguage.Msl)
            ], device);
        }

        private static string ReadMsl(string fileName)
        {
            return EmbeddedResources.ReadAllText(string.Join('/', ShadersSourcePath, fileName));
        }

        public void BlitColor(
            ITexture source,
            ITexture destination)
        {
            var sampler = _device.NewSamplerState(new MTLSamplerDescriptor
            {
                MinFilter = MTLSamplerMinMagFilter.Nearest,
                MagFilter = MTLSamplerMinMagFilter.Nearest,
                MipFilter = MTLSamplerMipFilter.NotMipmapped
            });

            // Save current state
            _pipeline.SaveState();

            _pipeline.SetProgram(_programColorBlit);
            // Viewport and scissor needs to be set before render pass begin so as not to bind the old ones
            _pipeline.SetViewports([]);
            _pipeline.SetScissors([]);
            _pipeline.SetRenderTargets([destination], null);
            _pipeline.SetTextureAndSampler(ShaderStage.Fragment, 0, source, new Sampler(sampler));
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.Triangles);
            _pipeline.Draw(6, 1, 0, 0);

            // Restore previous state
            _pipeline.RestoreState();
        }

        public unsafe void ClearColor(
            int index,
            ReadOnlySpan<float> clearColor)
        {
            const int ClearColorBufferSize = 16;

            var buffer = _device.NewBuffer(ClearColorBufferSize, MTLResourceOptions.ResourceStorageModeManaged);
            var span = new Span<float>(buffer.Contents.ToPointer(), ClearColorBufferSize);
            clearColor.CopyTo(span);

            buffer.DidModifyRange(new NSRange
            {
                location = 0,
                length = ClearColorBufferSize
            });

            var handle = buffer.NativePtr;
            var range = new BufferRange(Unsafe.As<IntPtr, BufferHandle>(ref handle), 0, ClearColorBufferSize);

            // Save current state
            _pipeline.SaveState();

            _pipeline.SetUniformBuffers([new BufferAssignment(0, range)]);

            _pipeline.SetProgram(_programsColorClear[index]);
            // _pipeline.SetRenderTargetColorMasks([componentMask]);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);

            // Restore previous state
            _pipeline.RestoreState();
        }

        public unsafe void ClearDepthStencil(
            ReadOnlySpan<float> depthValue,
            bool depthMask,
            int stencilValue,
            int stencilMask)
        {
            const int ClearColorBufferSize = 16;

            var buffer = _device.NewBuffer(ClearColorBufferSize, MTLResourceOptions.ResourceStorageModeManaged);
            var span = new Span<float>(buffer.Contents.ToPointer(), ClearColorBufferSize);
            depthValue.CopyTo(span);

            buffer.DidModifyRange(new NSRange
            {
                location = 0,
                length = ClearColorBufferSize
            });

            var handle = buffer.NativePtr;
            var range = new BufferRange(Unsafe.As<IntPtr, BufferHandle>(ref handle), 0, ClearColorBufferSize);

            // Save current state
            _pipeline.SaveState();

            _pipeline.SetUniformBuffers([new BufferAssignment(0, range)]);

            _pipeline.SetProgram(_programDepthStencilClear);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.SetDepthTest(new DepthTestDescriptor(true, depthMask, CompareOp.Always));
            // _pipeline.SetStencilTest(CreateStencilTestDescriptor(stencilMask != 0, stencilValue, 0xFF, stencilMask));
            _pipeline.Draw(4, 1, 0, 0);

            // Restore previous state
            _pipeline.RestoreState();
        }

        private static StencilTestDescriptor CreateStencilTestDescriptor(
            bool enabled,
            int refValue = 0,
            int compareMask = 0xff,
            int writeMask = 0xff)
        {
            return new StencilTestDescriptor(
                enabled,
                CompareOp.Always,
                StencilOp.Replace,
                StencilOp.Replace,
                StencilOp.Replace,
                refValue,
                compareMask,
                writeMask,
                CompareOp.Always,
                StencilOp.Replace,
                StencilOp.Replace,
                StencilOp.Replace,
                refValue,
                compareMask,
                writeMask);
        }

        public void Dispose()
        {
            _programColorBlit.Dispose();
            foreach (var programColorClear in _programsColorClear)
            {
                programColorClear.Dispose();
            }
            _programDepthStencilClear.Dispose();
            _pipeline.Dispose();
        }
    }
}
