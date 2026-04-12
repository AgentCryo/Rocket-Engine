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
using RERL.ShaderTypes;
using OpenTK.Graphics.OpenGL4;
using System.Diagnostics;
using bottlenoselabs.C2CS.Runtime;

namespace Dev;

// dotnet publish -p:PublishProfile=Win64

public class Game : GameWindow
{
    Camera _camera = new Camera();
    CameraController _cameraController = new CameraController();
    
    static Shader _fadeTest;
    static Shader _mengerSpongeObjectShader = new();
    static PostProcess _testingPostProcess = new();

    public RCS_Core.Scene MainScene;

    public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings) { }
    
    static DebugProc DebugMessageDelegate = OnDebugMessage;
    
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
        
        _fadeTest = new Shader().AttachGraphicsShader(Shader.DefaultVert, "./Shaders/FadeTest/fadeTest.frag", Shader.ShaderType.Prelight);
        RenderPipeline.RegisterShader(_fadeTest);
        
        _mengerSpongeObjectShader = new Shader().AttachGraphicsShader(Shader.DefaultVert, "./Shaders/MengerSpongeObject/mengerSpongeObject.frag", Shader.ShaderType.Shader);
        _mengerSpongeObjectShader.Use();
        _mengerSpongeObjectShader.RegisterAutoUniform("cameraPos", () => _cameraController.GetPosition());
        _mengerSpongeObjectShader.RegisterAutoUniform("cameraRot", () => _cameraController.GetOrientation());
        _mengerSpongeObjectShader.ApplyUniform("screenSize", Size.ToVector2());
        RenderPipeline.RegisterShader(_mengerSpongeObjectShader);
        
        _testingPostProcess = new PostProcess().AttachPostProcessShader("./Shaders/TestingPostProcess/testingPostProcess.post", this);
        RenderPipeline.RegisterPostProcess(_testingPostProcess);
        
        RERL_Core.SetCamera(_camera);
        RERL_Core.SetGameWindow(this);
        GL.ClearColor(Color.FromArgb(255, 20,25,35));
        RERL_Core.Load();
        
        MainScene = new RCS_Core.Scene("Main");
        
        // Directional
        {
            var sun = new Entity("GlobalSun");

            sun.AddComponent(
                LightBuilder.Directional()
                    .DirectionDegrees((3, 105f))
                    .Intensity(1f)
                    .Color(new Vector3(1f))
                    .Global()
                    .Build()
                    .SetDirectionListener(() => new Vector2(_time * 10, _time * 10))
            );

            MainScene.AddEntity(sun);
        }
        
        //// Spotlight
        //{
        //    var spot = new Entity("Spotlight") { Transform = { Position = new Vector3(0, 2.5f, 0) } };
//
        //    spot.AddComponent(
        //        LightBuilder.Spot()
        //            .Radius(2000)
        //            .AngleDegrees(45)
        //            .Intensity(12)
        //            .Color(new Vector3(1))
        //            .Build()
        //            .SetPositionListener(() => spot.Transform.Position)
        //            .Enable(LightConfig.SmoothEdgeClamping)
        //    );
//
        //    MainScene.AddEntity(spot);
        //}
        //// Light A
        //{
        //    var lightA = new Entity("Light A") { Transform = { Position = new Vector3(0, 2.5f, 0) } };
//
        //    lightA.AddComponent(
        //        LightBuilder.Point()
        //            .Radius(6)
        //            .Intensity(5)
        //            .Color(new Vector3(1))
        //            .Build()
        //            .SetPositionListener(() => lightA.Transform.Position)
        //            .Enable(LightConfig.SmoothEdgeClamping)
        //    );
//
        //    MainScene.AddEntity(lightA);
        //}
        //// Light B
        //{
        //    var lightB = new Entity("Light B") { Transform = { Position = new Vector3(0, 2.5f, 0) } };
//
        //    lightB.AddComponent(
        //        LightBuilder.Point()
        //            .Radius(6)
        //            .Intensity(2)
        //            .Color(new Vector3(1))
        //            .Build()
        //            .SetPositionListener(() => lightB.Transform.Position)
        //            .Enable(LightConfig.SmoothEdgeClamping)
        //    );
//
        //    MainScene.AddEntity(lightB);
        //}
        //{ // Light orbit things.
        //    Random rng = new Random(314159);
//
        //    int   count = 300;
        //    float minR = 3f, maxR = 25f;
        //    float minS = 0.2f, maxS = 1.2f;
//
        //    for (int i = 0; i < count; i++)
        //    {
        //        float radius      = float.Lerp(minR, maxR, (float)rng.NextDouble());
        //        float inclination = float.Lerp(-float.Pi * 0.4f, float.Pi * 0.4f, (float)rng.NextDouble());
        //        float speed       = float.Lerp(minS, maxS, (float)rng.NextDouble());
        //        float phase       = (float)rng.NextDouble() * float.Tau;
