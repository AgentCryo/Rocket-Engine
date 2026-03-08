using System.Drawing;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RERL;
using RERL.Loaders;
using RERL.Objects;

namespace Dev;

public class Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
    : GameWindow(gameWindowSettings, nativeWindowSettings)
{
    Camera _camera = new Camera();
    Dictionary<string, RERL_Core.Mesh> _meshes = new();
    
    protected override void OnLoad()
    {
        base.OnLoad();
        _camera.SetProjectionFovXInDegrees(90, Size.X / (float)Size.Y, 0.1f, 100f);

        _meshes.Add("Icosahedron", MeshLoader.ParseMesh(@"./Models/Icosahedron.obj"));
        
        RERL_Core.Load();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        RERL.RERL_Core.RenderFrame(this, _camera, args);
    }
    
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        _camera.SetProjectionFovXInDegrees(90, Size.X / (float)Size.Y, 0.1f, 100f);
    }
}