using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Metal;
using System;
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
        private readonly IProgram _programColorClearF;
        private readonly IProgram _programColorClearSI;
        private readonly IProgram _programColorClearUI;
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

            // var colorClearFSource = ReadMsl("ColorClearF.metal");
            // _programColorClearF = new Program(
            // [
            //     new ShaderSource(colorClearFSource, ShaderStage.Fragment, TargetLanguage.Msl),
            //     new ShaderSource(colorClearFSource, ShaderStage.Vertex, TargetLanguage.Msl)
            // ], device);
            //
            // var colorClearSiSource = ReadMsl("ColorClearSI.metal");
            // _programColorClearSI = new Program(
            // [
            //     new ShaderSource(colorClearSiSource, ShaderStage.Fragment, TargetLanguage.Msl),
            //     new ShaderSource(colorClearSiSource, ShaderStage.Vertex, TargetLanguage.Msl)
            // ], device);
            //
            // var colorClearUiSource = ReadMsl("ColorClearUI.metal");
            // _programColorClearUI = new Program(
            // [
            //     new ShaderSource(colorClearUiSource, ShaderStage.Fragment, TargetLanguage.Msl),
            //     new ShaderSource(colorClearUiSource, ShaderStage.Vertex, TargetLanguage.Msl)
            // ], device);
            //
            // var depthStencilClearSource = ReadMsl("DepthStencilClear.metal");
            // _programDepthStencilClear = new Program(
            // [
            //     new ShaderSource(depthStencilClearSource, ShaderStage.Fragment, TargetLanguage.Msl),
            //     new ShaderSource(depthStencilClearSource, ShaderStage.Vertex, TargetLanguage.Msl)
            // ], device);
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

        public void ClearColor(
            Texture dst,
            uint componentMask,
            int dstWidth,
            int dstHeight,
            ComponentType type,
            Rectangle<int> scissor)
        {
            Span<Viewport> viewports = stackalloc Viewport[1];

            viewports[0] = new Viewport(
                new Rectangle<float>(0, 0, dstWidth, dstHeight),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            IProgram program;

            if (type == ComponentType.SignedInteger)
            {
                program = _programColorClearSI;
            }
            else if (type == ComponentType.UnsignedInteger)
            {
                program = _programColorClearUI;
            }
            else
            {
                program = _programColorClearF;
            }

            _pipeline.SetProgram(program);
            // _pipeline.SetRenderTarget(dst, (uint)dstWidth, (uint)dstHeight);
            _pipeline.SetRenderTargetColorMasks([componentMask]);
            _pipeline.SetViewports(viewports);
            _pipeline.SetScissors([scissor]);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);
            _pipeline.Finish();
        }

        public void ClearDepthStencil(
            Texture dst,
            float depthValue,
            bool depthMask,
            int stencilValue,
            int stencilMask,
            int dstWidth,
            int dstHeight,
            Rectangle<int> scissor)
        {
            Span<Viewport> viewports = stackalloc Viewport[1];

            viewports[0] = new Viewport(
                new Rectangle<float>(0, 0, dstWidth, dstHeight),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            _pipeline.SetProgram(_programDepthStencilClear);
            // _pipeline.SetRenderTarget(dst, (uint)dstWidth, (uint)dstHeight);
            _pipeline.SetViewports(viewports);
            _pipeline.SetScissors([scissor]);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.SetDepthTest(new DepthTestDescriptor(true, depthMask, CompareOp.Always));
            _pipeline.SetStencilTest(CreateStencilTestDescriptor(stencilMask != 0, stencilValue, 0xFF, stencilMask));
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
            _programColorClearF.Dispose();
            _programColorClearSI.Dispose();
            _programColorClearUI.Dispose();
            _programDepthStencilClear.Dispose();
            _pipeline.Dispose();
        }
    }
}