//
        //        Vector3 color = new Vector3(
        //            0.4f + 0.6f * (float)rng.NextDouble(),
        //            0.4f + 0.6f * (float)rng.NextDouble(),
        //            0.4f + 0.6f * (float)rng.NextDouble()
        //        );
//
        //        var light = new Entity($"OrbitLight_{i}");
//
        //        light.AddComponent(
        //            LightBuilder.Point()
        //                .Radius(4f)
        //                .Intensity(1.5f)
        //                .Color(color)
        //                .Global(false)
        //                .Enable(LightConfig.SmoothEdgeClamping)
        //                .Build()
        //                .SetPositionListener(() =>
        //                {
        //                    float t = _time * speed + phase;
//
        //                    float x = MathF.Cos(t) * radius;
        //                    float z = MathF.Sin(t) * radius;
//
        //                    float y  = MathF.Sin(t * 0.5f) * 2f;
        //                    float zi = z * MathF.Cos(inclination) - y * MathF.Sin(inclination);
        //                    float yi = z * MathF.Sin(inclination) + y * MathF.Cos(inclination);
//
        //                    return new Vector3(x, yi + 2.5f, zi);
        //                })
        //        );
//
        //        MainScene.AddEntity(light);
        //    }
        //}
        //// === LightGrid ===
        //{
        //    int gridSize = 21;
//
        //    for (int gx = 0; gx < gridSize; gx++)
        //    for (int gz = 0; gz < gridSize; gz++)
        //    {
        //        var light = new Entity($"GridLight_{gx}_{gz}");
//
        //        light.RegisterData("ix", gx);
        //        light.RegisterData("iz", gz);
//
        //        light.Transform.SetPositionListener(() =>
        //        {
        //            int ix = (int)light.GetData("ix");
        //            int iz = (int)light.GetData("iz");
//
        //            float spacing = float.Sin(_time * 0.5f) * 10f;
//
        //            float x = (ix - (gridSize - 1) / 2f) * spacing;
        //            float z = (iz - (gridSize - 1) / 2f) * spacing;
//
        //            return new Vector3(x, 20f, z);
        //        });
//
        //        light.AddComponent(
        //            LightBuilder.Point()
        //                .Radius(3f)
        //                .Intensity(1.5f)
        //                .Color(new Vector3(1))
        //                .Build()
        //                .SetPositionListener(() => light.Transform.Position)
        //                .Enable(LightConfig.SmoothEdgeClamping)
        //        );
//
        //        light.AddComponent(
        //            new ModelRenderer()
        //                .AttachModel(ModelLoader.IcosahedronMesh)
        //                .AttachShader(RERL_Core.GetPrelightShader())
        //        );
//
        //        light.Transform.Scale = new Vector3(0.15f);
