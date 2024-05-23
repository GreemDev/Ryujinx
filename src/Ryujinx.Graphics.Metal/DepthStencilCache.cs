using SharpMetal.Metal;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public struct DepthStencilHash
    {
        public struct StencilHash
        {
            public MTLStencilOperation StencilFailureOperation;
            public MTLStencilOperation DepthFailureOperation;
            public MTLStencilOperation DepthStencilPassOperation;
            public MTLCompareFunction StencilCompareFunction;
            public uint ReadMask;
            public uint WriteMask;
        }
        public StencilHash FrontFace;
        public StencilHash BackFace;
        public MTLCompareFunction DepthCompareFunction;
        public bool DepthWriteEnabled;
    }

    [SupportedOSPlatform("macos")]
    public class DepthStencilCache : StateCache<MTLDepthStencilState, MTLDepthStencilDescriptor, DepthStencilHash>
    {
        private readonly MTLDevice _device;

        public DepthStencilCache(MTLDevice device)
        {
            _device = device;
        }

        protected override DepthStencilHash GetHash(MTLDepthStencilDescriptor descriptor)
        {
            var hash = new DepthStencilHash
            {
                // Front face
                FrontFace = new DepthStencilHash.StencilHash
                {
                    StencilFailureOperation = descriptor.FrontFaceStencil.StencilFailureOperation,
                    DepthFailureOperation = descriptor.FrontFaceStencil.DepthFailureOperation,
                    DepthStencilPassOperation = descriptor.FrontFaceStencil.DepthStencilPassOperation,
                    StencilCompareFunction = descriptor.FrontFaceStencil.StencilCompareFunction,
                    ReadMask = descriptor.FrontFaceStencil.ReadMask,
                    WriteMask = descriptor.FrontFaceStencil.WriteMask
                },
                // Back face
                BackFace = new DepthStencilHash.StencilHash
                {
                    StencilFailureOperation = descriptor.BackFaceStencil.StencilFailureOperation,
                    DepthFailureOperation = descriptor.BackFaceStencil.DepthFailureOperation,
                    DepthStencilPassOperation = descriptor.BackFaceStencil.DepthStencilPassOperation,
                    StencilCompareFunction = descriptor.BackFaceStencil.StencilCompareFunction,
                    ReadMask = descriptor.BackFaceStencil.ReadMask,
                    WriteMask = descriptor.BackFaceStencil.WriteMask
                },
                // Depth
                DepthCompareFunction = descriptor.DepthCompareFunction,
                DepthWriteEnabled = descriptor.DepthWriteEnabled
            };

            return hash;
        }

        protected override MTLDepthStencilState CreateValue(MTLDepthStencilDescriptor descriptor)
        {
            return _device.NewDepthStencilState(descriptor);
        }
    }
}
