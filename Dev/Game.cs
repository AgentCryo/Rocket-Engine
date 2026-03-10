using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
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
    
    static MeshRenderer _cube;
    static MeshRenderer _icosahedron;
    static MeshRenderer _sphere;

    protected override void OnLoad()
    {
        base.OnLoad();
        _camera.SetProjectionFovXInDegrees(100, Size.X / (float)Size.Y, 0.1f, 100f);
        CursorState = CursorState.Grabbed;
        _cameraController.InitializeCameraController(_camera, KeyboardState, MouseState, this);
        
        RERL_Core.Load(_camera, this);

        _cube = new MeshRenderer().AttachMesh(MeshLoader.CubeMesh).AttachShader(RERL_Core.GetDefaultShader());
        RERL_Core.RegisterRenderable(_cube);

        _icosahedron = new MeshRenderer().AttachMesh(MeshLoader.IcosahedronMesh).AttachShader(RERL_Core.GetDefaultShader());
        RERL_Core.RegisterRenderable(_icosahedron);

        _sphere = new MeshRenderer().AttachMesh(MeshLoader.UVSphereMesh).AttachShader(RERL_Core.GetDefaultShader());
        RERL_Core.RegisterRenderable(_sphere);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        _cameraController.UpdateInput(args.Time);
        base.OnUpdateFrame(args);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        RERL_Core.RenderFrame(this, _camera, args);
    }
    
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        _camera.SetProjectionFovXInDegrees(90, Size.X / (float)Size.Y, 0.1f, 100f);
    }
}