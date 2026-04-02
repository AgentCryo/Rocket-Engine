using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RCS;
using RERL.Components;
using RERL.ShaderTypes;
using static RERL.Loaders.MaterialLoader;
using static RERL.RenderData;
using static RERL.RERL_Core;
using Buffer = System.Buffer;
using Window = OpenTK.Windowing.GraphicsLibraryFramework.Window;

namespace RERL;

public static class RenderPipeline
{
    internal static GBuffer GFrame;
    
    static int _postProcessingQuad_VAO;
    
    internal static readonly List<Shader> Shaders = [];
    internal static readonly List<PostProcess> PostProcesses = [];
    internal static readonly List<PostProcess> EnginePostProcesses = [];
    internal static readonly Dictionary<int, List<Renderable>> ShaderBatchRendering = new();
    internal static readonly List<Renderable> Renderables = [];
    internal static readonly List<LightComponent> Lights = [];
    
    internal static readonly List<Material> Materials = [];

    static Compute clusterBuilder;
    static Compute clusterLighter;

    static Vector3i ClusterGridSize = new Vector3i(12, 12, 24);
    static int      ClusterCount    = 12 * 12 * 24;
    
    static int lightsSSBO = -1, clustersSSBO = -1;
    
    #region Temp

    //static PostProcess clusterBuilderDebug;
    
    #endregion

    internal static void InitializeRenderPipeline()
    {
        GFrame = new GBuffer(RERL_Core.Window.Size);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, GFrame.GetFBO());
        
        _postProcessingQuad_VAO = GL.GenVertexArray();
        
        clustersSSBO = GL.GenBuffer();
        UpdateClustersSsbo();
        
        lightsSSBO = GL.GenBuffer();
        if (Lights.Count != 0) UpdateLightsSsbo();
        
        #region Temp
        
        //clusterBuilderDebug = new PostProcess().AttachPostProcessShader("./Shaders/ClusterBuilding/clusterBuilderDebug.post", RERL_Core.Window);
        //EnginePostProcesses.Add(clusterBuilderDebug);
        
        #endregion
        
