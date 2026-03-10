using System.ComponentModel;
using OpenTK.Mathematics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RERL.Loaders;
using RERL.Objects;

namespace RERL;

public static class RERL_Core
{
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        public Vector3 Position = position;
        public Vector3 Normal = normal;
        public Vector2 UV = uv;
    }

    public struct Mesh(Vertex[] vertices, uint[] indices)
    {
        public Vertex[] Vertices = vertices;
        public uint[] Indices = indices;
    }

    public struct RenderTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        public Vector3 Position = position;
        public Quaternion Rotation = rotation;
        public Vector3 Scale = scale;
        
        public static RenderTransform Identity =>
            new RenderTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);

        public RenderTransform(Vector3 position)
            : this(position, Quaternion.Identity, Vector3.One) { }

        public RenderTransform(Quaternion rotation)
            : this(Vector3.Zero, rotation, Vector3.One) { }

        public RenderTransform(Vector3 position, Quaternion rotation)
            : this(position, rotation, Vector3.One) { }
    }

    public struct GBuffer
    {
        public int Color, Normal, Depth;
        public int FBO;
        public int GetColor() => Color;
        public int GetNormal() => Normal;
        public int GetDepth() => Depth;
        public int GetFBO() => FBO;
        
        public GBuffer(Vector2i screenSize)
        {
            FBO = GL.GenFramebuffer();
            
            Color = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Color);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                screenSize.X, screenSize.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            SetupTexture2D(Color);

            Normal = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Normal);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f,
                screenSize.X, screenSize.Y, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            SetupTexture2D(Normal);

            Depth = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Depth);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent,
                screenSize.X, screenSize.Y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            SetupTexture2D(Depth);
            
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                throw new Exception($"GBuffer incomplete: {status}");
        }
        
        void SetupTexture2D(int tex)
        {
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }

        public void Clear()
        {
            float[] clear = new float[4];
            GL.GetFloat(GetPName.ColorClearValue, clear);

            // Clear COLOR attachment 0 (albedo)
            GL.ClearBuffer(ClearBuffer.Color, 0, clear);

            // Clear COLOR attachment 1 (normal)
            GL.ClearBuffer(ClearBuffer.Color, 1, clear);

            // Clear DEPTH attachment
            float depthClear = 1f;
            GL.ClearBuffer(ClearBuffer.Depth, 0, ref depthClear);
        }

    }

    #region Temp    
    
    static Shader? _tempShader;

    static float _time;

    static MeshRenderer _meshObject = new();
    static MeshRenderer _icosahedron = new();
    static MeshRenderer _sphere = new();
    
    static PostProcess _postProcess = new();
    
    #endregion

    static int _postProcessingQuad_VAO;
    static GBuffer _geometryFrame;
    static List<PostProcess> _postProcesses = [];
    static Dictionary<int, List<MeshRenderer>> _shaderBatchRendering = new();
    static List<MeshRenderer> _renderables = [];
    
    public static void Load(Camera camera, GameWindow window)
    {
        var assembly = AppDomain.CurrentDomain .GetAssemblies() .FirstOrDefault(a => a.GetName().Name == "RERL");

        Console.WriteLine(assembly != null ? $"Library Found: {assembly.FullName}" : "Library not Found");

        GL.ClearColor(Color.FromArgb(255, 20,25,35));
        GL.Enable(EnableCap.DepthTest);
        
        #region Temp

        _tempShader = new Shader().AttachShader("./Shaders/Default/default.vert", "./Shaders/Default/default.frag");
        _tempShader.RegisterAutoUniform("uView", () => camera.GetView());
        _tempShader.RegisterAutoUniform("uProjection", () => camera.GetProjection());
        
        Mesh mesh = MeshLoader.ParseMesh(
            @"./Models/Cube.obj");
        _meshObject.AttachMesh(mesh);
        _meshObject.AttachShader(_tempShader);
        _meshObject.BuildMeshBuffers();
        
        mesh = MeshLoader.ParseMesh(
            @"./Models/Icosahedron.obj");
        _icosahedron.AttachMesh(mesh);
        _icosahedron.AttachShader(_tempShader);
        _icosahedron.BuildMeshBuffers();

        mesh = MeshLoader.ParseMesh(MeshLoader.UVSphere);
        _sphere.AttachMesh(mesh);
        _sphere.AttachShader(_tempShader);
        _sphere.BuildMeshBuffers();
        
        _renderables.Add(_meshObject);
        _renderables.Add(_icosahedron);
        _renderables.Add(_sphere);
        PopulateShaderBatchRendering();

        _postProcess = new PostProcess().AttachPostProcessShader(@"./Shaders/DefaultPostProcess/defaultPostProcess.frag", window);
        _postProcesses.Add(_postProcess);
        
        _geometryFrame = new GBuffer(window.Size);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _geometryFrame.GetFBO());
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _geometryFrame.Color, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _geometryFrame.Normal, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _geometryFrame.Depth, 0);
        
        DrawBuffersEnum[] drawBuffers = [DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1];
        GL.DrawBuffers(drawBuffers.Length, drawBuffers);
        
        _postProcessingQuad_VAO = GL.GenVertexArray();
        
        #endregion
    }
    
    public static void RenderFrame(GameWindow gameWindow, Camera camera, FrameEventArgs args)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _geometryFrame.GetFBO());
        _geometryFrame.Clear();
        //Render all meshes grouped by shader to minimize shader switches.
        foreach (var kpv in _shaderBatchRendering) {
            //int shaderHandle = kpv.Key;
            List<MeshRenderer> renderables = kpv.Value;
            
            Shader shader = renderables[0].GetShader();
            shader.Use();
            shader.ApplyAutoUniforms();

            foreach (var mr in renderables) {
                //Temp Transform Identity, will be replaced with actual transforms later.
                mr.Render(RenderTransform.Identity);
            }
        }
        
        GBuffer input = _geometryFrame;
        for (int p = 0; p < _postProcesses.Count; p++) {
            input = _postProcesses[p].RenderPostProcess(input, _postProcessingQuad_VAO, (p == _postProcesses.Count - 1));
        }
        
        gameWindow.SwapBuffers();
    }
    
    public static void PopulateShaderBatchRendering()
    {
        foreach (var renderable in _renderables) {
            int handle = renderable.GetShader().GetHandle();

            if (!_shaderBatchRendering.TryGetValue(handle, out List<MeshRenderer>? list))
            {
                list = [];
                _shaderBatchRendering[handle] = list;
            }

            list.Add(renderable);
        }
    }
}