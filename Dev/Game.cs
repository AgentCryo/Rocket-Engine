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
    Dictionary<string, RERL_Core.Mesh> _meshes = new();
    
    protected override void OnLoad()
    {
        base.OnLoad();
        _camera.SetProjectionFovXInDegrees(100, Size.X / (float)Size.Y, 0.1f, 100f);
        CursorState = CursorState.Grabbed;
        _cameraController.InitializeCameraController(_camera, KeyboardState, MouseState, this);

        //_meshes.Add("Icosahedron", MeshLoader.ParseMesh(MeshLoader.Icosahedron));
        //_meshes.Add("Sphere", MeshLoader.ParseMesh(MeshLoader.UVSphere));
        //_meshes.Add("Cube", MeshLoader.ParseMesh(MeshLoader.Cube));
        
        RERL_Core.Load(_camera, this);
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