using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RCS;
using RERL.Components;
using RERL.Shader_Engine;
using static RERL.Loaders.MaterialLoader;
using Buffer = System.Buffer;

namespace RERL;

public static class RenderPipeline
{
    internal static GBuffer GFrame;
    static int _postProcessQuadVao;
    internal static readonly List<PostProcessShader> PostProcesses = [];
    internal static readonly List<PostProcessShader> EnginePostProcesses = [];
    internal static readonly Dictionary<GraphicsShader, List<Renderable>> ShaderBatches = new();
    internal static readonly List<Renderable> Renderables = [];
    internal static readonly List<LightComponent> Lights = [];
    internal static readonly List<LightComponent> GlobalLights = [];
    internal static readonly List<Material> Materials = [];
    static GraphicsShader _deferredShading = null!;

    // Color buffers post-process pass ping-pong between, so a
    // pass never reads from the same buffer it's writing to. Distinct from
    // GFrame - GFrame.Position/Normal/Depth stay untouched read-only inputs
    // for the entire chain.
    static readonly PostProcessTarget[] _pingPong = new PostProcessTarget[2];

    internal static void InitializeRenderPipeline()
    {
        GFrame = new GBuffer(RERL_Core.Window.Size);
        _postProcessQuadVao = GL.GenVertexArray();
        _pingPong[0] = new PostProcessTarget(RERL_Core.Window.Size);
        _pingPong[1] = new PostProcessTarget(RERL_Core.Window.Size);
        LightManager.Initialize();
        ClusterManager.Initialize();
        _deferredShading = ShaderEngine.RegisterGraphics("DeferredShading", "./RocketEngine/Shaders/Templates/DefaultPostProcess/defaultPostProcess.vert", "./RocketEngine/Shaders/LightShaders/deferredShading.post");
        EnginePostProcesses.Add(new PostProcessShader(_deferredShading));
        UpdateDeferredShader();
    }

    internal static void RenderPipelineFrame(FrameEventArgs args)
    {
        GL.GenQueries(1, out int query);
        GL.BeginQuery(QueryTarget.TimeElapsed, query);

        if (Lights.Count > 0) LightManager.UploadClusterLights();
        if (GlobalLights.Count > 0) LightManager.UploadGlobalLights();

        GFrame.Clear();

        bool hasPostProcessing = PostProcesses.Count > 0 || EnginePostProcesses.Count > 0;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, hasPostProcessing ? GFrame.GetFBO() : 0);

        RenderGeometry();
        RenderClusters();
        RenderPostProcessing();

