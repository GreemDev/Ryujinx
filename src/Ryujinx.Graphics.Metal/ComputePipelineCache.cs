using Ryujinx.Common.Logging;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class ComputePipelineCache : StateCache<MTLComputePipelineState, MTLFunction, MTLFunction>
    {
        private readonly MTLDevice _device;

        public ComputePipelineCache(MTLDevice device)
        {
            _device = device;
        }

        protected override MTLFunction GetHash(MTLFunction function)
        {
            return function;
        }

        protected override MTLComputePipelineState CreateValue(MTLFunction function)
        {
            var error = new NSError(IntPtr.Zero);
            var pipelineState = _device.NewComputePipelineState(function, ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Compute Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
            }

            return pipelineState;
        }
    }
}
