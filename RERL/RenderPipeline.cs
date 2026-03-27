using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RCS;
using RERL.Components;
using RERL.Loaders;
using RERL.ShaderTypes;
using static RERL.RERL_Core;
using Window = OpenTK.Windowing.GraphicsLibraryFramework.Window;

namespace RERL;

/// <summary>
/// 
/// </summary>
public static class RenderPipeline
{
    internal static GBuffer GeometryFrame;
    
    // ReSharper disable once InconsistentNaming
    static int _postProcessingQuad_VAO;
    
    internal static readonly List<Shader> Shaders = [];
    internal static readonly List<PostProcess> PostProcesses = [];
    internal static readonly Dictionary<int, List<Renderable>> ShaderBatchRendering = new();
    internal static readonly List<Renderable> Renderables = [];
    internal static readonly List<MaterialLoader.Material> Materials = [];

    internal static void InitializeRenderPipeline()
    {
        GeometryFrame = new GBuffer(RERL_Core.Window.Size);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, GeometryFrame.GetFBO());
        
        _postProcessingQuad_VAO = GL.GenVertexArray();
    }

    internal static void RenderPipelineFrame(FrameEventArgs args)
    {
        #region FrameTime

        GL.GenQueries(1, out int query);
        GL.BeginQuery(QueryTarget.TimeElapsed, query);

        #endregion
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, PostProcesses.Count != 0 ? GeometryFrame.GetFBO() : 0);
        GeometryFrame.Clear();
        
        //Render all meshes grouped by shader to minimize shader switches. (Will be replaced with prelight shader loop)
        foreach (var kpv in ShaderBatchRendering) {
            List<Renderable> renderables = kpv.Value;
            
            Shader shader = renderables[0].GetShader() ?? throw new Exception($"ERR: {renderables[0].GetType().Name} does not have a shader.");
            shader.Use();
            shader.ApplyAutoUniforms();

            foreach (var mr in renderables) {
                mr.Render();
            }
        }
        
        //Render all lights grouped by shader to minimize shader switches. (Light shader loop here)
        
        // Post-processing pass
        GL.DepthMask(false);
        GL.Disable(EnableCap.DepthTest);
        if (PostProcesses.Count != 0) {
            GBuffer input = GeometryFrame;
            for (int p = 0; p < PostProcesses.Count; p++) {
                input = PostProcesses[p].RenderPostProcess(input, _postProcessingQuad_VAO, (p == PostProcesses.Count - 1));
            }
        }
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);

        #region FrameTime

        GL.EndQuery(QueryTarget.TimeElapsed);
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, out long gpuTime);
        Logger.Log($"GPU time: {gpuTime / 1_000_000.0} ms");

        #endregion
        
        RERL_Core.Window.SwapBuffers();
    }
    
    static void RegisterToShaderBatch(Renderable renderable)
    {
        if (renderable.GetShader() == null) throw new Exception("No shader found for renderable: " + renderable + ", shader required for rendering.");
        // ReSharper disable once NullableWarningSuppressionIsUsed
        int handle = renderable.GetShader()!.GetHandle();
        if (!ShaderBatchRendering.TryGetValue(handle, out var list))
        {
            list = [];
            ShaderBatchRendering[handle] = list;
        }
        list.Add(renderable);
    }

    #region User Interface

    #region Renderable
    
    /// <summary>
    /// Registers a renderable mesh renderer for batched rendering.
    /// </summary>
    public static void RegisterRenderable(Renderable renderable)
    {
        Renderables.Add(renderable);
        RegisterToShaderBatch(renderable);
    }
    
    /// <summary>
    /// Removes a renderable from the rendering system.
    /// </summary>
    public static void UnregisterRenderable(Renderable renderable)
    {
        Renderables.Remove(renderable);

        // ReSharper disable once NullableWarningSuppressionIsUsed
        int handle = renderable.GetShader()!.GetHandle();
        if (ShaderBatchRendering.TryGetValue(handle, out var list))
        {
            list.Remove(renderable);
            if (list.Count == 0)
                ShaderBatchRendering.Remove(handle);
        }
    }

    #endregion
    
    #region Shader

    /// <summary>
    /// Registers a shader with the rendering system and assigns automatic uniforms.
    /// </summary>
    public static void RegisterShader(Shader shader)
    {
        Shaders.Add(shader);
        shader.RegisterAutoUniform("uView", () => RERL_Core.Camera.GetView());
        shader.RegisterAutoUniform("uProjection", () => RERL_Core.Camera.GetProjection());
    }

    /// <summary>
    /// Removes a shader from the rendering system.
    /// </summary>
    public static void UnregisterShader(Shader shader) => Shaders.Remove(shader);

    #endregion

    #region Material

    /// <summary>
    /// Registers a material to the render pipeline.
    /// </summary>
    /// <returns>Material id.</returns>
    public static int RegisterMaterial(MaterialLoader.Material material)
    {
        if(Materials.Contains(material)) return Materials.IndexOf(material);
        Materials.Add(material);
        return Materials.IndexOf(material);
    }
    
    public static void UnregisterMaterial(MaterialLoader.Material material) => Materials.Remove(material);

    public static int GetMaterialIndex(MaterialLoader.Material material) => Materials.IndexOf(material);
    
    public static MaterialLoader.Material GetIndexedMaterial(int materialIndex) => Materials[materialIndex];
    
    #endregion
    
    #region PostProcess
    
    /// <summary>
    /// Registers a post‑processing effect to be applied after geometry rendering.
    /// </summary>
    public static void RegisterPostProcess(PostProcess postProcess) => PostProcesses.Add(postProcess);
    /// <summary>
    /// Removes a post‑processing effect from the pipeline.
    /// </summary>
    public static void UnregisterPostProcess(PostProcess postProcess) => PostProcesses.Remove(postProcess);
    
    #endregion
    
    #endregion
}