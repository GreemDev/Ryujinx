using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Shader;

namespace Ryujinx.Graphics.Gpu.Engine.Threed
{
    /// <summary>
    /// Draw state.
    /// </summary>
    class DrawState
    {
        /// <summary>
        /// First index to be used for the draw on the index buffer.
        /// </summary>
        public int FirstIndex;

        /// <summary>
        /// Number of indices to be used for the draw on the index buffer.
        /// </summary>
        public int IndexCount;

        /// <summary>
        /// First vertex used on non-indexed draws. This value is stored somewhere else on indexed draws.
        /// </summary>
        public int DrawFirstVertex;

        /// <summary>
        /// Vertex count used on non-indexed draws. Indexed draws have a index count instead.
        /// </summary>
        public int DrawVertexCount;

        /// <summary>
        /// Indicates if the next draw will be a indexed draw.
        /// </summary>
        public bool DrawIndexed;

        /// <summary>
        /// Indicates if the next draw will be a indirect draw.
        /// </summary>
        public bool DrawIndirect;

        /// <summary>
        /// Indicates that the draw is using the draw parameters on the 3D engine state, rather than inline parameters submitted with the draw command.
        /// </summary>
        public bool DrawUsesEngineState;

        /// <summary>
        /// Indicates if any of the currently used vertex shaders reads the instance ID.
        /// </summary>
        public bool VsUsesInstanceId;

        /// <summary>
        /// Indicates if any of the currently used vertex buffers is instanced.
        /// </summary>
        public bool IsAnyVbInstanced;

        /// <summary>
        /// Primitive topology for the next draw.
        /// </summary>
        public PrimitiveTopology Topology;

        /// <summary>
        /// Index buffer data streamer for inline index buffer updates, such as those used in legacy OpenGL.
        /// </summary>
        public IbStreamer IbStreamer;

        /// <summary>
        /// If the vertex shader is emulated on compute, this should be set to the compute program, otherwise it should be null.
        /// </summary>
        public ShaderAsCompute VertexAsCompute;

        /// <summary>
        /// If a geometry shader exists and is emulated on compute, this should be set to the compute program, otherwise it should be null.
        /// </summary>
        public ShaderAsCompute GeometryAsCompute;

        /// <summary>
        /// If the vertex shader is emulated on compute, this should be set to the passthrough vertex program, otherwise it should be null.
        /// </summary>
        public IProgram VertexPassthrough;
    }
}
