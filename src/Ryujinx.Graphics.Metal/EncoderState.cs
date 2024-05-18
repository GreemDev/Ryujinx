using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public struct EncoderState
    {
        public MTLFunction? VertexFunction = null;
        public MTLFunction? FragmentFunction = null;

        public Dictionary<ulong, MTLTexture> FragmentTextures = new();
        public Dictionary<ulong, MTLSamplerState> FragmentSamplers = new();

        public Dictionary<ulong, MTLTexture> VertexTextures = new();
        public Dictionary<ulong, MTLSamplerState> VertexSamplers = new();

        public List<BufferInfo> VertexBuffers = [];
        public List<BufferInfo> UniformBuffers = [];
        public List<BufferInfo> StorageBuffers = [];

        public MTLBuffer IndexBuffer = default;
        public MTLIndexType IndexType = MTLIndexType.UInt16;
        public ulong IndexBufferOffset = 0;

        public MTLDepthStencilState? DepthStencilState = null;

        public MTLCompareFunction DepthCompareFunction = MTLCompareFunction.Always;
        public bool DepthWriteEnabled = false;

        public MTLStencilDescriptor BackFaceStencil = new();
        public MTLStencilDescriptor FrontFaceStencil = new();

        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.Clockwise;

        public MTLViewport[] Viewports = [];
        public MTLScissorRect[] Scissors = [];

        // Changes to attachments take recreation!
        public MTLTexture DepthStencil = default;
        public MTLTexture[] RenderTargets = [];
        public MTLVertexDescriptor VertexDescriptor = new();

        public EncoderState() { }
    }
}
