using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace RERL.Objects;

public class MeshRenderer
{
    RERL_Core.Mesh? _mesh;
    Shader? _shader;
    int _vao = -1, _vbo = -1, _ibo = -1;
    bool _buffersDirty = false;

    public MeshRenderer AttachMesh(RERL_Core.Mesh mesh)
    {
        _mesh = mesh;
        _buffersDirty = false;
        return this;
    }

    public MeshRenderer AttachShader(Shader shader, bool buildMeshBuffers = true)
    {
        _shader = shader;
        if (buildMeshBuffers) BuildMeshBuffers();
        return this;
    }
    public RERL_Core.Mesh? GetMesh() => _mesh!;
    public Shader? GetShader() => _shader;


    /// <summary>
    /// The mesh will not update if this is not called.
    /// In <see cref="AttachMesh"/> has a boolean that allows this to be automatically called, unless explicitly told not to.
    /// </summary>
    public bool BuildMeshBuffers(bool silence = false)
    {
        if (_mesh == null)
            return silence ? false : throw new Exception("ERR: Mesh is null.");
        
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ibo = GL.GenBuffer();
        
        GL.BindVertexArray(_vao);
        
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, ((RERL_Core.Mesh)_mesh).Vertices.Length * Marshal.SizeOf<RERL_Core.Vertex>(), ((RERL_Core.Mesh)_mesh).Vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, ((RERL_Core.Mesh)_mesh).Indices.Length * sizeof(uint), ((RERL_Core.Mesh)_mesh).Indices.ToArray(), BufferUsageHint.StaticDraw);
        
        int stride = Marshal.SizeOf<RERL_Core.Vertex>();

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        
        _buffersDirty = true;
        return true;
    }
    
    public void Render(RERL_Core.RenderTransform transform, int instanceCount = 1)
    {
        if (_mesh == null) throw new Exception("ERR: Mesh is null.");
        if (_shader == null) throw new Exception("ERR: Shader is null.");
        if(!_buffersDirty) Console.WriteLine("WRN: Rendering an object with outdated mesh buffers.");
        
        var model =
            Matrix4.CreateScale(transform.Scale) *
            Matrix4.CreateFromQuaternion(transform.Rotation) *
            Matrix4.CreateTranslation(transform.Position);
        
        _shader.ApplyUniform("uModel", model);

        GL.BindVertexArray(_vao);
        GL.DrawElementsInstanced(PrimitiveType.Triangles,  ((RERL_Core.Mesh)_mesh).Indices.Length, DrawElementsType.UnsignedInt, 0, instanceCount);
    }
    
    public void DisposeBuffers()
    {
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_ibo != 0) GL.DeleteBuffer(_ibo);

        _vao = _vbo = _ibo = 0;
    }
}