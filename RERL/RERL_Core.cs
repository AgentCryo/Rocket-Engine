using OpenTK.Mathematics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
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

    #region Temp
    
    static Shader? _tempShader;

    static float _time;

    static MeshRenderer _meshObject = new();
    static MeshRenderer _icosahedron = new();
    static MeshRenderer _sphere = new();
    
    #endregion
    
    static Dictionary<int, List<MeshRenderer>> _shaderBatchRendering = new();
    static List<MeshRenderer> _renderables = [];
    
    public static void Load(Camera camera)
    {
        var assembly = AppDomain.CurrentDomain .GetAssemblies() .FirstOrDefault(a => a.GetName().Name == "RERL");

        Console.WriteLine(assembly != null ? $"Library Found: {assembly.FullName}" : "Library not Found");

        GL.ClearColor(Color.FromArgb(255, 20,25,35));
        GL.Enable(EnableCap.DepthTest);
        
        #region Temp

        _tempShader = new Shader().AttachShader("./Shaders/Default/default.vert", "./Shaders/Default/default.frag");
        _tempShader.AddAutoUniform("uView", () => camera.GetView());
        _tempShader.AddAutoUniform("uProjection", () => camera.GetProjection());
        
        Mesh mesh = MeshLoader.ParseMesh(
            @".\Models\Cube.obj");
        _meshObject.AttachMesh(mesh);
        _meshObject.AttachShader(_tempShader);
        _meshObject.BuildMeshBuffers();
        
        mesh = MeshLoader.ParseMesh(
            @".\Models\Icosahedron.obj");
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
        
        #endregion
    }
    
    public static void RenderFrame(GameWindow gameWindow, Camera camera, FrameEventArgs args)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
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