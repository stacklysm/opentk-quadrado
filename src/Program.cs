using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace OpenTKApp1
{
    class GLShader
    {
        const int SHADER_TRUE = 0;

        public int ID { get; }
        public ShaderType ShaderType { get; }
        public string ShaderSource { get; }

        public GLShader(ShaderType shaderType, string shaderSource)
        {
            ShaderType = shaderType;
            ShaderSource = shaderSource;

            ID = GL.CreateShader(ShaderType);
            GL.ShaderSource(ID, shaderSource);
        }

        public bool Compile(out string message)
        {
            message = "";

            GL.GetShader(ID, ShaderParameter.CompileStatus, out int status);

            if (status != SHADER_TRUE)
            {
                GL.GetShaderInfoLog(ID, out message);
                return false;
            }

            return true;
        }
    }

    class GLProgram
    {
        const int PROGRAM_TRUE = 1;

        public int ID { get; }

        public GLProgram(params GLShader[] shaders)
        {
            if (shaders.Length < 2)
            {
                throw new ArgumentException("Provide at least two shader");
            }

            ID = GL.CreateProgram();
            Array.ForEach(shaders, (x) => GL.AttachShader(ID, x.ID));
        }

        public bool Link(out string message)
        {
            message = "";

            GL.LinkProgram(ID);
            GL.GetProgram(ID, GetProgramParameterName.LinkStatus, out int status);

            if (status != PROGRAM_TRUE)
            {
                GL.GetProgramInfoLog(ID, out message);
                return false;
            }

            return true;
        }

        public void Use()
        {
            GL.UseProgram(ID);
        }
    }

    class GLBuffer
    {
        readonly BufferTarget target;

        public int ID { get; }

        void ThrowIfInvalid()
        {
            int currentBuffer = GL.GetInteger(target == BufferTarget.ArrayBuffer ? GetPName.ArrayBufferBinding : GetPName.ElementArrayBufferBinding);

            if (currentBuffer != ID)
            {
                throw new InvalidOperationException("Cannot load data. This object is referencing an invalid/unbound buffer");
            }
        }

        public GLBuffer(BufferTarget bufferTarget)
        {
            if (bufferTarget != BufferTarget.ArrayBuffer && bufferTarget != BufferTarget.ElementArrayBuffer)
            {
                throw new ArgumentException($"target must be {nameof(BufferTarget.ArrayBuffer)} or {nameof(BufferTarget.ElementArrayBuffer)}");
            }

            target = bufferTarget;
            ID = GL.GenBuffer();
        }

        public void LoadFloat(float[] bufferData)
        {
            ThrowIfInvalid();

            unsafe
            {
                fixed (float* pData = bufferData)
                {
                    GL.BufferData(target, bufferData.Length * sizeof(float), new IntPtr(pData), BufferUsageHint.StaticDraw);
                }
            }

        }

        public void LoadUnsignedInt(uint[] bufferData)
        {
            ThrowIfInvalid();

            unsafe
            {
                fixed (uint* pData = bufferData)
                {
                    GL.BufferData(target, bufferData.Length * sizeof(uint), new IntPtr(pData), BufferUsageHint.StaticDraw);
                }
            }
        }

        public void Bind()
        {
            GL.BindBuffer(target, ID);
        }

        public void Unbind()
        {
            ThrowIfInvalid();

            GL.BindBuffer(target, 0);
        }
    }

    class VAO
    {
        struct VAOAttribute
        {
            public VertexAttribPointerType AttributeType;
            public int TypeSize;
            public int Index;
            public int ComponentCount;
            public int Offset => Index * ComponentCount * TypeSize;
            public bool Normalized;
        }

        class VAOAttributeCollection
        {
            public bool Ready { get; private set; }
            public readonly List<VAOAttribute> Items = new List<VAOAttribute>();
            public int Stride => Items.Sum(x => x.ComponentCount * x.TypeSize);

            public void MarkReady()
            {
                Ready = true;
            }
        }

        readonly VAOAttributeCollection attributes = new VAOAttributeCollection();

        public int ID { get; }

        void ThrowIfInvalid()
        {
            int currentVao = GL.GetInteger(GetPName.VertexArrayBinding);

            if (currentVao != ID)
            {
                throw new InvalidOperationException("Cannot bind VAO. This object is referencing an invalid/unbound VAO");
            }
        }

        public VAO()
        {
            ID = GL.GenVertexArray();
        }

        public void Bind()
        {
            GL.BindVertexArray(ID);
        }

        public void PrepareAttribute(VertexAttribPointerType attributeType, int typeSize, int numComponents, bool normalized = false)
        {
            ThrowIfInvalid();

            if (attributes.Ready)
            {
                throw new InvalidOperationException("Cannot modify a locked attribute list");
            }

            VAOAttribute attribute = new VAOAttribute()
            {
                AttributeType = attributeType,
                ComponentCount = numComponents,
                Index = attributes.Items.Count,
                Normalized = normalized,
                TypeSize = typeSize
            };

            attributes.Items.Add(attribute);
        }

        public void SubmitAttributes()
        {
            ThrowIfInvalid();

            attributes.MarkReady();

            attributes.Items.ForEach(attrib =>
            {
                GL.VertexAttribPointer(attrib.Index, attrib.ComponentCount, attrib.AttributeType, attrib.Normalized, attributes.Stride, attrib.Offset);
            });
        }

        public void EnableAttributes()
        {
            ThrowIfInvalid();

            attributes.Items.ForEach(x => GL.EnableVertexAttribArray(x.Index));
        }
    }

    class AppWindow : GameWindow
    {
        GLProgram program;
        VAO vao;

        public AppWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            Resize += App_Resize;
            Load += App_Load;
            RenderFrame += App_RenderFrame;
        }

        private void App_Load()
        {
            string errorMessage;

            string vertexShaderSource = File.ReadAllText("..\\Shaders\\vertex.glsl");
            string fragmentShaderSource = File.ReadAllText("..\\Shaders\\fragment.glsl");

            GLShader vertexShader = new GLShader(ShaderType.VertexShader, vertexShaderSource);

            if (!vertexShader.Compile(out errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            GLShader fragmentShader = new GLShader(ShaderType.FragmentShader, fragmentShaderSource);

            if (!fragmentShader.Compile(out errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            program = new GLProgram(vertexShader, fragmentShader);

            if (!program.Link(out errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            float[] data =
            {
                0.5f,  0.5f, 0.0f, 1.0f, 1.0f, 1.0f,
                -0.5f,  0.5f, 0.0f, 1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, 0.0f, 0.0f, 1.0f, 0.0f,
                0.5f, -0.5f, 0.0f, 0.0f, 0.0f, 1.0f
            };

            uint[] indices =
            {
                0, 1, 2, 2, 3, 0
            };

            vao = new VAO();

            GLBuffer vbo = new GLBuffer(BufferTarget.ArrayBuffer);
            GLBuffer ebo = new GLBuffer(BufferTarget.ElementArrayBuffer);

            vao.Bind();

            vbo.Bind();
            vbo.LoadFloat(data);

            ebo.Bind();
            ebo.LoadUnsignedInt(indices);

            vao.PrepareAttribute(VertexAttribPointerType.Float, sizeof(float), 3);
            vao.PrepareAttribute(VertexAttribPointerType.Float, sizeof(float), 3);

            vao.SubmitAttributes();
            vao.EnableAttributes();

            vao.Bind();
            program.Use();
        }

        private void App_RenderFrame(FrameEventArgs obj)
        {
            GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            program.Use();
            vao.Bind();
            GL.DrawElements(BeginMode.Triangles, 6, DrawElementsType.UnsignedInt, 0);

            Context.SwapBuffers();
        }

        private void App_Resize(ResizeEventArgs obj)
        {
            GL.Viewport(0, 0, obj.Width, obj.Height);
        }
    }

    class Program
    {
        static void Main()
        {
            var nativeWindowSettings = new NativeWindowSettings()
            {
                API = ContextAPI.OpenGL,
                APIVersion = new Version(4, 6),
                AutoLoadBindings = true,
                Profile = ContextProfile.Core
            };

            var window = new AppWindow(GameWindowSettings.Default, nativeWindowSettings);

            window.Run();
        }
    }
}