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
        RERL_Core.Load();
        
        MainScene = new RCS_Core.Scene("Main");
        
        // Directional
        {
            var sun = new Entity("GlobalSun");

            sun.AddComponent(new LightComponent(LightType.Directional)
                .SetGlobal(true)
                .SetColor((1f, 1f, 1f))
                .SetIntensity(3f)
                .SetDirection(new Vector2(-105f, 3f))
            );

            MainScene.AddEntity(sun);
        }
        // Spotlight
        {
            var spotLight = new Entity("Spotlight") { Transform = { Position = new Vector3(0, 2.5f, 0) } };
            spotLight.AddComponent(new LightComponent(LightType.Spot)
                .SetPositionListener(() => spotLight.Transform.Position)
                .SetRadius(2000)
                .SetColor((1,1,1))
                .SetIntensity(12)
                .SetAngle(float.DegreesToRadians(45f))
                .SetDirection((0,0))
                .Enable(LightConfig.SmoothEdgeClamping)
            );
    
            MainScene.AddEntity(spotLight);
        }
        // Light A
        {
            var lightA = new Entity("Light A") { Transform = { Position = new Vector3(0, 2.5f, 0) } };

            lightA.AddComponent(new LightComponent(LightType.Point)
                .SetPositionListener(() => lightA.Transform.Position)
                .SetRadius(6)
                .SetColor((1,1,1))
                .SetIntensity(5f)
                .Enable(LightConfig.SmoothEdgeClamping)
            );
            
            MainScene.AddEntity(lightA);
        }
        // Light B
        {
            var lightB = new Entity("Light B") { Transform = { Position = new Vector3(0, 2.5f, 0) } };

            lightB.AddComponent(new LightComponent(LightType.Point)
                .SetPositionListener(() => lightB.Transform.Position)
                .SetRadius(6)
                .SetColor((1, 1, 1))
                .SetIntensity(2f)
                .Enable(LightConfig.SmoothEdgeClamping)
            );
            
            MainScene.AddEntity(lightB);
        }
        {
            // === Add hundreds of randomly orbiting lights ===
            Random rng = new Random(314159);

            int lightCount = 300;
            float minRadius = 3f;
            float maxRadius = 25f;
            float minSpeed  = 0.2f;
            float maxSpeed  = 1.2f;

            for (int i = 0; i < lightCount; i++)
            {
                // Random orbit radius
                float radius = MathHelper.Lerp(minRadius, maxRadius, (float)rng.NextDouble());

                // Random inclination (tilt of orbit plane)
                float inclination = MathHelper.Lerp(-MathF.PI * 0.4f, MathF.PI * 0.4f, (float)rng.NextDouble());

                // Random orbit speed
                float speed = MathHelper.Lerp(minSpeed, maxSpeed, (float)rng.NextDouble());

                // Random phase offset so they don't clump
                float phase = (float)rng.NextDouble() * MathF.Tau;

                // Random color
                Vector3 color = new Vector3(
                    0.4f + 0.6f * (float)rng.NextDouble(),
                    0.4f + 0.6f * (float)rng.NextDouble(),
                    0.4f + 0.6f * (float)rng.NextDouble()
                );

                var light = new Entity($"OrbitLight_{i}");

                light.AddComponent(new LightComponent(LightType.Point)
                    .SetPositionListener(() =>
                    {
                        float t = _time * speed + phase;

                        // Base circular orbit in XZ
                        float x = MathF.Cos(t) * radius;
                        float z = MathF.Sin(t) * radius;

                        // Apply inclination by rotating around X axis
                        float y = MathF.Sin(t * 0.5f) * 2f; // small vertical wobble
                        float zi = z * MathF.Cos(inclination) - y * MathF.Sin(inclination);
                        float yi = z * MathF.Sin(inclination) + y * MathF.Cos(inclination);

                        return new Vector3(x, yi + 2.5f, zi); // lift everything up a bit
                    })
                    .SetRadius(4f)
                    .SetColor((color.X, color.Y, color.Z))
                    .SetIntensity(1.5f)
                    .Enable(LightConfig.SmoothEdgeClamping)
                    .SetGlobal(false)
                );

                MainScene.AddEntity(light);
            }
        }
        // === LightGrid ===
        {
            int gridSize = 21;
            
            for (int gx = 0; gx < gridSize; gx++)
            {
                for (int gz = 0; gz < gridSize; gz++)
                {
                    var light = new Entity($"GridLight_{gx}_{gz}");

                    // Store grid coordinates in the entity's data dictionary
                    light.RegisterData("ix", gx);
                    light.RegisterData("iz", gz);

                    // Dynamic position listener
                    light.Transform.SetPositionListener(() =>
                    {
                        int ix = (int)light.GetData("ix");
                        int iz = (int)light.GetData("iz");

                        float spacing = float.Sin(_time * 0.5f) * 10f;

                        float x = (ix - (gridSize - 1) / 2f) * spacing;
                        float z = (iz - (gridSize - 1) / 2f) * spacing;

                        return new Vector3(x, 20f, z);
                    });

                    // Light follows transform
                    light.AddComponent(new LightComponent(LightType.Point)
                        .SetPositionListener(() => light.Transform.Position)
                        .SetRadius(3f)
                        .SetColor((1, 1, 1))
                        .SetIntensity(1.5f)
                        .Enable(LightConfig.SmoothEdgeClamping)
                    );

                    // Sphere at light position
                    light.AddComponent(new ModelRenderer()
                        .AttachModel(ModelLoader.IcosahedronMesh)
                        .AttachShader(RERL_Core.GetPrelightShader()));

                    light.Transform.Scale = new Vector3(0.15f);

                    MainScene.AddEntity(light);
                }
            }
        }
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
        RCS_Core.GetActiveScene().GetEntity("Light B").Transform.Position.X = float.Sin((_time += (float)args.Time)) * 4;
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
