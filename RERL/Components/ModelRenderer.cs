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
    
    int _vao, _ibo, _vbo, _materialVbo, _ebo;
    int _singleIndirect, _doubleIndirect;
    int _singleCount, _doubleCount;
    int _materialSSBO;
    int albedoTextures;

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
        var indiceCount = _model.SubMeshes.Sum(mesh => mesh.Indices.Length);

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
            Array.Copy(mesh.Vertices, (long)0, combinedVertices, vertexOffset, mesh.Vertices.Length);
            foreach (var index in mesh.Indices) combinedIndices[indexWritePos++] = index + (uint)vertexOffset;
            for (var v = 0; v < mesh.Vertices.Length; v++)
                materialIndexes[vertexOffset + v] = globalToLocalMaterials[mesh.MaterialIndex];
            vertexOffset += mesh.Vertices.Length;
        }

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

        // Tangent (location = 3) // I don't have this yet.
        //GL.EnableVertexAttribArray(3);
        //GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, 32);
        
        // Upload material indexes
        GL.BindBuffer(BufferTarget.ArrayBuffer, _materialVbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            materialIndexes.Length * sizeof(int),
            materialIndexes,
            BufferUsageHint.StaticDraw);
        
        // Material Indexes (location = 4)
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
                BaseInstance = (uint)i
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
        
        // 5. Create dummy material SSBO
        _materialSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _materialSSBO);
        
        Dictionary<int, int> textureToLayer = new();
        List<int> albedoTextures = [];
        var materials = new GPUMaterial[materialArray.Count];

        for (int i = 0; i < materialArray.Count; i++)
        {
            var texture = materialArray[i].AlbedoTexture;
            var layer = -1;

            var valid = texture > 0 && IsValidTexture(texture);

            if (valid && !textureToLayer.TryGetValue(texture, out layer)) {
                layer = albedoTextures.Count;
                albedoTextures.Add(texture);
                textureToLayer[texture] = layer;
            }

            materials[i].albedo = layer;
            materials[i].BaseColor = new Vector4(materialArray[i].BaseAlbedo, 1.0f);
        }
        
        GL.BufferData(
            BufferTarget.ShaderStorageBuffer,
            materials.Length * Marshal.SizeOf<GPUMaterial>(),
            materials,
            BufferUsageHint.StaticDraw
        );

        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _materialSSBO);
        
        int width = 0, height = 0;

        if (albedoTextures.Count > 0) {
            var firstTex = albedoTextures[0];

            GL.BindTexture(TextureTarget.Texture2D, firstTex);
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out width);
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out height);
        }

        if (width <= 0 || height <= 0) {
            this.albedoTextures = 0;
            return;
        }
        
        this.albedoTextures = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2DArray, this.albedoTextures);
        
        //int.Max(1 + (int)Math.Floor(double.Log2(int.Max(width, height))), 4)
        GL.TexStorage3D(
            TextureTarget3d.Texture2DArray,
            1,
            SizedInternalFormat.Srgb8Alpha8,
            width, height,
            albedoTextures.Count
        );

        for (int i = 0; i < albedoTextures.Count; i++) {
            GL.CopyImageSubData(albedoTextures[i], ImageTarget.Texture2D, 0, 0, 0, 0, this.albedoTextures,
                ImageTarget.Texture2DArray, 0, 0, 0, i, width, height, 1);
        }
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
        
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
        
        bool IsValidTexture(int handle)
        {
            if (handle <= 0)
                return false;

            GL.BindTexture(TextureTarget.Texture2D, handle);

            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0,
                GetTextureParameter.TextureWidth, out int w);
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0,
                GetTextureParameter.TextureHeight, out int h);

            return w > 0 && h > 0;
        }
    }

    public void Render(int instanceCount = 1)
    {
        if (_model is not { } model) { Logger.Warning("ModelRenderer added without a model."); return; }

        if (_shader == null) { Logger.Warning("ModelRenderer added without a shader."); return; }
        
        _shader.Use();
        _shader.ApplyUniform("uModel", Owner.Transform.WorldMatrix, false);

        if (albedoTextures != 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, albedoTextures);
            _shader.ApplyUniform("uAlbedoTextures", 0);
        }
        
        GL.BindVertexArray(_vao);

        // Bind SSBO
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _materialSSBO);

        if (_singleCount > 0) {
            // PASS 1 single-sided
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
            // PASS 2 double-sided
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