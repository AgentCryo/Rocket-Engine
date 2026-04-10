using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using RCS;
using RCS.Components;
using RERL.ShaderTypes;
using static RERL.RenderData;

namespace RERL.Components;

/// <summary>
/// A component that renders a mesh using a shader.
/// Handles VAO/VBO/IBO creation, uniform setup, and instanced rendering.
/// </summary>
[Obsolete]
public class MeshRenderer : IComponent, Renderable
{
    public Entity Owner { get; set; }

    public bool AutoRegister { get; set; } = true;

    public MeshRenderer SetAutoRegister(bool autoRegister)
    {
        AutoRegister = autoRegister;
        return this;
    }

    Mesh? _mesh;
    Shader? _shader;
    int _vao = -1, _vbo = -1, _ibo = -1;
    bool _buffersDirty = false;

    public MeshRenderer AttachMesh(Mesh mesh)
    {
        _mesh = mesh;
        _buffersDirty = false;
        return this;
    }

    public MeshRenderer AttachShader(Shader shader, bool buildMeshBuffers = true)
    {
        _shader = shader;
        _buffersDirty = true;
        if (buildMeshBuffers) BuildMeshBuffers();
        return this;
    }

    public Mesh? GetMesh() => _mesh!;
    public Shader? GetShader() => _shader;

    public void OnAdd()
    {
        if (AutoRegister)
            RenderPipeline.RegisterRenderable(this);
    }

    /// <summary>
    /// Builds the VAO, VBO, and IBO for the attached mesh.
    /// Must be called after attaching a mesh unless auto‑built.
    /// </summary>
    /// <param name="silence">If true, missing mesh errors are ignored.</param>
    /// <returns>True if buffers were built successfully.</returns>
    public bool BuildMeshBuffers(bool silence = false)
    {
        if (_mesh == null)
            return silence ? false : throw new Exception("ERR: Mesh is null.");

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ibo = GL.GenBuffer();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            _mesh.Value.Vertices.Length * Marshal.SizeOf<Vertex>(),
            _mesh.Value.Vertices,
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);
        GL.BufferData(BufferTarget.ElementArrayBuffer,
            _mesh.Value.Indices.Length * sizeof(uint),
            _mesh.Value.Indices.ToArray(),
            BufferUsageHint.StaticDraw);

        int stride = Marshal.SizeOf<Vertex>();

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        _buffersDirty = false;
        return true;
    }

    public void RebuildMaterialSSBO()
    {
        
    }

    /// <summary>
    /// Renders the mesh using the attached shader and the owner's Transform.
    /// </summary>
    /// <param name="instanceCount">Number of instances to draw.</param>
    public void Render(int instanceCount = 1)
    {
        if (_mesh == null) throw new Exception("ERR: Mesh is null.");
        if (_shader == null) throw new Exception("ERR: Shader is null.");
        if (_buffersDirty) Console.WriteLine("WRN: Rendering an object with outdated mesh buffers.");
        
        _shader.ApplyUniform("uModel", Owner.Transform.WorldMatrix, false);

        GL.BindVertexArray(_vao);
        GL.DrawElementsInstanced(
            PrimitiveType.Triangles,
            _mesh.Value.Indices.Length,
            DrawElementsType.UnsignedInt,
            0,
            instanceCount);
    }

    /// <summary>
    /// Deletes the VAO, VBO, and IBO associated with this mesh.
    /// </summary>
    public void DisposeBuffers()
    {
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_ibo != 0) GL.DeleteBuffer(_ibo);

        _vao = _vbo = _ibo = 0;
    }

    public void Load() {}
    public void Update(float deltaTime) {}
}
