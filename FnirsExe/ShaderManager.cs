using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;

namespace FnirsExe
{
    public class Shader : IDisposable
    {
        private int _handle;

        public Shader(string vertexShaderSource, string fragmentShaderSource)
        {
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);

            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(vertexShader);
                throw new Exception($"顶点着色器编译错误: {infoLog}");
            }

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);

            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(fragmentShader);
                throw new Exception($"片段着色器编译错误: {infoLog}");
            }

            _handle = GL.CreateProgram();
            GL.AttachShader(_handle, vertexShader);
            GL.AttachShader(_handle, fragmentShader);
            GL.LinkProgram(_handle);

            GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(_handle);
                throw new Exception($"着色器程序链接错误: {infoLog}");
            }

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        public void Use()
        {
            GL.UseProgram(_handle);
        }

        public void SetMatrix4(string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(_handle, name);
            GL.UniformMatrix4(location, false, ref matrix);
        }

        public void Dispose()
        {
            GL.DeleteProgram(_handle);
        }
    }

    public static class ShaderManager
    {
        private static Shader _brainShader;

        public static Shader GetBrainShader()
        {
            if (_brainShader == null)
            {
                string vertexShaderSource = @"
                    #version 330 core
                    layout (location = 0) in vec3 aPos;
                    layout (location = 1) in vec3 aColor;
                    
                    out vec3 vertexColor;
                    
                    uniform mat4 model;
                    uniform mat4 view;
                    uniform mat4 projection;
                    
                    void main()
                    {
                        gl_Position = projection * view * model * vec4(aPos, 1.0);
                        vertexColor = aColor;
                    }
                ";

                string fragmentShaderSource = @"
                    #version 330 core
                    in vec3 vertexColor;
                    out vec4 FragColor;
                    
                    void main()
                    {
                        FragColor = vec4(vertexColor, 1.0);
                    }
                ";

                _brainShader = new Shader(vertexShaderSource, fragmentShaderSource);
            }
            return _brainShader;
        }

        public static void Cleanup()
        {
            _brainShader?.Dispose();
            _brainShader = null;
        }
    }
}