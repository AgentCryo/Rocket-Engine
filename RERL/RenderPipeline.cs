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

    #region Temp

    static PostProcess clusterBuilderDebug;
    static Compute clusterBuilder;
    
    const int gridSizeX = 12;
    const int gridSizeY = 12;
    const int gridSizeZ = 24;
    const int ClusterCount = gridSizeX * gridSizeY * gridSizeZ;
    
    #endregion

    internal static void InitializeRenderPipeline()
    {
        GFrame = new GBuffer(RERL_Core.Window.Size);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, GFrame.GetFBO());
        
        _postProcessingQuad_VAO = GL.GenVertexArray();

        #region Temp

        
        
        int clustersSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, clustersSSBO);
        unsafe {GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(Cluster) * ClusterCount, IntPtr.Zero, BufferUsageHint.DynamicDraw);}
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, clustersSSBO);
        
        clusterBuilderDebug = new PostProcess().AttachPostProcessShader("./Shaders/ClusterBuilding/clusterBuilderDebug.post", RERL_Core.Window);
        clusterBuilderDebug.RegisterAutoUniform("gridSize",         () => new Vector3i(gridSizeX, gridSizeY, gridSizeZ));
        clusterBuilderDebug.RegisterAutoUniform("screenDimensions", () => RERL_Core.Window.Size);
        EnginePostProcesses.Add(clusterBuilderDebug);
        clusterBuilder = (Compute)new Compute().AttachComputeShader("./Shaders/ClusterBuilding/clusterBuilder.comp");
        clusterBuilder.RegisterAutoUniform("zNear", () => 0.1f);
        clusterBuilder.RegisterAutoUniform("zFar",  () => 100f);
        clusterBuilder.RegisterAutoUniform("inverseProjection", () => Matrix4.Invert(RERL_Core.Camera.GetProjection()));
        clusterBuilder.RegisterAutoUniform("gridSize",          () => new Vector3i(gridSizeX, gridSizeY, gridSizeZ));
        clusterBuilder.RegisterAutoUniform("screenDimensions",  () => RERL_Core.Window.Size);

        #endregion
    }

    internal static void RenderPipelineFrame(FrameEventArgs args)
    {
        GL.GenQueries(1, out int query);
        GL.BeginQuery(QueryTarget.TimeElapsed, query);
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, (PostProcesses.Count != 0 || EnginePostProcesses.Count != 0) ? GFrame.GetFBO() : 0);
        GFrame.Clear();
        
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Lequal); 
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

        clusterBuilder.Use();
        clusterBuilder.ApplyAutoUniforms();
        clusterBuilder.Dispatch(gridSizeX, gridSizeY, gridSizeZ);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

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

    public static void RegisterLight(LightComponent light) => Lights.Add(light);
    public static void UnregisterLight(LightComponent light) => Lights.Remove(light);

    #endregion
    
    #endregion
}
