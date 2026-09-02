using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RCS;
using RCS.Component_Engine;
using RCS.Components;
using RERL;
using RERL.Components;
using RERL.Loaders;
using RERL.Shader_Engine;

namespace Dev;

// dotnet publish -p:PublishProfile=Win64

public class Game : GameWindow
{
    readonly Camera _camera = new();
    readonly CameraController _cameraController = new();
    GraphicsShader? _mengerSpongeObjectShader;
    World MainWorld = null!;
    float _time;
    static DebugProc DebugMessageDelegate = OnDebugMessage;

    public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings) { }

    protected override void OnLoad()
    {
        base.OnLoad();
        Logger.Initialize(createLogFile: false, outputToConsole: true);
        GL.DebugMessageCallback(DebugMessageDelegate, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        _camera.SetProjectionFovXInDegrees(90, Size.X / (float)Size.Y, 0.1f, 100f);
        CursorState = CursorState.Grabbed;
        _cameraController.InitializeCameraController(_camera, KeyboardState, MouseState, this);
        RERL_Core.SetCamera(_camera);
        RERL_Core.SetGameWindow(this);
        GL.ClearColor(Color.FromArgb(255, 20, 25, 35));
        RERL_Core.Load();

        CreatePostProcessShaders();
        CreateMengerShader();

        MainWorld = new World();
        RCS_Core.AddWorld("Main", MainWorld);
        RCS_Core.SetActiveWorld("Main");

        CreateLights();
        CreatePong();
        CreateMenger();
        LoadSponza("./Models/Sponza/NewSponza_Main_glTF_003.gltf", "Sponza Main");
        LoadSponza("./Models/SponzaCurtains/NewSponza_Curtains_glTF.gltf", "Sponza Curtains");
        LoadSponza("./Models/SponzaIvy/NewSponza_IvyGrowth_glTF.gltf", "Sponza Ivy");
        
        //BenchmarkEntityLookup();
        BenchmarkECS();
    }

    void CreatePostProcessShaders()
    {
        // var testingPostProcessShader = new PostProcessShader("./Shaders/TestingPostProcess/TestingPostProcess.post");
        // RenderPipeline.RegisterPostProcess(testingPostProcessShader);

        var ssaoShader = new PostProcessShader("./Shaders/SSAO/SSAO.post");
        ssaoShader.RegisterAutoUniform("uProjection", () => _camera.GetProjection());
        ssaoShader.RegisterAutoUniform("uView", () => _camera.GetView());
        RenderPipeline.RegisterPostProcess(ssaoShader);
    }

    void CreateMengerShader()
    {
        _mengerSpongeObjectShader = new GraphicsShader("./RocketEngine/Shaders/Templates/Default/default.vert", "./Shaders/MengerSpongeObject/mengerSpongeObject.frag");
        _mengerSpongeObjectShader.RegisterAutoUniform("cameraPos", () => _cameraController.GetPosition());
        _mengerSpongeObjectShader.RegisterAutoUniform("cameraRot", () => _cameraController.GetOrientation());
    }

    void CreateLights()
    {
        var sun = MainWorld.CreateEntity("GlobalSun");
        MainWorld.Add(sun, new Transform());
        MainWorld.Add(sun, LightBuilder.Directional().DirectionDegrees((3, 105f)).Intensity(1f).Color(new Vector3(1f)).Global().Build());
    
        var spot = MainWorld.CreateEntity("Spotlight");
        MainWorld.Add(spot, new Transform());
        MainWorld.Get<Transform>(spot).Position = new Vector3(0, 2.5f, 0);
        MainWorld.Add(spot, LightBuilder.Spot().Radius(2000).AngleDegrees(45).Intensity(12).Color(new Vector3(1)).Build().Enable(LightConfig.SmoothEdgeClamping));
    
        var lightA = MainWorld.CreateEntity("Light A");
        MainWorld.Add(lightA, new Transform());
        MainWorld.Get<Transform>(lightA).Position = new Vector3(0, 2.5f, 0);
        MainWorld.Add(lightA, LightBuilder.Point().Radius(6).Intensity(5).Color(new Vector3(1)).Build().Enable(LightConfig.SmoothEdgeClamping));
    
        var lightB = MainWorld.CreateEntity("Light B");
        MainWorld.Add(lightB, new Transform());
        MainWorld.Get<Transform>(lightB).Position = new Vector3(0, 2.5f, 0);
        MainWorld.Add(lightB, LightBuilder.Point().Radius(6).Intensity(2).Color(new Vector3(1)).Build().Enable(LightConfig.SmoothEdgeClamping));
    
        Random rng = new(314159);

        for (int i = 0; i < 1024; i++)
        {
            float radius = float.Lerp(5f, 35f, (float)rng.NextDouble());
            float height = float.Lerp(2f, 8f, (float)rng.NextDouble());
            float inclination = float.Lerp(-0.35f, 0.35f, (float)rng.NextDouble());
            float speed = float.Lerp(0.15f, 0.8f, (float)rng.NextDouble());
            float verticalMotion = float.Lerp(0.5f, 2.5f, (float)rng.NextDouble());
            float verticalSpeed = float.Lerp(0.3f, 1.5f, (float)rng.NextDouble());
            float phase = (float)rng.NextDouble() * float.Tau;

            Vector3 color = new(0.4f + 0.6f * (float)rng.NextDouble(), 0.4f + 0.6f * (float)rng.NextDouble(), 0.4f + 0.6f * (float)rng.NextDouble());

            var light = MainWorld.CreateEntity($"OrbitLight_{i}");

            MainWorld.Add(light, new Transform());

            MainWorld.Add(light, new OrbitComponent(radius, height, inclination, speed, verticalMotion, verticalSpeed, phase));

            MainWorld.Add(light,
                LightBuilder.Point()
                    .Radius(4f)
                    .Intensity(1.5f)
                    .Color(color)
                    .Global(false)
                    .Enable(LightConfig.SmoothEdgeClamping)
                    .Build());
        }
    }

    void CreatePong()
    {
        var entity = MainWorld.CreateEntity("PongIco");
        MainWorld.Add(entity, new Transform());

        MainWorld.Add(entity, new ModelRenderer().AttachModel(ModelLoader.IcosahedronMesh).AttachShader(RERL_Core.GetPrelightShader()));
        MainWorld.Add(entity, new PongComponent(20, 10));
    }

    void CreateMenger()
    {
        var menger = MainWorld.CreateEntity("MengerSpongeObject");
        MainWorld.Add(menger, new Transform());

        MainWorld.Add(menger, new ModelRenderer().AttachModel(ModelLoader.CubeMesh).AttachShader(_mengerSpongeObjectShader));

        MainWorld.Get<Transform>(menger).Position = new Vector3(5, 2.5f, 0);
        MainWorld.Get<Transform>(menger).Scale = new Vector3(2.5f);

        _mengerSpongeObjectShader!.Use();
        _mengerSpongeObjectShader.RegisterAutoUniform("objectPos", () => MainWorld.Get<Transform>(menger).Position);
        _mengerSpongeObjectShader.RegisterAutoUniform("objectRot", () => MainWorld.Get<Transform>(menger).Rotation);
        _mengerSpongeObjectShader.RegisterAutoUniform("objectScale", () => MainWorld.Get<Transform>(menger).Scale);
        _mengerSpongeObjectShader.Set("screenSize", Size.ToVector2());
    }

    void LoadSponza(string path, string label)
    {
        var sw = Stopwatch.StartNew();
        var models = ModelLoader.ParseMesh(path);
        Dictionary<string, Entity> entityLookup = new();

        foreach (var model in models)
        {
            var entity = MainWorld.CreateEntity(model.Name);
            MainWorld.Add(entity, new Transform());

            if (model.Model != null)
                MainWorld.Add(entity, new ModelRenderer().AttachModel(model.Model).AttachShader(RERL_Core.GetPrelightShader()));

            var transform = MainWorld.Get<Transform>(entity);
            transform.SetTransform(model.Transform);
            entityLookup[model.Name] = entity;
        }

        foreach (var model in models.Where(m => !string.IsNullOrWhiteSpace(m.ParrentName)))
        {
            if (!entityLookup.TryGetValue(model.Name, out var child) || !entityLookup.TryGetValue(model.ParrentName, out var parent))
                continue;

            MainWorld.SetParent(child, parent);
        }

        sw.Stop();
        Logger.Log($"[PERF] {label} model load time: {sw.ElapsedMilliseconds} ms");
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        _cameraController.UpdateInput(args.Time);
        _time += (float)args.Time;
        
        var mengerTransform = MainWorld.Get<Transform>(MainWorld.FindEntity("MengerSpongeObject"));
        float degreesPerSecond = 30f;
        mengerTransform.Rotation = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(degreesPerSecond) * (float)args.Time) * mengerTransform.Rotation;
        
        MainWorld.Update((float)args.Time);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        RERL_Core.RenderFrame(args);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        if (Size.Y > 0)
            _camera.SetProjectionFovXInDegrees(90, Size.X / (float)Size.Y, 0.1f, 100f);

        _mengerSpongeObjectShader?.Use();
        _mengerSpongeObjectShader?.Set("screenSize", Size.ToVector2());
        RERL_Core.Resize();
    }

    static void OnDebugMessage(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr pMessage, IntPtr pUserParam)
    {
        string message = Marshal.PtrToStringAnsi(pMessage, length);
        Console.WriteLine("[{0} source={1} type={2} id={3}] {4}", severity, source, type, id, message);
        if (type == DebugType.DebugTypeError)
            throw new Exception(message);
    }
    
    void BenchmarkECS()
    {
        //const int iterations = 10_000_000;
    //
        //var entity = MainWorld.FindEntity("MengerSpongeObject");
        //var transformStore = GetTransformStore();
    //
        //var entities = transformStore.Entities;
        //var components = transformStore.Data;
    //
        //Logger.Log("[BENCH] ========================================");
        //Logger.Log("[BENCH] ECS PERFORMANCE");
        //Logger.Log("[BENCH] ========================================");
    //
        //Logger.Log("[BENCH] ");
        //Logger.Log("[BENCH] -------- SAFE API --------");
        //Logger.Log("[BENCH] ");
    //
        //BenchmarkWorldGet(entity, iterations);
        //BenchmarkComponentStoreGet(transformStore, entity, iterations);
    //
        //Logger.Log("[BENCH] ");
        //Logger.Log("[BENCH] -------- OPTIMIZED API --------");
        //Logger.Log("[BENCH] ");
    //
        //BenchmarkWorldUncheckedGet(entity, iterations);
        //BenchmarkComponentStoreUncheckedGet(transformStore, entity, iterations);
        //BenchmarkStoreAccess(entity, iterations);
    //
        //Logger.Log("[BENCH] ");
        //Logger.Log("[BENCH] -------- RAW / QUERY --------");
        //Logger.Log("[BENCH] ");
    //
        //BenchmarkRawArray(components, iterations);
        //BenchmarkQuery(entities, iterations);
    //
        //Logger.Log("[BENCH] ");
        //Logger.Log("[BENCH] ========================================");
    }
    
    void BenchmarkWorldGet(Entity entity, int iterations)
    {
        for (var i = 0; i < 1_000_000; i++)
            _ = MainWorld.Get<Transform>(entity);
    
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    
        var start = Stopwatch.GetTimestamp();
    
        Transform? result = null;
    
        for (var i = 0; i < iterations; i++)
            result = MainWorld.Get<Transform>(entity);
    
        var elapsed = Stopwatch.GetTimestamp() - start;
    
        GC.KeepAlive(result);
    
        LogBenchmark(
            "World.Get<Transform>()",
            elapsed,
            iterations);
    }
    
    void BenchmarkComponentStoreGet(
        ComponentStore<Transform> store,
        Entity entity,
        int iterations)
    {
        for (var i = 0; i < 1_000_000; i++)
            _ = store.Get(entity);
    
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    
        var start = Stopwatch.GetTimestamp();
    
        Transform? result = null;
    
        for (var i = 0; i < iterations; i++)
            result = store.Get(entity);
    
        var elapsed = Stopwatch.GetTimestamp() - start;
    
        GC.KeepAlive(result);
    
        LogBenchmark(
            "ComponentStore.Get<Transform>()",
            elapsed,
            iterations);
    }
    
    void BenchmarkWorldUncheckedGet(Entity entity, int iterations)
    {
        for (var i = 0; i < 1_000_000; i++)
            _ = MainWorld.GetUnchecked<Transform>(entity);
    
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    
        var start = Stopwatch.GetTimestamp();
    
        Transform? result = null;
    
        for (var i = 0; i < iterations; i++)
            result = MainWorld.GetUnchecked<Transform>(entity);
    
        var elapsed = Stopwatch.GetTimestamp() - start;
    
        GC.KeepAlive(result);
    
        LogBenchmark(
            "World.GetUnchecked<Transform>()",
            elapsed,
            iterations);
    }
    
    void BenchmarkComponentStoreUncheckedGet(
        ComponentStore<Transform> store,
        Entity entity,
        int iterations)
    {
        for (var i = 0; i < 1_000_000; i++)
            _ = store.GetUnchecked(entity);
    
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    
        var start = Stopwatch.GetTimestamp();
    
        Transform? result = null;
    
        for (var i = 0; i < iterations; i++)
            result = store.GetUnchecked(entity);
    
        var elapsed = Stopwatch.GetTimestamp() - start;
    
        GC.KeepAlive(result);
    
        LogBenchmark(
            "ComponentStore.GetUnchecked<Transform>()",
            elapsed,
            iterations);
    }
    
    void BenchmarkStoreAccess(Entity entity, int iterations)
    {
        var store = MainWorld.Store<Transform>();
    
        for (var i = 0; i < 1_000_000; i++)
            _ = store.GetUnchecked(entity);
    
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    
        var start = Stopwatch.GetTimestamp();
    
        Transform? result = null;
    
        for (var i = 0; i < iterations; i++)
            result = store.GetUnchecked(entity);
    
        var elapsed = Stopwatch.GetTimestamp() - start;
    
        GC.KeepAlive(result);
    
        LogBenchmark(
            "Cached World.Store<Transform>() + GetUnchecked()",
            elapsed,
            iterations);
    }
    
    void BenchmarkRawArray(
        ReadOnlySpan<Transform> components,
        int iterations)
    {
        if (components.Length == 0)
            return;
    
        var index = 0;
    
        for (var i = 0; i < 1_000_000; i++)
            _ = components[index];
    
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    
        var start = Stopwatch.GetTimestamp();
    
        Transform? result = null;
    
        for (var i = 0; i < iterations; i++)
            result = components[index];
    
        var elapsed = Stopwatch.GetTimestamp() - start;
    
        GC.KeepAlive(result);
    
        LogBenchmark(
            "Raw component array lookup",
            elapsed,
            iterations);
    }
    
    void BenchmarkQuery(
        ReadOnlySpan<Entity> entities,
        int iterations)
    {
        if (entities.Length == 0)
            return;
    
        var passes = Math.Max(
            1,
            iterations / entities.Length);
    
        var operations = (long)passes * entities.Length;
    
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    
        long accumulator = 0;
    
        var start = Stopwatch.GetTimestamp();
    
        for (var i = 0; i < passes; i++)
        {
            for (var j = 0; j < entities.Length; j++)
                accumulator += entities[j].Id;
        }
    
        var elapsed = Stopwatch.GetTimestamp() - start;
    
        GC.KeepAlive(accumulator);
    
        LogBenchmark(
            $"Query entity iteration ({entities.Length} entities)",
            elapsed,
            operations);
    }
    
    void LogBenchmark(
        string name,
        long elapsedTicks,
        long operations)
    {
        var seconds =
            elapsedTicks / (double)Stopwatch.Frequency;
    
        var milliseconds =
            seconds * 1_000.0;
    
        var nanoseconds =
            seconds * 1_000_000_000.0;
    
        var nanosecondsPerOperation =
            nanoseconds / operations;
    
        Logger.Log($"[BENCH] {name}");
        Logger.Log($"[BENCH] Operations: {operations:N0}");
        Logger.Log($"[BENCH] Total: {milliseconds:F3} ms");
        Logger.Log($"[BENCH] Per operation: {nanosecondsPerOperation:F3} ns");
    }
    
    ComponentStore<Transform> GetTransformStore()
    {
        var field = typeof(World).GetField(
            "_componentStores",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
    
        var stores =
            (IComponentStore[])field!.GetValue(MainWorld)!;
    
        return (ComponentStore<Transform>)stores[
            ComponentType<Transform>.Id];
    }
}