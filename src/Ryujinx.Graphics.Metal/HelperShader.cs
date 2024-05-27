using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Metal;
using System;
using System.Collections.Generic;
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
            _pipeline.SaveAndResetState();

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

            // Save current state
            _pipeline.SaveState();

            _pipeline.SetProgram(_programsColorClear[index]);
            _pipeline.SetBlendState(index, new BlendDescriptor(false, new ColorF(0f, 0f, 0f, 1f), BlendOp.Add, BlendFactor.One, BlendFactor.Zero, BlendOp.Add, BlendFactor.One, BlendFactor.Zero));
            _pipeline.SetFaceCulling(false, Face.Front);
            _pipeline.SetDepthTest(new DepthTestDescriptor(false, false, CompareOp.Always));
            // _pipeline.SetRenderTargetColorMasks([componentMask]);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);

            fixed (float* ptr = clearColor)
            {
                _pipeline.GetOrCreateRenderEncoder().SetFragmentBytes((IntPtr)ptr, ClearColorBufferSize, 0);
            }

            _pipeline.Draw(4, 1, 0, 0);

            // Restore previous state
            _pipeline.RestoreState();
        }

        public unsafe void ClearDepthStencil(
            float depthValue,
            bool depthMask,
            int stencilValue,
            int stencilMask)
        {
            const int ClearDepthBufferSize = 4;

            IntPtr ptr = new(&depthValue);

            // Save current state
            _pipeline.SaveState();

            _pipeline.SetProgram(_programDepthStencilClear);
            _pipeline.SetFaceCulling(false, Face.Front);
            _pipeline.SetDepthTest(new DepthTestDescriptor(false, false, CompareOp.Always));
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.SetDepthTest(new DepthTestDescriptor(true, depthMask, CompareOp.Always));
            // _pipeline.SetStencilTest(CreateStencilTestDescriptor(stencilMask != 0, stencilValue, 0xFF, stencilMask));
            _pipeline.GetOrCreateRenderEncoder().SetFragmentBytes(ptr, ClearDepthBufferSize, 0);
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
