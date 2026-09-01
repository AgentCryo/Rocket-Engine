using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RERL.Shader_Engine;
using static RERL.RenderPipeline;

namespace RERL;

public static class RERL_Core
{
    static GraphicsShader? _preLightShader;

    internal static Camera Camera { get; private set; } = null!;
    internal static GameWindow Window { get; private set; } = null!;

    public static GraphicsShader GetPrelightShader() => _preLightShader!;

    public static void SetCamera(Camera camera) {
        Camera = camera;
        CameraChange();
    }

    public static void SetGameWindow(GameWindow window) {
        Window = window;
        Resize();
    }

    public static void Load() {
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.FrontFace(FrontFaceDirection.Ccw);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _preLightShader = ShaderEngine.RegisterGraphics("Prelight", "./RocketEngine/Shaders/Prelight/prelight.vert", "./RocketEngine/Shaders/Prelight/prelight.frag", new ShaderVariant().Define("PRELIGHT"));

        InitializeRenderPipeline();
    }

    public static void RenderFrame(FrameEventArgs args) => RenderPipelineFrame(args);

    public static void Resize() {
        if (Window == null) return;

        ResizeRenderPipeline();
    }
}