        GL.EndQuery(QueryTarget.TimeElapsed);
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, out long gpuTime);
        Logger.Log($"GPU time: {gpuTime / 1_000_000.0} ms");
        GL.DeleteQuery(query);
        RERL_Core.Window.SwapBuffers();
    }

    static void RenderGeometry()
    {
        foreach (var batch in ShaderBatches)
        {
            GraphicsShader shader = batch.Key;
            shader.Use();
            shader.ApplyAutoUniforms();
            ApplyFrameUniforms(shader);

            foreach (var renderable in batch.Value)
                renderable.Render();
        }
    }

    static void ApplyFrameUniforms(GraphicsShader shader)
    {
        shader.Set("uView", RERL_Core.Camera.GetView());
        shader.Set("uProjection", RERL_Core.Camera.GetProjection());
    }

    static void RenderClusters()
    {
        if (Lights.Count == 0) return;

        ClusterManager.Builder.Dispatch(ClusterManager.GridSize.X, ClusterManager.GridSize.Y, ClusterManager.GridSize.Z);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

        ClusterManager.Lighter.Dispatch(ClusterManager.ClusterCount, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
    }

    static void RenderPostProcessing()
    {
        int totalPasses = EnginePostProcesses.Count + PostProcesses.Count;
        if (totalPasses == 0) return;

        GL.DepthMask(false);
        GL.Disable(EnableCap.DepthTest);

        // First pass reads GFrame's raw geometry-pass color output. Every
        // pass after that reads the previous pass's ping-pong output instead.
        int inputColor = GFrame.Color;
        int pingIndex = 0;
        int passIndex = 0;

        void RunPass(PostProcessShader pass)
        {
            bool isLast = passIndex == totalPasses - 1;

            // Last pass writes straight to the screen; every other pass writes
            // into whichever ping-pong buffer isn't currently the input, so
            // read and write targets are always different buffers.
            int targetFbo = isLast ? 0 : _pingPong[pingIndex].FBO;

            pass.Render(GFrame, inputColor, _postProcessQuadVao, targetFbo);

            if (!isLast)
            {
                inputColor = _pingPong[pingIndex].ColorTexture;
                pingIndex = 1 - pingIndex;
            }

            passIndex++;
        }

        foreach (var pass in EnginePostProcesses) RunPass(pass);
        foreach (var pass in PostProcesses) RunPass(pass);

        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);
    }

    internal static void ResizeRenderPipeline()
    {
        var size = RERL_Core.Window.Size;
        if (size.X <= 0 || size.Y <= 0 || !ClusterManager.Initialized) return;

        ClusterManager.UpdateShaderUniforms();
        ClusterManager.AllocateClusterBuffer();
        UpdateDeferredShader();
        GFrame = new GBuffer(size);
        _pingPong[0].Resize(size);
        _pingPong[1].Resize(size);
    }

    internal static void CameraChange()
    {
        if (!ClusterManager.Initialized) return;

        ClusterManager.UpdateShaderUniforms();
        ClusterManager.AllocateClusterBuffer();
        UpdateDeferredShader();
    }

    #region Registering

    static void RegisterToShaderBatch(Renderable renderable)
    {
        GraphicsShader shader = renderable.GetShader() ?? throw new Exception($"ERR: {renderable.GetType().Name} does not have a shader.");

        if (!ShaderBatches.TryGetValue(shader, out var batch))
        {
            batch = [];
            ShaderBatches.Add(shader, batch);
        }

        batch.Add(renderable);
    }

    public static void RegisterRenderable(Renderable renderable)
    {
        if (Renderables.Contains(renderable)) return;

        Renderables.Add(renderable);
        RegisterToShaderBatch(renderable);
    }

    public static void UnregisterRenderable(Renderable renderable)
    {
        if (!Renderables.Remove(renderable)) return;

        GraphicsShader? shader = renderable.GetShader();
        if (shader == null || !ShaderBatches.TryGetValue(shader, out var batch)) return;

        batch.Remove(renderable);
        if (batch.Count == 0) ShaderBatches.Remove(shader);
    }

    public static int RegisterMaterial(Material material) {
        ArgumentNullException.ThrowIfNull(material);
        if (Materials.Contains(material)) return Materials.IndexOf(material);

        Materials.Add(material);
        return Materials.Count - 1;
    }

    public static void UnregisterMaterial(Material material) => Materials.Remove(material);
    public static int GetMaterialIndex(Material material) => Materials.IndexOf(material);
    public static Material GetIndexedMaterial(int index) => Materials[index];

    public static void RegisterPostProcess(PostProcessShader postProcess) {
        if (!PostProcesses.Contains(postProcess)) PostProcesses.Add(postProcess);
    }

    public static void UnregisterPostProcess(PostProcessShader postProcess) => PostProcesses.Remove(postProcess);

    public static void RegisterLight(LightComponent light) {
        if (!Lights.Contains(light)) Lights.Add(light);
    }

    public static void UnregisterLight(LightComponent light) => Lights.Remove(light);

    public static void RegisterGlobalLight(LightComponent light) {
        if (!GlobalLights.Contains(light)) GlobalLights.Add(light);
    }

    public static void UnregisterGlobalLight(LightComponent light) => GlobalLights.Remove(light);

    #endregion

    static void UpdateDeferredShader() {
        if (_deferredShading == null || RERL_Core.Window == null || RERL_Core.Camera == null) return;

        _deferredShading.Use();
        _deferredShading.Set("screenDimensions", RERL_Core.Window.Size);
        _deferredShading.Set("gridSize", ClusterManager.GridSize);
        _deferredShading.Set("zNear", RERL_Core.Camera.Near);
        _deferredShading.Set("zFar", RERL_Core.Camera.Far);
        _deferredShading.RegisterAutoUniform("viewMatrix", () => RERL_Core.Camera.GetView());
    }

    public static class LightManager
    {
        public static bool Initialized { get; private set; }
        static int _lightsSsbo;
        static int _globalLightsSsbo;

        internal static void Initialize()
        {
            _lightsSsbo = GL.GenBuffer();
            _globalLightsSsbo = GL.GenBuffer();
            Initialized = true;
        }

        internal static void UploadClusterLights() => UploadLightArray(Lights, _lightsSsbo, 2);
        internal static void UploadGlobalLights() => UploadLightArray(GlobalLights, _globalLightsSsbo, 3);

        static unsafe void UploadLightArray(List<LightComponent> list, int ssbo, int binding)
        {
            if (list.Count == 0) return;

            RenderData.Light[] lights = list.Select(x => x.LightData).ToArray();
            int size = sizeof(RenderData.Light) * lights.Length;

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, size, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            fixed (RenderData.Light* source = lights)
            {
                IntPtr destination = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, size, MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapInvalidateBufferBit);
                if (destination == IntPtr.Zero) throw new Exception("Failed to map light SSBO.");

                Buffer.MemoryCopy(source, destination.ToPointer(), size, size);
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            }

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, binding, ssbo);
        }
    }

    public static class ClusterManager
    {
        public static bool Initialized { get; private set; }
        internal static Vector3i GridSize { get; private set; } = new(12, 12, 24);
        internal static int ClusterCount => GridSize.X * GridSize.Y * GridSize.Z;
        static int _clustersSsbo;
        internal static ComputeShader Builder { get; private set; } = null!;
        internal static ComputeShader Lighter { get; private set; } = null!;

        internal static void Initialize()
        {
            _clustersSsbo = GL.GenBuffer();

            Builder = ShaderEngine.RegisterCompute("ClusterBuilder", "./RocketEngine/Shaders/ClusterBuilding/clusterBuilder.comp");
            Lighter = ShaderEngine.RegisterCompute("ClusterLighter", "./RocketEngine/Shaders/ClusterBuilding/clusterLighter.comp");

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

        internal static void UpdateShaderUniforms()
        {
            if (RERL_Core.Window == null || RERL_Core.Camera == null) return;

            Vector2i size = RERL_Core.Window.Size;
            if (size.X <= 0 || size.Y <= 0) return;

            Builder.Use();
            Builder.Set("zNear", RERL_Core.Camera.Near);
            Builder.Set("zFar", RERL_Core.Camera.Far);
            Builder.Set("screenDimensions", size);
            Builder.RegisterAutoUniform("inverseProjection", () => Matrix4.Invert(RERL_Core.Camera.GetProjection()));
            Builder.RegisterAutoUniform("gridSize", () => GridSize);

            Lighter.Use();
            Lighter.Set("zNear", RERL_Core.Camera.Near);
            Lighter.Set("zFar", RERL_Core.Camera.Far);
            Lighter.RegisterAutoUniform("viewMatrix", () => RERL_Core.Camera.GetView());
            Lighter.RegisterAutoUniform("gridSize", () => GridSize);

            Matrix4 projection = RERL_Core.Camera.GetProjection();
            Lighter.Set("tanHalfFovX", 1f / projection[0, 0]);
            Lighter.Set("tanHalfFovY", 1f / projection[1, 1]);
        }

        internal static void AllocateClusterBuffer()
        {
            const int bytesPerCluster = sizeof(float) * 3 + sizeof(uint) + sizeof(float) * 3 + sizeof(uint) + sizeof(uint) * 128;

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _clustersSsbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, bytesPerCluster * ClusterCount, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _clustersSsbo);
        }
    }
}