using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Metal.State;
using SharpMetal.Metal;
using System;
using System.Linq;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [Flags]
    enum DirtyFlags
    {
        None = 0,
        RenderPipeline = 1 << 0,
        ComputePipeline = 1 << 1,
        DepthStencil = 1 << 2,
        DepthClamp = 1 << 3,
        DepthBias = 1 << 4,
        CullMode = 1 << 5,
        FrontFace = 1 << 6,
        StencilRef = 1 << 7,
        Viewports = 1 << 8,
        Scissors = 1 << 9,
        VertexBuffers = 1 << 10,
        Buffers = 1 << 11,
        VertexTextures = 1 << 12,
        FragmentTextures = 1 << 13,
        ComputeTextures = 1 << 14,

        RenderAll = RenderPipeline | DepthStencil | DepthClamp | DepthBias | CullMode | FrontFace | StencilRef | Viewports | Scissors | VertexBuffers | Buffers | VertexTextures | FragmentTextures,
        ComputeAll = ComputePipeline | Buffers | ComputeTextures,
        All = RenderAll | ComputeAll,
    }

    record struct BufferRef
    {
        public Auto<DisposableBuffer> Buffer;
        public BufferRange? Range;

        public BufferRef(Auto<DisposableBuffer> buffer)
        {
            Buffer = buffer;
        }

        public BufferRef(Auto<DisposableBuffer> buffer, ref BufferRange range)
        {
            Buffer = buffer;
            Range = range;
        }
    }

    struct PredrawState
    {
        public MTLCullMode CullMode;
        public DepthStencilUid DepthStencilUid;
        public PrimitiveTopology Topology;
        public MTLViewport[] Viewports;
    }

    struct RenderTargetCopy
    {
        public MTLScissorRect[] Scissors;
        public Texture DepthStencil;
        public Texture[] RenderTargets;
    }

    [SupportedOSPlatform("macos")]
    class EncoderState
    {
        public Program RenderProgram = null;
        public Program ComputeProgram = null;

        public PipelineState Pipeline;
        public DepthStencilUid DepthStencilUid;

        public TextureBase[] FragmentTextures = new TextureBase[Constants.MaxTexturesPerStage];
        public MTLSamplerState[] FragmentSamplers = new MTLSamplerState[Constants.MaxTexturesPerStage];

        public TextureBase[] VertexTextures = new TextureBase[Constants.MaxTexturesPerStage];
        public MTLSamplerState[] VertexSamplers = new MTLSamplerState[Constants.MaxTexturesPerStage];

        public TextureBase[] ComputeTextures = new TextureBase[Constants.MaxTexturesPerStage];
        public MTLSamplerState[] ComputeSamplers = new MTLSamplerState[Constants.MaxTexturesPerStage];

        public BufferRef[] UniformBuffers = new BufferRef[Constants.MaxUniformBuffersPerStage];
        public BufferRef[] StorageBuffers = new BufferRef[Constants.MaxStorageBuffersPerStage];

        public Auto<DisposableBuffer> IndexBuffer = default;
        public MTLIndexType IndexType = MTLIndexType.UInt16;
        public ulong IndexBufferOffset = 0;

        public MTLDepthClipMode DepthClipMode = MTLDepthClipMode.Clip;

        public float DepthBias;
        public float SlopeScale;
        public float Clamp;

        public int BackRefValue = 0;
        public int FrontRefValue = 0;

        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.CounterClockwise;
        public bool CullBoth = false;

        public MTLViewport[] Viewports = [];
        public MTLScissorRect[] Scissors = [];

        // Changes to attachments take recreation!
        public Texture DepthStencil = default;
        public Texture[] RenderTargets = new Texture[Constants.MaxColorAttachments];
        public ITexture PreMaskDepthStencil = default;
        public ITexture[] PreMaskRenderTargets;
        public bool FramebufferUsingColorWriteMask;

        public Array8<ColorBlendStateUid> StoredBlend;
        public ColorF BlendColor = new();

        public VertexBufferDescriptor[] VertexBuffers = [];
        public VertexAttribDescriptor[] VertexAttribs = [];

        // Dirty flags
        public DirtyFlags Dirty = DirtyFlags.None;

        // Only to be used for present
        public bool ClearLoadAction = false;

        public EncoderState()
        {
            Pipeline.Initialize();
            DepthStencilUid.DepthCompareFunction = MTLCompareFunction.Always;
        }

        public RenderTargetCopy InheritForClear(EncoderState other, bool depth, int singleIndex = -1)
        {
            // Inherit render target related information without causing a render encoder split.

            var oldState = new RenderTargetCopy
            {
                Scissors = other.Scissors,
                RenderTargets = other.RenderTargets,
                DepthStencil = other.DepthStencil
            };

            Scissors = other.Scissors;
            RenderTargets = other.RenderTargets;
            DepthStencil = other.DepthStencil;

            Pipeline.ColorBlendAttachmentStateCount = other.Pipeline.ColorBlendAttachmentStateCount;
            Pipeline.Internal.ColorBlendState = other.Pipeline.Internal.ColorBlendState;
            Pipeline.DepthStencilFormat = other.Pipeline.DepthStencilFormat;

            ref var blendStates = ref Pipeline.Internal.ColorBlendState;

            // Mask out irrelevant attachments.
            for (int i = 0; i < blendStates.Length; i++)
            {
                if (depth || (singleIndex != -1 && singleIndex != i))
                {
                    blendStates[i].WriteMask = MTLColorWriteMask.None;
                }
            }

            return oldState;
        }

        public void Restore(RenderTargetCopy copy)
        {
            Scissors = copy.Scissors;
            RenderTargets = copy.RenderTargets;
            DepthStencil = copy.DepthStencil;

            Pipeline.Internal.ResetColorState();
        }
    }
}
