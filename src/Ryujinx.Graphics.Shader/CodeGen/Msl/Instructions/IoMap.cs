using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;
using System.Globalization;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class IoMap
    {
        public static (string, AggregateType) GetMslBuiltIn(
            ShaderDefinitions definitions,
            IoVariable ioVariable,
            int location,
            int component,
            bool isOutput,
            bool isPerPatch)
        {
            var returnValue = ioVariable switch
            {
                IoVariable.BaseInstance => ("base_instance", AggregateType.S32),
                IoVariable.BaseVertex => ("base_vertex", AggregateType.S32),
                IoVariable.CtaId => ("threadgroup_position_in_grid", AggregateType.Vector3 | AggregateType.U32),
                IoVariable.ClipDistance => ("clip_distance", AggregateType.Array | AggregateType.FP32),
                IoVariable.FragmentOutputColor => ($"out.color{location}", AggregateType.Vector4 | AggregateType.FP32),
                IoVariable.FragmentOutputDepth => ("out.depth", AggregateType.FP32),
                IoVariable.FrontFacing => ("in.front_facing", AggregateType.Bool),
                IoVariable.GlobalId => ("thread_position_in_grid", AggregateType.Vector3 | AggregateType.U32),
                IoVariable.InstanceId => ("instance_id", AggregateType.S32),
                IoVariable.InvocationId => ("INVOCATION_ID", AggregateType.S32),
                IoVariable.PointCoord => ("point_coord", AggregateType.Vector2),
                IoVariable.PointSize => ("out.point_size", AggregateType.FP32),
                IoVariable.Position => ("out.position", AggregateType.Vector4 | AggregateType.FP32),
                IoVariable.PrimitiveId => ("primitive_id", AggregateType.S32),
                IoVariable.UserDefined => GetUserDefinedVariableName(definitions, location, component, isOutput, isPerPatch),
                IoVariable.ThreadId => ("thread_position_in_threadgroup", AggregateType.Vector3 | AggregateType.U32),
                IoVariable.VertexId => ("vertex_id", AggregateType.S32),
                // gl_VertexIndex does not have a direct equivalent in MSL
                IoVariable.VertexIndex => ("vertex_id", AggregateType.U32),
                IoVariable.ViewportIndex => ("viewport_array_index", AggregateType.S32),
                IoVariable.FragmentCoord => ("in.position", AggregateType.Vector4 | AggregateType.FP32),
                _ => (null, AggregateType.Invalid),
            };

            if (returnValue.Item2 == AggregateType.Invalid)
            {
                Logger.Warning?.PrintMsg(LogClass.Gpu, $"Unable to find type for IoVariable {ioVariable}!");
            }

            return returnValue;
        }

        private static (string, AggregateType) GetUserDefinedVariableName(ShaderDefinitions definitions, int location, int component, bool isOutput, bool isPerPatch)
        {
            string name = isPerPatch
                ? DefaultNames.PerPatchAttributePrefix
                : (isOutput ? DefaultNames.OAttributePrefix : DefaultNames.IAttributePrefix);

            if (location < 0)
            {
                return (name, definitions.GetUserDefinedType(0, isOutput));
            }

            name += location.ToString(CultureInfo.InvariantCulture);

            if (definitions.HasPerLocationInputOrOutputComponent(IoVariable.UserDefined, location, component, isOutput))
            {
                name += "_" + "xyzw"[component & 3];
            }

            string prefix = isOutput ? "out" : "in";

            return (prefix + "." + name, definitions.GetUserDefinedType(location, isOutput));
        }
    }
}