        clusterBuilder = (Compute)new Compute().AttachComputeShader("./Shaders/ClusterBuilding/clusterBuilder.comp");
        clusterLighter = (Compute)new Compute().AttachComputeShader("./Shaders/ClusterBuilding/clusterLighter.comp");
        UpdateClusterShaders();
    }

    internal static void RenderPipelineFrame(FrameEventArgs args)
    {
        GL.GenQueries(1, out int query);
        GL.BeginQuery(QueryTarget.TimeElapsed, query);
        
        if(Lights.Count != 0) UpdateLightsSsbo();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, (PostProcesses.Count != 0 || EnginePostProcesses.Count != 0) ? GFrame.GetFBO() : 0);
        GFrame.Clear();
        
        foreach (var kpv in ShaderBatchRendering)
        {
            List<Renderable> renderables = kpv.Value;
            
            Shader shader = renderables[0].GetShader() ?? throw new Exception($"ERR: {renderables[0].GetType().Name} does not have a shader.");
            shader.Use();
            shader.ApplyAutoUniforms();

            foreach (var mr in renderables)
                mr.Render();
        }

        #region Engine Computes

        #region Clusters

        if (Lights.Count != 0) {
            clusterBuilder.Use();
            clusterBuilder.ApplyAutoUniforms();
            clusterBuilder.Dispatch(ClusterGridSize.X, ClusterGridSize.Y, ClusterGridSize.Z);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            
            Logger.Log(ClusterCount.ToString());
            clusterLighter.Use();
            clusterLighter.ApplyAutoUniforms();
            clusterLighter.Dispatch(ClusterCount, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }

        #endregion

        #endregion
        
        GL.DepthMask(false);
        GL.Disable(EnableCap.DepthTest);
        if (PostProcesses.Count != 0)
            for (int p = 0; p < PostProcesses.Count; p++)
                PostProcesses[p].RenderPostProcess(GFrame, _postProcessingQuad_VAO, (p == PostProcesses.Count - 1));

        if (EnginePostProcesses.Count != 0)
            for (int p = 0; p < EnginePostProcesses.Count; p++)
                EnginePostProcesses[p].RenderPostProcess(GFrame, _postProcessingQuad_VAO, p == EnginePostProcesses.Count - 1);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);

        GL.EndQuery(QueryTarget.TimeElapsed);
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, out long gpuTime);
        Logger.Log($"GPU time: {gpuTime / 1_000_000.0} ms");
        
        RERL_Core.Window.SwapBuffers();
    }

    internal static void ResizeRenderPipeline()
    {
        UpdateClusterShaders();
    }
    
    static void RegisterToShaderBatch(Renderable renderable)
    {
        if (renderable.GetShader() == null)
            throw new Exception("No shader found for renderable: " + renderable + ", shader required for rendering.");

        int handle = renderable.GetShader()!.GetHandle();
        if (!ShaderBatchRendering.TryGetValue(handle, out var list))
        {
            list = [];
            ShaderBatchRendering[handle] = list;
        }
        list.Add(renderable);
    }
    
    #region User Interface

    #region Renderable
    
    public static void RegisterRenderable(Renderable renderable)
    {
        Renderables.Add(renderable);
        RegisterToShaderBatch(renderable);
    }
    
    public static void UnregisterRenderable(Renderable renderable)
    {
        Renderables.Remove(renderable);

        int handle = renderable.GetShader()!.GetHandle();
        if (ShaderBatchRendering.TryGetValue(handle, out var list))
        {
            list.Remove(renderable);
            if (list.Count == 0)
                ShaderBatchRendering.Remove(handle);
        }
    }

    #endregion
    
    #region Shader

    public static void RegisterShader(Shader shader)
    {
        Shaders.Add(shader);
        shader.RegisterAutoUniform("uView", () => RERL_Core.Camera.GetView());
        shader.RegisterAutoUniform("uProjection", () => RERL_Core.Camera.GetProjection());
    }

    public static void UnregisterShader(Shader shader) => Shaders.Remove(shader);

    #endregion

    #region Material

    public static int RegisterMaterial(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (Materials.Contains(material)) return Materials.IndexOf(material);
        Materials.Add(material);
        return Materials.IndexOf(material);
    }

    public static void UnregisterMaterial(Material material) => Materials.Remove(material);

    public static int GetMaterialIndex(Material material) => Materials.IndexOf(material);
    
    public static Material GetIndexedMaterial(int materialIndex) => Materials[materialIndex];
    
    #endregion
    
    #region PostProcess
    
    public static void RegisterPostProcess(PostProcess postProcess) => PostProcesses.Add(postProcess);
    public static void UnregisterPostProcess(PostProcess postProcess) => PostProcesses.Remove(postProcess);
    
    #endregion

    #region Light

    public static void RegisterLight(LightComponent light)
    {
        Lights.Add(light);
        UpdateLightsSsbo();
    }

    public static void UnregisterLight(LightComponent light)
    {
        Lights.Remove(light);
        if (Lights.Count != 0) UpdateLightsSsbo();
    }

    public static void SetClusterGridSize(Vector3i gridSize)
    {
        ClusterGridSize = gridSize;
        ClusterCount    = gridSize.X * gridSize.Y * gridSize.Z;
        UpdateClusterShaders();
        UpdateClustersSsbo();
    }

    #endregion
    
    #endregion

    #region Light Clustering

    static void UpdateClusterShaders()
    {
        #region Debug

        //clusterBuilderDebug.Use();
        //clusterBuilderDebug.ApplyUniform("zNear", RERL_Core.Camera.Near);
        //clusterBuilderDebug.ApplyUniform("zFar",  RERL_Core.Camera.Far);
        //clusterBuilderDebug.RegisterAutoUniform("inverseProjection", () => Matrix4.Invert(RERL_Core.Camera.GetProjection()));
        //clusterBuilderDebug.RegisterAutoUniform("gridSize",          () => new Vector3i(ClusterGridSize.X, ClusterGridSize.Y, ClusterGridSize.Z));
        //clusterBuilderDebug.RegisterAutoUniform("viewMatrix",        () => RERL_Core.Camera.GetView());
        //clusterBuilderDebug.ApplyUniform(       "screenDimensions",        RERL_Core.Window.Size);

        #endregion

        #region Cluster Builder

        clusterBuilder.Use();
        clusterBuilder.ApplyUniform("zNear", RERL_Core.Camera.Near);
        clusterBuilder.ApplyUniform("zFar",  RERL_Core.Camera.Far);
        clusterBuilder.RegisterAutoUniform("inverseProjection", () => Matrix4.Invert(RERL_Core.Camera.GetProjection()));
        clusterBuilder.RegisterAutoUniform("gridSize",          () => new Vector3i(ClusterGridSize.X, ClusterGridSize.Y, ClusterGridSize.Z));
        clusterBuilder.ApplyUniform(       "screenDimensions",  RERL_Core.Window.Size);

        #endregion

        #region Cluster Lighter

        clusterLighter.RegisterAutoUniform("viewMatrix", () => RERL_Core.Camera.GetView());
        clusterLighter.RegisterAutoUniform("gridSize",   () => new Vector3i(ClusterGridSize.X, ClusterGridSize.Y, ClusterGridSize.Z));

        #endregion
    }

    static void UpdateClustersSsbo()
    {
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, clustersSSBO);
        const int bytesPerCluster = sizeof(float) * 3 + sizeof(uint) // minPoint + _pad0
                                  + sizeof(float) * 3 + sizeof(uint) // maxPoint + lightCount
                                  + sizeof(uint ) * 128;             // lightIndices

        GL.BufferData(BufferTarget.ShaderStorageBuffer, bytesPerCluster * ClusterCount, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, clustersSSBO);
    }
    
    static void UpdateLightsSsbo()
    {
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, lightsSSBO);
        unsafe {GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(Light) * Lights.Count, IntPtr.Zero, BufferUsageHint.DynamicDraw);}
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, lightsSSBO);
        unsafe {
            Light[] lightArray = Lights
                .Select(lc => lc.LightData)
                .ToArray();

            fixed (Light* src = lightArray)
            {
                IntPtr gpuPtr = GL.MapBufferRange(
                    BufferTarget.ShaderStorageBuffer,
                    IntPtr.Zero,
                    sizeof(Light) * lightArray.Length,
                    MapBufferAccessMask.MapWriteBit |
                    MapBufferAccessMask.MapInvalidateBufferBit
                );

                Buffer.MemoryCopy(
                    src,
                    gpuPtr.ToPointer(),
                    sizeof(Light) * lightArray.Length,
                    sizeof(Light) * lightArray.Length
                );

                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            }
        }
    }

    #endregion
}
