using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Program : IProgram
    {
        private readonly ProgramLinkStatus _status;
        public MTLFunction VertexFunction;
        public MTLFunction FragmentFunction;
        public MTLFunction ComputeFunction;
        public ComputeSize ComputeLocalSize { get; }

        private HashTableSlim<PipelineUid, MTLRenderPipelineState> _graphicsPipelineCache;
        private MTLComputePipelineState? _computePipelineCache;
        private bool _firstBackgroundUse;

        public Program(ShaderSource[] shaders, MTLDevice device, ComputeSize computeLocalSize = default)
        {
            ComputeLocalSize = computeLocalSize;

            for (int index = 0; index < shaders.Length; index++)
            {
                ShaderSource shader = shaders[index];

                var libraryError = new NSError(IntPtr.Zero);
                var shaderLibrary = device.NewLibrary(StringHelper.NSString(shader.Code), new MTLCompileOptions(IntPtr.Zero), ref libraryError);
                if (libraryError != IntPtr.Zero)
                {
                    Logger.Warning?.PrintMsg(LogClass.Gpu, shader.Code);
                    Logger.Warning?.Print(LogClass.Gpu, $"{shader.Stage} shader linking failed: \n{StringHelper.String(libraryError.LocalizedDescription)}");
                    _status = ProgramLinkStatus.Failure;
                    return;
                }

                switch (shaders[index].Stage)
                {
                    case ShaderStage.Compute:
                        ComputeFunction = shaderLibrary.NewFunction(StringHelper.NSString("kernelMain"));
                        break;
                    case ShaderStage.Vertex:
                        VertexFunction = shaderLibrary.NewFunction(StringHelper.NSString("vertexMain"));
                        break;
                    case ShaderStage.Fragment:
                        FragmentFunction = shaderLibrary.NewFunction(StringHelper.NSString("fragmentMain"));
                        break;
                    default:
                        Logger.Warning?.Print(LogClass.Gpu, $"Cannot handle stage {shaders[index].Stage}!");
                        break;
                }
            }

            _status = ProgramLinkStatus.Success;
        }

        public ProgramLinkStatus CheckProgramLink(bool blocking)
        {
            return _status;
        }

        public byte[] GetBinary()
        {
            return ""u8.ToArray();
        }

        public void AddGraphicsPipeline(ref PipelineUid key, MTLRenderPipelineState pipeline)
        {
            (_graphicsPipelineCache ??= new()).Add(ref key, pipeline);
        }

        public void AddComputePipeline(MTLComputePipelineState pipeline)
        {
            _computePipelineCache = pipeline;
        }

        public bool TryGetGraphicsPipeline(ref PipelineUid key, out MTLRenderPipelineState pipeline)
        {
            if (_graphicsPipelineCache == null)
            {
                pipeline = default;
                return false;
            }

            if (!_graphicsPipelineCache.TryGetValue(ref key, out pipeline))
            {
                if (_firstBackgroundUse)
                {
                    Logger.Warning?.Print(LogClass.Gpu, "Background pipeline compile missed on draw - incorrect pipeline state?");
                    _firstBackgroundUse = false;
                }

                return false;
            }

            _firstBackgroundUse = false;

            return true;
        }

        public bool TryGetComputePipeline(out MTLComputePipelineState pipeline)
        {
            if (_computePipelineCache.HasValue)
            {
                pipeline = _computePipelineCache.Value;
                return true;
            }

            pipeline = default;
            return false;
        }

        public void Dispose()
        {
            if (_graphicsPipelineCache != null)
            {
                foreach (MTLRenderPipelineState pipeline in _graphicsPipelineCache.Values)
                {
                    pipeline.Dispose();
                }
            }

            _computePipelineCache?.Dispose();

            VertexFunction.Dispose();
            FragmentFunction.Dispose();
            ComputeFunction.Dispose();
        }
    }
}