//
        //        MainScene.AddEntity(light);
        //    }
        //}
        //// Pong Ico Object
        //{
        //    MainScene.AddEntity(new Entity("PongIco")
        //        .AddComponent(new ModelRenderer()
        //            .AttachModel(ModelLoader.IcosahedronMesh)
        //            .AttachShader(RERL_Core.GetPrelightShader()))
        //        .AddComponent(new PongComponent(20, 10))
        //    );
        //}
        //// Menger Sponge Object
        //{
        //    var menger = new Entity("MengerSpongeObject")
        //        .AddComponent(new ModelRenderer()
        //            .AttachModel(ModelLoader.CubeMesh)
        //            .AttachShader(_mengerSpongeObjectShader));
        //
        //    menger.Transform.Position = new Vector3(5, 2.5f, 0);
        //    menger.Transform.Scale = new Vector3(2.5f, 2.5f, 2.5f);
        //    MainScene.AddEntity(menger);
        //
        //    _mengerSpongeObjectShader.RegisterAutoUniform("objectPos", () => menger.Transform.Position);
        //    _mengerSpongeObjectShader.RegisterAutoUniform(
        //        "objectRot",
        //        () => menger.Transform.Rotation
        //    );
        //    _mengerSpongeObjectShader.RegisterAutoUniform("objectScale", () => menger.Transform.Scale);
        //}
        {
            var sw = Stopwatch.StartNew();

            var glbSponzaModels = ModelLoader.ParseMesh("./Models/Sponza/NewSponza_Main_glTF_003.gltf");
            Dictionary<string, Entity> sponzaEntityLookup = new();

            foreach (var m in glbSponzaModels) {
                var ent = new Entity(m.Name);

                if (m.Model != null) {
                    ent.AddComponent(new ModelRenderer()
                        .AttachModel(m.Model)
                        .AttachShader(RERL_Core.GetPrelightShader()));
                }

                ent.Transform.SetTransform(m.Transform);

                sponzaEntityLookup[m.Name] = ent;
                MainScene.AddEntity(ent);
            }

            foreach (var m in glbSponzaModels.Where(m => !string.IsNullOrWhiteSpace(m.ParrentName))) {
                if (!sponzaEntityLookup.TryGetValue(m.Name, out var child))
                    continue;

                if (!sponzaEntityLookup.TryGetValue(m.ParrentName, out var parent))
                    continue;

                child.Transform.SetParent(parent.Transform);
            }

            sw.Stop();
            Logger.Log($"[PERF] Sponza Curtains model load time: {sw.ElapsedMilliseconds} ms");
        }
        {
            var sw = Stopwatch.StartNew();

            var glbSponzaModels = ModelLoader.ParseMesh("./Models/SponzaCurtains/NewSponza_Curtains_glTF.gltf");
            Dictionary<string, Entity> sponzaEntityLookup = new();

            foreach (var m in glbSponzaModels) {
                var ent = new Entity(m.Name);

                if (m.Model != null) {
                    ent.AddComponent(new ModelRenderer()
                        .AttachModel(m.Model)
                        .AttachShader(RERL_Core.GetPrelightShader()));
                }

                ent.Transform.SetTransform(m.Transform);

                sponzaEntityLookup[m.Name] = ent;
                MainScene.AddEntity(ent);
            }

            foreach (var m in glbSponzaModels.Where(m => !string.IsNullOrWhiteSpace(m.ParrentName))) {
                if (!sponzaEntityLookup.TryGetValue(m.Name, out var child))
                    continue;

                if (!sponzaEntityLookup.TryGetValue(m.ParrentName, out var parent))
                    continue;

                child.Transform.SetParent(parent.Transform);
            }

            sw.Stop();
            Logger.Log($"[PERF] Sponza Curtains model load time: {sw.ElapsedMilliseconds} ms");
        }
        {
            var sw = Stopwatch.StartNew();
            var glbSponzaModels = ModelLoader.ParseMesh("./Models/SponzaIvy/NewSponza_IvyGrowth_glTF.gltf");
            Dictionary<string, Entity> sponzaEntityLookup = new();

            foreach (var m in glbSponzaModels) {
                var ent = new Entity(m.Name);

                if (m.Model != null) {
                    ent.AddComponent(new ModelRenderer()
                        .AttachModel(m.Model)
                        .AttachShader(RERL_Core.GetPrelightShader()));
                }

                ent.Transform.SetTransform(m.Transform);

                sponzaEntityLookup[m.Name] = ent;
                MainScene.AddEntity(ent);
            }

            foreach (var m in glbSponzaModels.Where(m => !string.IsNullOrWhiteSpace(m.ParrentName))) {
                if (!sponzaEntityLookup.TryGetValue(m.Name, out var child))
                    continue;

                if (!sponzaEntityLookup.TryGetValue(m.ParrentName, out var parent))
                    continue;

                child.Transform.SetParent(parent.Transform);
            }

            sw.Stop();
            Logger.Log($"[PERF] Sponza Ivy model load time: {sw.ElapsedMilliseconds} ms");
        }

        RCS_Core.AddScene(MainScene);
        RCS_Core.SetActiveScene("Main");
        RCS_Core.LoadActiveScene();
    }

    float _time;
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        _cameraController.UpdateInput(args.Time);
        RCS_Core.UpdateActiveScene(args.Time);
        _time += (float)args.Time;
        //RCS_Core.GetActiveScene().GetEntity("Light B").Transform.Position.X = float.Sin((_time += (float)args.Time)) * 4;
        base.OnUpdateFrame(args);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        RERL_Core.RenderFrame(args);
    }
    
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        _camera.SetProjectionFovXInDegrees(90, Size.X / (float)Size.Y, 0.1f, 100f);
        _mengerSpongeObjectShader.Use();
        _mengerSpongeObjectShader.ApplyUniform("screenSize", Size.ToVector2());
        RERL_Core.Resize();
    }
    
    static void OnDebugMessage(
        DebugSource source,     // Source of the debugging message.
        DebugType type,         // Type of the debugging message.
        int id,                 // ID associated with the message.
        DebugSeverity severity, // Severity of the message.
        int length,             // Length of the string in pMessage.
        IntPtr pMessage,        // Pointer to message string.
        IntPtr pUserParam)      // The pointer you gave to OpenGL, explained later.
    {
        // In order to access the string pointed to by pMessage, you can use Marshal
        // class to copy its contents to a C# string without unsafe code. You can
        // also use the new function Marshal.PtrToStringUTF8 since .NET Core 1.1.
        string message = Marshal.PtrToStringAnsi(pMessage, length);

        // The rest of the function is up to you to implement, however a debug output
        // is always useful.
        Console.WriteLine("[{0} source={1} type={2} id={3}] {4}", severity, source, type, id, message);

        // Potentially, you may want to throw from the function for certain severity
        // messages.
        if (type == DebugType.DebugTypeError)
        {
            throw new Exception(message);
        }
    }
}
