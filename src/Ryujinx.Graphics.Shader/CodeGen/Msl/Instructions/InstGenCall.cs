using Ryujinx.Graphics.Shader.StructuredIr;

using static Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions.InstGenHelper;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions
{
    static class InstGenCall
    {
        public static string Call(CodeGenContext context, AstOperation operation)
        {
            AstOperand funcId = (AstOperand)operation.GetSource(0);

            var functon = context.GetFunction(funcId.Value);

            int argCount = operation.SourcesCount - 1;
            string[] args = new string[argCount + CodeGenContext.AdditionalArgCount];

            // Additional arguments
            args[0] = "in";
            args[1] = "support_buffer";

            int argIndex = CodeGenContext.AdditionalArgCount;
            for (int i = 0; i < argCount; i++)
            {
                args[argIndex++] = GetSourceExpr(context, operation.GetSource(i + 1), functon.GetArgumentType(i));
            }

            return $"{functon.Name}({string.Join(", ", args)})";
        }
    }
}
