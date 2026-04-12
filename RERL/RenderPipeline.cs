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
    internal static readonly List<LightComponent> GlobalLights = [];
    
    internal static readonly List<Material> Materials = [];

    static Compute _clusterBuilder;
    static Compute _clusterLighter;

    static Vector3i _clusterGridSize = new Vector3i(12, 12, 24);
    static int      _clusterCount    = 12 * 12 * 24;
    
    static int _lightsSsbo = -2, _globalLightsSsbo = -2, _clustersSsbo = -2;

    static PostProcess _deferredShading;
    
    #region Temp

    //static PostProcess clusterBuilderDebug;
    
    #endregion

    internal static void InitializeRenderPipeline()
    {
        GFrame = new GBuffer(RERL_Core.Window.Size);

        _postProcessingQuad_VAO = GL.GenVertexArray();

        LightManager.Initialize();
        ClusterManager.Initialize();

        _deferredShading = new PostProcess().AttachPostProcessShader("./RocketEngine/Shaders/LightShaders/deferredShading.post", RERL_Core.Window);
        EnginePostProcesses.Add(_deferredShading);
        UpdateDeferredShader();
    }
    
    internal static void RenderPipelineFrame(FrameEventArgs args)
    {
        GL.GenQueries(1, out int query);
        GL.BeginQuery(QueryTarget.TimeElapsed, query);
        
        if(Lights.Count != 0) LightManager.UploadCusterLights();
        if(GlobalLights.Count != 0) LightManager.UploadGlobalLights();
        GFrame.Clear();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, (PostProcesses.Count != 0 || EnginePostProcesses.Count != 0) ? GFrame.GetFBO() : 0); 
        
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
            ClusterManager.Builder.Use();
            ClusterManager.Builder.ApplyAutoUniforms();
            ClusterManager.Builder.Dispatch(ClusterManager.GridSize.X, ClusterManager.GridSize.Y, ClusterManager.GridSize.Z);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            
            ClusterManager.Lighter.Use();
            ClusterManager.Lighter.ApplyAutoUniforms();
            ClusterManager.Lighter.Dispatch(ClusterManager.ClusterCount, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }

        #endregion

        #endregion
        
        GL.DepthMask(false);
        GL.Disable(EnableCap.DepthTest);
        if (EnginePostProcesses.Count != 0) // Engine Post Processes
            for (int p = 0; p < EnginePostProcesses.Count; p++)
                EnginePostProcesses[p].RenderPostProcess(GFrame, _postProcessingQuad_VAO, (p == EnginePostProcesses.Count -1 && PostProcesses.Count == 0));
        
        if (PostProcesses.Count != 0) // User Post Processes
            for (int p = 0; p < PostProcesses.Count; p++)
                PostProcesses[p].RenderPostProcess(GFrame, _postProcessingQuad_VAO, (p == PostProcesses.Count - 1));
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);

        GL.EndQuery(QueryTarget.TimeElapsed);
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, out long gpuTime);
        Logger.Log($"GPU time: {gpuTime / 1_000_000.0} ms");
        
        RERL_Core.Window.SwapBuffers();
    }

    internal static void ResizeRenderPipeline() {
        var size = RERL_Core.Window.Size;
        if (size.X <= 0 || size.Y <= 0) return;
        if (!ClusterManager.Initialized) return;

        ClusterManager.UpdateShaderUniforms();
        ClusterManager.AllocateClusterBuffer();
        UpdateDeferredShader();
        GFrame = new GBuffer(size);
    }

    internal static void CameraChange() {
        if (!ClusterManager.Initialized) return;
        ClusterManager.UpdateShaderUniforms();
        ClusterManager.AllocateClusterBuffer();
        UpdateDeferredShader();
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
    }

    public static void UnregisterLight(LightComponent light)
    {
        Lights.Remove(light);
    }
    
    public static void RegisterGlobalLight(LightComponent light)
    {
        GlobalLights.Add(light);
    }

    public static void UnregisterGlobalLight(LightComponent light)
    {
        GlobalLights.Remove(light);
    }

    #endregion
    
    #endregion
    
    #region Deferred Shader

    public static void UpdateDeferredShader()
    {
        if (_deferredShading == null) return;
        _deferredShading.Use();
        _deferredShading.ApplyUniform("screenDimensions", RERL_Core.Window.Size);
        _deferredShading.ApplyUniform("gridSize", _clusterGridSize);
        _deferredShading.ApplyUniform("zNear", RERL_Core.Camera.Near);
        _deferredShading.ApplyUniform("zFar", RERL_Core.Camera.Far);
        _deferredShading.RegisterAutoUniform("viewMatrix", () => RERL_Core.Camera.GetView());
    }

    #endregion
    
    public static class LightManager
    {
        public static bool Initialized { get; private set; } = false;
        static int LightsSsbo;
        static int GlobalLightsSsbo;

        internal static void Initialize()
        {
            LightsSsbo = GL.GenBuffer();
            GlobalLightsSsbo = GL.GenBuffer();
            Initialized = true;
        }

        internal static void UploadCusterLights() => UploadLightArray(Lights, LightsSsbo, 2);
        internal static void UploadGlobalLights() => UploadLightArray(GlobalLights, GlobalLightsSsbo, 3);

        static unsafe void UploadLightArray(List<LightComponent> list, int ssbo, int binding)
        {
            if (list.Count == 0) return;

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(Light) * list.Count, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            Light[] array = list.Select(l => l.LightData).ToArray();

            fixed (Light* src = array)
            {
                IntPtr ptr = GL.MapBufferRange(
                    BufferTarget.ShaderStorageBuffer,
                    IntPtr.Zero,
                    sizeof(Light) * array.Length,
                    MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapInvalidateBufferBit);

                Buffer.MemoryCopy(src, ptr.ToPointer(), sizeof(Light) * array.Length, sizeof(Light) * array.Length);
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            }

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, binding, ssbo);
        }
    }
    
    public static class ClusterManager
    {
        public static bool Initialized { get; private set; } = false;
        internal static Vector3i GridSize = new(12, 12, 24);
        internal static int ClusterCount => GridSize.X * GridSize.Y * GridSize.Z;

        static int ClustersSsbo;

        internal static Compute Builder;
        internal static Compute Lighter;

        internal static void Initialize()
        {
            ClustersSsbo = GL.GenBuffer();
            Builder = (Compute)new Compute().AttachComputeShader("./RocketEngine/Shaders/ClusterBuilding/clusterBuilder.comp");
            Lighter = (Compute)new Compute().AttachComputeShader("./RocketEngine/Shaders/ClusterBuilding/clusterLighter.comp");

            AllocateClusterBuffer();
            UpdateShaderUniforms();
            Initialized = true;
        }

        public static void SetGridSize(Vector3i size)
        {
            GridSize = size;
            AllocateClusterBuffer();
            UpdateShaderUniforms();
        }

        public static void UpdateShaderUniforms()
        {
            if (RERL_Core.Window == null || RERL_Core.Camera == null) return;
            var size = RERL_Core.Window.Size;
            if (size.X <= 0 || size.Y <= 0) return;
            
            Builder.Use();
            Builder.ApplyUniform("zNear", RERL_Core.Camera.Near);
            Builder.ApplyUniform("zFar", RERL_Core.Camera.Far);
            Builder.RegisterAutoUniform("inverseProjection", () => Matrix4.Invert(RERL_Core.Camera.GetProjection()));
            Builder.RegisterAutoUniform("gridSize", () => GridSize);
            Builder.ApplyUniform("screenDimensions", RERL_Core.Window.Size);

            Lighter.Use();
            Lighter.RegisterAutoUniform("viewMatrix", () => RERL_Core.Camera.GetView());
            Lighter.RegisterAutoUniform("gridSize", () => GridSize);

            var proj = RERL_Core.Camera.GetProjection();
            Lighter.ApplyUniform("tanHalfFovX", 1f / proj[0, 0]);
            Lighter.ApplyUniform("tanHalfFovY", 1f / proj[1, 1]);
            Lighter.ApplyUniform("zNear", RERL_Core.Camera.Near);
            Lighter.ApplyUniform("zFar", RERL_Core.Camera.Far);
        }

        public static void AllocateClusterBuffer()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ClustersSsbo);

            const int bytesPerCluster =
                sizeof(float) * 3 + sizeof(uint) +
                sizeof(float) * 3 + sizeof(uint) +
                sizeof(uint) * 128;

            GL.BufferData(BufferTarget.ShaderStorageBuffer, bytesPerCluster * ClusterCount, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, ClustersSsbo);
        }
    }
}
