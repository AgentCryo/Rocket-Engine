using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RCS;
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

    public RCS_Core.Scene MainScene;
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

        MainScene = new RCS_Core.Scene("Main");

        CreateLights();
        CreateLightGrid();
        CreatePong();
        CreateMenger();
        LoadSponza("./Models/Sponza/NewSponza_Main_glTF_003.gltf", "Sponza Main");
        LoadSponza("./Models/SponzaCurtains/NewSponza_Curtains_glTF.gltf", "Sponza Curtains");
        LoadSponza("./Models/SponzaIvy/NewSponza_IvyGrowth_glTF.gltf", "Sponza Ivy");

        RCS_Core.AddScene(MainScene);
        RCS_Core.SetActiveScene("Main");
        RCS_Core.LoadActiveScene();
    }

    void CreatePostProcessShaders()
    {
        //var testingPostProcessShader = new PostProcessShader("./Shaders/TestingPostProcess/TestingPostProcess.post");
        //RenderPipeline.RegisterPostProcess(testingPostProcessShader);
        
        var ssaoShader = new PostProcessShader("./Shaders/SSAO/SSAO.post");
        ssaoShader.RegisterAutoUniform("uProjection", () => _camera.GetProjection());
        ssaoShader.RegisterAutoUniform("uView",       () => _camera.GetView());
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
        var sun = new Entity("GlobalSun");
        sun.AddComponent(
            LightBuilder.Directional()
                .DirectionDegrees((3, 105f))
                .Intensity(1f)
                .Color(new Vector3(1f))
                .Global()
                .Build()
                .SetDirectionListener(() => new Vector2(_time * 10, _time * 10)));
        MainScene.AddEntity(sun);

        var spot = new Entity("Spotlight") { Transform = { Position = new Vector3(0, 2.5f, 0) } };
        spot.AddComponent(
            LightBuilder.Spot()
                .Radius(2000)
                .AngleDegrees(45)
                .Intensity(12)
                .Color(new Vector3(1))
                .Build()
                .SetPositionListener(() => spot.Transform.Position)
                .Enable(LightConfig.SmoothEdgeClamping));
        MainScene.AddEntity(spot);

        var lightA = new Entity("Light A") { Transform = { Position = new Vector3(0, 2.5f, 0) } };
        lightA.AddComponent(
            LightBuilder.Point()
                .Radius(6)
                .Intensity(5)
                .Color(new Vector3(1))
                .Build()
                .SetPositionListener(() => lightA.Transform.Position)
                .Enable(LightConfig.SmoothEdgeClamping));
        MainScene.AddEntity(lightA);

        var lightB = new Entity("Light B") { Transform = { Position = new Vector3(0, 2.5f, 0) } };
        lightB.AddComponent(
            LightBuilder.Point()
                .Radius(6)
                .Intensity(2)
                .Color(new Vector3(1))
                .Build()
                .SetPositionListener(() => lightB.Transform.Position)
                .Enable(LightConfig.SmoothEdgeClamping));
        MainScene.AddEntity(lightB);

        Random rng = new(314159);

        for (int i = 0; i < 1024; i++)
        {
            float radius = float.Lerp(3f, 25f, (float)rng.NextDouble());
            float inclination = float.Lerp(-float.Pi * 0.4f, float.Pi * 0.4f, (float)rng.NextDouble());
            float speed = float.Lerp(0.2f, 1.2f, (float)rng.NextDouble());
            float phase = (float)rng.NextDouble() * float.Tau;
            Vector3 color = new(
                0.4f + 0.6f * (float)rng.NextDouble(),
                0.4f + 0.6f * (float)rng.NextDouble(),
                0.4f + 0.6f * (float)rng.NextDouble());

            var light = new Entity($"OrbitLight_{i}");
            light.AddComponent(
                LightBuilder.Point()
                    .Radius(4f)
                    .Intensity(1.5f)
                    .Color(color)
                    .Global(false)
                    .Enable(LightConfig.SmoothEdgeClamping)
                    .Build()
                    .SetPositionListener(() =>
                    {
                        float t = _time * speed + phase;
                        float x = MathF.Cos(t) * radius;
                        float z = MathF.Sin(t) * radius;
                        float y = MathF.Sin(t * 0.5f) * 2f;
                        float zi = z * MathF.Cos(inclination) - y * MathF.Sin(inclination);
                        float yi = z * MathF.Sin(inclination) + y * MathF.Cos(inclination);
                        return new Vector3(x, yi + 2.5f, zi);
                    }));
            MainScene.AddEntity(light);
        }
    }

    void CreateLightGrid()
    {
        const int gridSize = 21;

        for (int gx = 0; gx < gridSize; gx++)
        for (int gz = 0; gz < gridSize; gz++)
        {
            var light = new Entity($"GridLight_{gx}_{gz}");
            light.RegisterData("ix", gx);
            light.RegisterData("iz", gz);

            light.Transform.SetPositionListener(() =>
            {
                int ix = (int)light.GetData("ix");
                int iz = (int)light.GetData("iz");
                float spacing = float.Sin(_time * 0.5f) * 10f;
                return new Vector3(
                    (ix - (gridSize - 1) / 2f) * spacing,
                    20f,
                    (iz - (gridSize - 1) / 2f) * spacing);
            });

            light.AddComponent(
                LightBuilder.Point()
                    .Radius(3f)
                    .Intensity(1.5f)
                    .Color(new Vector3(1))
                    .Build()
                    .SetPositionListener(() => light.Transform.Position)
                    .Enable(LightConfig.SmoothEdgeClamping));

            light.AddComponent(
                new ModelRenderer()
                    .AttachModel(ModelLoader.IcosahedronMesh)
                    .AttachShader(RERL_Core.GetPrelightShader()));

            light.Transform.Scale = new Vector3(0.15f);
            MainScene.AddEntity(light);
        }
    }

    void CreatePong()
    {
        MainScene.AddEntity(
            new Entity("PongIco")
                .AddComponent(new ModelRenderer()
                    .AttachModel(ModelLoader.IcosahedronMesh)
                    .AttachShader(RERL_Core.GetPrelightShader()))
                .AddComponent(new PongComponent(20, 10)));
    }

    void CreateMenger()
    {
        var menger = new Entity("MengerSpongeObject")
            .AddComponent(new ModelRenderer()
                .AttachModel(ModelLoader.CubeMesh)
                .AttachShader(_mengerSpongeObjectShader));

        menger.Transform.Position = new Vector3(5, 2.5f, 0);
        menger.Transform.Scale = new Vector3(2.5f);
        MainScene.AddEntity(menger);

        _mengerSpongeObjectShader!.Use();
        _mengerSpongeObjectShader.Set("objectPos", menger.Transform.Position);
        _mengerSpongeObjectShader.Set("objectRot", menger.Transform.Rotation);
        _mengerSpongeObjectShader.Set("objectScale", menger.Transform.Scale);
        _mengerSpongeObjectShader.Set("screenSize", Size.ToVector2());
    }

    void LoadSponza(string path, string label)
    {
        var sw = Stopwatch.StartNew();
        var models = ModelLoader.ParseMesh(path);
        Dictionary<string, Entity> entityLookup = new();

        foreach (var model in models)
        {
            var entity = new Entity(model.Name);
            if (model.Model != null)
                entity.AddComponent(new ModelRenderer()
                    .AttachModel(model.Model)
                    .AttachShader(RERL_Core.GetPrelightShader()));

            entity.Transform.SetTransform(model.Transform);
            entityLookup[model.Name] = entity;
            MainScene.AddEntity(entity);
        }

        foreach (var model in models.Where(m => !string.IsNullOrWhiteSpace(m.ParrentName)))
        {
            if (!entityLookup.TryGetValue(model.Name, out var child) ||
                !entityLookup.TryGetValue(model.ParrentName, out var parent))
                continue;

            child.Transform.SetParent(parent.Transform);
        }

        sw.Stop();
        Logger.Log($"[PERF] {label} model load time: {sw.ElapsedMilliseconds} ms");
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        _cameraController.UpdateInput(args.Time);
        RCS_Core.UpdateActiveScene(args.Time);
        _time += (float)args.Time;
        base.OnUpdateFrame(args);
    }

    //protected override void OnRenderFrame(FrameEventArgs args)
    //{
    //    base.OnRenderFrame(args);
//
    //    if (_ssao != null)
    //    {
    //        _ssao.Shader.Use();
    //        _ssao.Shader.Set("uProjection", _camera.GetProjection());
    //        _ssao.Shader.Set("uView", _camera.GetView());
    //    }
//
    //    RERL_Core.RenderFrame(args);
    //    SwapBuffers();
    //}
    
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        RERL_Core.RenderFrame(args);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
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
}
