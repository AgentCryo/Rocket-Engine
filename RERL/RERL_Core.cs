using System.ComponentModel;
using OpenTK.Mathematics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RCS;
using RERL.Components;
using RERL.Loaders;
using RERL.ShaderTypes;
using static RERL.RenderData;
using static RERL.RenderPipeline;

namespace RERL;

/// <summary>
/// The core rendering system for the Rocket Engine Rendering Layer (RERL).
/// Handles shader registration, mesh batching, G‑Buffer creation,
/// post‑processing, and the main render loop.
/// </summary>
public static class RERL_Core
{
    static Shader? _preLightShader;
    public static Shader GetPrelightShader() => _preLightShader!;

    internal static Camera Camera;
    internal static GameWindow Window;
    
    public static void SetCamera(Camera camera) => Camera = camera;
    public static void SetGameWindow(GameWindow window) => Window = window;
    
    /// <summary>
    /// Initializes the rendering system, loads shaders, creates the G‑Buffer,
    /// and prepares OpenGL state. Must be called before adding renderables.
    /// </summary>
    public static void Load()
    {
        //var RERL = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RERL");
        //var RCS = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RCS");
        //Console.WriteLine(RERL != null ? $"Library Found: {RERL.FullName}" : "Library not Found");
        //Console.WriteLine(RCS != null ? $"Library Found: {RCS.FullName}" : "Library not Found");

        GL.ClearColor(Color.FromArgb(255, 20,25,35));
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.FrontFace(FrontFaceDirection.Ccw);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        _preLightShader = new Shader().AttachGraphicsShader("./Shaders/Prelight/prelight.vert", "./Shaders/Prelight/prelight.frag", Shader.ShaderType.Prelight);
        RegisterShader(_preLightShader);
        
        InitializeRenderPipeline();
    }
    
    /// <summary>
    /// Renders a single frame, including geometry pass, post‑processing, and buffer swapping.
    /// </summary>
    public static void RenderFrame(FrameEventArgs args) => RenderPipelineFrame(args);

    public static void OnResize()
    {
        ResizeRenderPipeline();
    }
}