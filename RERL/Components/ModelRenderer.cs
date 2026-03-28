using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RCS;
using RCS.Components;
using RERL.ShaderTypes;
using static RERL.Loaders.MaterialLoader;
using static RERL.Loaders.ModelLoader;
using static RERL.RenderData;
using static RERL.RERL_Core;

namespace RERL.Components;

public class ModelRenderer : IComponent, Renderable
{
    public Entity Owner { get; set; }

    Model? _model;
    Shader? _shader;
    public Shader? GetShader() => _shader;
    
    int _vao, _ibo, _vbo, _materialVbo;
    int _singleIndirect, _doubleIndirect;
    int _singleCount, _doubleCount;
    int _materialSSBO;

    List<Material> _localMaterials = [];

    public bool AutoRegister { get; set; } = true;

    public ModelRenderer SetAutoRegister(bool autoRegister)
    {
        AutoRegister = autoRegister;
        return this;
    }
    
    public ModelRenderer AttachModel(Model model)
    {
        _model = model;
        return this;
    }

    public ModelRenderer AttachShader(Shader shader, bool buildModelBuffers = true)
    {
        _shader = shader;
        if (buildModelBuffers) BuildModelBuffers();
        return this;
    }

    public void OnAdd()
    {
        if (AutoRegister)
            RenderPipeline.RegisterRenderable(this);
    }

    public void BuildModelBuffers()
    {
        if (_model is not { } model) { Logger.Warning("ModelRenderer added without a model."); return; }
        if (_shader == null) { Logger.Warning("ModelRenderer added without a shader."); return; }

        DisposeBuffers();
        
        // 0. Consolidate Mesh

        var verticeCount = _model.SubMeshes.Sum(mesh => mesh.Vertices.Length);
        var indiceCount  = _model.SubMeshes.Sum(mesh => mesh.Indices.Length);

        var combinedVertices = new Vertex[verticeCount];
        var combinedIndices  = new uint[indiceCount];
        
        var vertexOffset  = 0;
        var indexWritePos = 0;

        var materialIndexes = new int[verticeCount];
        Dictionary<int, int> globalToLocalMaterials = new();
        List<Material> materialArray = [];

        foreach (var mesh in _model.SubMeshes)
        {
            int g = mesh.MaterialIndex;
            if (g == -1) continue;
            if (!globalToLocalMaterials.ContainsKey(g)) {
                globalToLocalMaterials[g] = materialArray.Count;
                materialArray.Add(RenderPipeline.GetIndexedMaterial(g));
            }
        }

        for (var i = 0; i < _model.SubMeshes.Length; i++) {
            var mesh = _model.SubMeshes[i];
            Array.Copy(mesh.Vertices, 0, combinedVertices, vertexOffset, mesh.Vertices.Length);
            foreach (var index in mesh.Indices) combinedIndices[indexWritePos++] = index + (uint)vertexOffset;
            for (var v = 0; v < mesh.Vertices.Length; v++)
                materialIndexes[vertexOffset + v] = globalToLocalMaterials[mesh.MaterialIndex];
            vertexOffset += mesh.Vertices.Length;
        }

        _localMaterials = materialArray;

        // 1. Create VAO/IBO/VBO

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ibo = GL.GenBuffer();
        _materialVbo = GL.GenBuffer();
        
        GL.BindVertexArray(_vao);

        // 2. Upload vertices
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            combinedVertices.Length * Marshal.SizeOf<Vertex>(),
            combinedVertices,
            BufferUsageHint.StaticDraw);

