using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RCS;
using RERL;
using RERL.Loaders;
using RERL.Objects;

namespace Dev;

//dotnet publish -p:PublishProfile=Win64

public class Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : GameWindow(gameWindowSettings, nativeWindowSettings)
{
    Camera _camera = new Camera();
    CameraController _cameraController = new CameraController();
    
    static Shader _fadeTest;
    static PostProcess _postProcess = new();
    
    protected override void OnLoad()
    {
        base.OnLoad();
        _camera.SetProjectionFovXInDegrees(90, Size.X / (float)Size.Y, 0.1f, 100f);
        CursorState = CursorState.Grabbed;
        _cameraController.InitializeCameraController(_camera, KeyboardState, MouseState, this);
        
        _fadeTest = new Shader().AttachShader(Shader.DefaultVert, "./Shaders/FadeTest/fadeTest.frag");
        RERL_Core.RegisterShader(_fadeTest);
        
        _postProcess = new PostProcess().AttachPostProcessShader("./Shaders/MergerSponge/mergerSponge.frag", this);
        _postProcess.RegisterAutoUniform("cameraPos", () => _cameraController.GetPosition());
        _postProcess.RegisterAutoUniform("cameraRot", () => _cameraController.GetOrientation());
        RERL_Core.RegisterPostProcess(_postProcess);
        
        RERL_Core.SetCamera(_camera);
        RERL_Core.SetGameWindow(this);
        RERL_Core.Load();
        
        RCS_Core.AddScene(
            new RCS_Core.Scene("Main")
                .AddEntity(new Entity("Cube")
                    .AddComponent(new MeshRenderer().SetAutoRegister(false).AttachMesh(MeshLoader.CubeMesh).AttachShader(_fadeTest))
                    .AddComponent(Transform.Identity.SetPosition(new Vector3(0, 0.0f, 0)))
                    .AddComponent<CubeComponent>())
                
                .AddEntity(new Entity("Icosahedron")
                    .AddComponent(new MeshRenderer().SetAutoRegister(false).AttachMesh(MeshLoader.IcosahedronMesh).AttachShader(RERL_Core.GetDefaultShader()))
                    .AddComponent(Transform.Identity.SetPosition(new Vector3(0, 2.2f, 0)))
                    .AddComponent(new PongComponent(5, 5)))
                
                .AddEntity(new Entity("Sphere")
                    .AddComponent(new MeshRenderer().AttachMesh(MeshLoader.UVSphereMesh).AttachShader(RERL_Core.GetDefaultShader()))
                    .AddComponent(Transform.Identity.SetPosition(new Vector3(0, 0.0f, 0))))
            );
        RCS_Core.SetActiveScene("Main");
        RERL_Core.RegisterRenderable(RCS_Core.GetActiveScene().GetEntity("Icosahedron").GetComponent<MeshRenderer>());  //Get Component Examples
        RERL_Core.RegisterRenderable(RCS_Core.GetActiveScene().GetComponentFromEntity<MeshRenderer>("Cube"));           //Get Component Examples
        RCS_Core.LoadActiveScene();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        _cameraController.UpdateInput(args.Time);
        RCS_Core.UpdateActiveScene(args.Time);
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
    }
}