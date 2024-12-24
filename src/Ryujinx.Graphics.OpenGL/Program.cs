using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Buffers.Binary;

namespace Ryujinx.Graphics.OpenGL
{
    class Program : IProgram
    {
        private const int MaxShaderLogLength = 2048;

        public int Handle { get; private set; }

        public bool IsLinked
        {
            get
            {
                if (_status == ProgramLinkStatus.Incomplete)
                {
                    CheckProgramLink(true);
                }

                return _status == ProgramLinkStatus.Success;
            }
        }

        private ProgramLinkStatus _status = ProgramLinkStatus.Incomplete;
        private int[] _shaderHandles;

        public int FragmentOutputMap { get; }

        public Program(ShaderSource[] shaders, int fragmentOutputMap)
        {
            Handle = GL.CreateProgram();

            GL.ProgramParameter(Handle, ProgramParameterName.ProgramBinaryRetrievableHint, 1);

            _shaderHandles = new int[shaders.Length];
            bool hasFragmentShader = false;

            for (int index = 0; index < shaders.Length; index++)
            {
                ShaderSource shader = shaders[index];

                if (shader.Stage == ShaderStage.Fragment)
                {
                    hasFragmentShader = true;
                }

                int shaderHandle = GL.CreateShader(shader.Stage.Convert());

                switch (shader.Language)
                {
                    case TargetLanguage.Glsl:
                        GL.ShaderSource(shaderHandle, shader.Code);
                        GL.CompileShader(shaderHandle);
                        break;
                    case TargetLanguage.Spirv:
                        GL.ShaderBinary(1, ref shaderHandle, (BinaryFormat)All.ShaderBinaryFormatSpirVArb, shader.BinaryCode, shader.BinaryCode.Length);
                        GL.SpecializeShader(shaderHandle, "main", 0, (int[])null, (int[])null);
                        break;
                }

                GL.AttachShader(Handle, shaderHandle);

                _shaderHandles[index] = shaderHandle;
            }

            GL.LinkProgram(Handle);

            FragmentOutputMap = hasFragmentShader ? fragmentOutputMap : 0;
        }

        public Program(ReadOnlySpan<byte> code, bool hasFragmentShader, int fragmentOutputMap)
        {
            Handle = GL.CreateProgram();

            if (code.Length >= 4)
            {
                BinaryFormat binaryFormat = (BinaryFormat)BinaryPrimitives.ReadInt32LittleEndian(code.Slice(code.Length - 4, 4));

                unsafe
                {
                    fixed (byte* ptr = code)
                    {
                        GL.ProgramBinary(Handle, binaryFormat, (nint)ptr, code.Length - 4);
                    }
                }
            }

            FragmentOutputMap = hasFragmentShader ? fragmentOutputMap : 0;
        }

        public void Bind()
        {
            GL.UseProgram(Handle);
        }

        public ProgramLinkStatus CheckProgramLink(bool blocking)
        {
            if (!blocking && HwCapabilities.SupportsParallelShaderCompile)
            {
                GL.GetProgram(Handle, (GetProgramParameterName)ArbParallelShaderCompile.CompletionStatusArb, out int completed);

                if (completed == 0)
                {
                    return ProgramLinkStatus.Incomplete;
                }
            }

            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int status);
            DeleteShaders();

            if (status == 0)
            {
                _status = ProgramLinkStatus.Failure;

                string log = GL.GetProgramInfoLog(Handle);

                if (log.Length > MaxShaderLogLength)
                {
                    log = log[..MaxShaderLogLength] + "...";
                }

                Logger.Warning?.Print(LogClass.Gpu, $"Shader linking failed: \n{log}");
            }
            else
            {
                _status = ProgramLinkStatus.Success;
            }

            return _status;
        }

        public byte[] GetBinary()
        {
            GL.GetProgram(Handle, (GetProgramParameterName)All.ProgramBinaryLength, out int size);

            byte[] data = new byte[size + 4];

            GL.GetProgramBinary(Handle, size, out _, out BinaryFormat binFormat, data);

            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(size, 4), (int)binFormat);

            return data;
        }

        private void DeleteShaders()
        {
            if (_shaderHandles != null)
            {
                foreach (int shaderHandle in _shaderHandles)
                {
                    GL.DetachShader(Handle, shaderHandle);
                    GL.DeleteShader(shaderHandle);
                }

                _shaderHandles = null;
            }
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                DeleteShaders();
                GL.DeleteProgram(Handle);

                Handle = 0;
            }
        }
    }
}
