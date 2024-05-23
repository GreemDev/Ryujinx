using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    enum ComponentType
    {
        Float,
        SignedInteger,
        UnsignedInteger,
    }

    [SupportedOSPlatform("macos")]
    class HelperShader : IDisposable
    {
        private const string ShadersSourcePath = "/Ryujinx.Graphics.Metal/Shaders";
        private readonly Pipeline _pipeline;
        private MTLDevice _device;

        private readonly IProgram _programColorBlit;
        private readonly IProgram _programColorClear;
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
            _programColorClear = new Program(
            [
                new ShaderSource(colorClearSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(colorClearSource, ShaderStage.Vertex, TargetLanguage.Msl)
            ], device);

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

            _pipeline.SetProgram(_programColorBlit);
            _pipeline.SetRenderTargets([destination], null);
            _pipeline.SetTextureAndSampler(ShaderStage.Fragment, 0, source, new Sampler(sampler));
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.Triangles);
            _pipeline.Draw(6, 1, 0, 0);
            _pipeline.Finish();
        }

        public unsafe void ClearColor(
            Texture dst,
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

            _pipeline.SetUniformBuffers([new BufferAssignment(0, range)]);

            _pipeline.SetProgram(_programColorClear);
            _pipeline.SetRenderTargets([dst], null);
            // _pipeline.SetRenderTargetColorMasks([componentMask]);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);
            _pipeline.Finish();
        }

        public unsafe void ClearDepthStencil(
            Texture dst,
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

            _pipeline.SetUniformBuffers([new BufferAssignment(0, range)]);

            _pipeline.SetProgram(_programDepthStencilClear);
            _pipeline.SetRenderTargets([], dst);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.SetDepthTest(new DepthTestDescriptor(true, depthMask, CompareOp.Always));
            // _pipeline.SetStencilTest(CreateStencilTestDescriptor(stencilMask != 0, stencilValue, 0xFF, stencilMask));
            _pipeline.Draw(4, 1, 0, 0);
            _pipeline.Finish();
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
            _programColorClear.Dispose();
            _programDepthStencilClear.Dispose();
            _pipeline.Dispose();
        }
    }
}