        // 3. Upload indices
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);
        GL.BufferData(BufferTarget.ElementArrayBuffer,
            combinedIndices.Length * sizeof(uint),
            combinedIndices,
            BufferUsageHint.StaticDraw);

        int stride = Marshal.SizeOf<Vertex>();
        
        // Position (location = 0)
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

        // Normal (location = 1)
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 12);

        // UV (location = 2)
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 24);

        // Material Indexes (location = 4)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _materialVbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            materialIndexes.Length * sizeof(int),
            materialIndexes,
            BufferUsageHint.StaticDraw);
        
        GL.EnableVertexAttribArray(4);
        GL.VertexAttribIPointer(4, 1, VertexAttribIntegerType.Int, sizeof(int), IntPtr.Zero);
        
        // 4. Create indirect draw commands
        
        List<DrawElementsIndirectCommand> singleSided = [];
        List<DrawElementsIndirectCommand> doubleSided = [];

        uint runningIndexOffset = 0;
        
        for (int i = 0; i < _model.SubMeshes.Length; i++)
        {
            var mesh = _model.SubMeshes[i];
            
            var cmd = new DrawElementsIndirectCommand
            {
                Count = (uint)mesh.Indices.Length,
                InstanceCount = 1,
                FirstIndex = runningIndexOffset,
                BaseVertex = 0,
                BaseInstance = 0
            };
            
            if (RenderPipeline.GetIndexedMaterial(mesh.MaterialIndex).DoubleSided)
                doubleSided.Add(cmd);
            else
                singleSided.Add(cmd);
            
            runningIndexOffset += (uint)mesh.Indices.Length;
        }
        
        _singleCount  = singleSided.Count;
        _doubleCount  = doubleSided.Count;
        _singleIndirect = UploadIndirect(singleSided);
        _doubleIndirect = UploadIndirect(doubleSided);
        
        // 5. Create material SSBO (bindless handles as uvec2 via GPUMaterial)

        _materialSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _materialSSBO);

        var materials = new GPUMaterial[materialArray.Count];

        for (int i = 0; i < materialArray.Count; i++)
        {
            ulong h = materialArray[i].AlbedoHandle;

            materials[i].BaseColor      = new Vector4(materialArray[i].BaseAlbedo, 1.0f);
            materials[i].AlbedoHandleLo = (uint)(h & 0xFFFFFFFF);
            materials[i].AlbedoHandleHi = (uint)(h >> 32);
        }
        
        GL.BufferData(
            BufferTarget.ShaderStorageBuffer,
            materials.Length * Marshal.SizeOf<GPUMaterial>(),
            materials,
            BufferUsageHint.StaticDraw
        );

        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _materialSSBO);
        
        return;
        
        int UploadIndirect(List<DrawElementsIndirectCommand> cmds)
        {
            int buffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, buffer);

            GL.BufferData(
                BufferTarget.DrawIndirectBuffer,
                cmds.Count * Marshal.SizeOf<DrawElementsIndirectCommand>(),
                cmds.ToArray(),
                BufferUsageHint.StaticDraw
            );

            return buffer;
        }
    }

    public void Render(int instanceCount = 1)
    {
        if (_model is not { } model) { Logger.Warning("ModelRenderer added without a model."); return; }
        if (_shader == null) { Logger.Warning("ModelRenderer added without a shader."); return; }
        
        _shader.Use();
        _shader.ApplyUniform("uModel", Owner.Transform.WorldMatrix, false);

        GL.BindVertexArray(_vao);

        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _materialSSBO);

        if (_singleCount > 0) {
            GL.Enable(EnableCap.CullFace);
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, _singleIndirect);
            GL.MultiDrawElementsIndirect(
                PrimitiveType.Triangles,
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                _singleCount,
                0
            );
        }

        if (_doubleCount > 0) {
            GL.Disable(EnableCap.CullFace);
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, _doubleIndirect);
            GL.MultiDrawElementsIndirect(
                PrimitiveType.Triangles,
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                _doubleCount,
                0
            );
        }
    }
    
    public void RebuildMaterialSSBO()
    {
        if (_localMaterials.Count == 0 || _materialSSBO == 0) return;

        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _materialSSBO);

        var materials = new GPUMaterial[_localMaterials.Count];

        for (int i = 0; i < _localMaterials.Count; i++)
        {
            var mat = _localMaterials[i];
            ulong h = mat.AlbedoHandle;

            materials[i].BaseColor      = new Vector4(mat.BaseAlbedo, 1.0f);
            materials[i].AlbedoHandleLo = (uint)(h & 0xFFFFFFFF);
            materials[i].AlbedoHandleHi = (uint)(h >> 32);
        }

        GL.BufferData(
            BufferTarget.ShaderStorageBuffer,
            materials.Length * Marshal.SizeOf<GPUMaterial>(),
            materials,
            BufferUsageHint.StaticDraw
        );
    }

    public void Load() {}
    public void Update(float deltaTime) {}

    public void DisposeBuffers()
    {
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_ibo != 0) GL.DeleteBuffer(_ibo);
        if (_singleIndirect != 0) GL.DeleteBuffer(_singleIndirect);
        if (_doubleIndirect != 0) GL.DeleteBuffer(_doubleIndirect);
        if (_materialSSBO != 0) GL.DeleteBuffer(_materialSSBO);
        if (_materialVbo != 0) GL.DeleteBuffer(_materialVbo);

        _vao = _vbo = _materialVbo = _ibo = _singleIndirect = _doubleIndirect = _materialSSBO = 0;
    }
}
