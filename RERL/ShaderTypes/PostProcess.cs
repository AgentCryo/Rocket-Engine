using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;

namespace RERL.ShaderTypes;

/// <summary>
/// Represents a post‑processing shader that renders a full‑screen pass
/// using the output of the geometry buffer.
/// </summary>
public class PostProcess : Shader
{
    /// <summary>
    /// Loads and attaches the post‑processing shader, and creates an internal G‑Buffer
    /// used to store the output of this post‑process pass.
    /// </summary>
    /// <param name="postProcessFragmentPath">Path to the fragment shader used for post‑processing.</param>
    /// <param name="window">The game window used to size the internal G‑Buffer.</param>
    /// <returns>The current <see cref="PostProcess"/> instance for chaining.</returns>
    public PostProcess AttachPostProcessShader(string postProcessFragmentPath, GameWindow window)
    {
        AttachGraphicsShader(DefaultPostProcessVert, postProcessFragmentPath, ShaderType.PostProcess);
        
        return this;
    }

    /// <summary>
    /// Executes the post‑processing shader using the provided G‑Buffer as input.
    /// </summary>
    /// <param name="gbuffer">The input G‑Buffer from the geometry pass.</param>
    /// <param name="VAO">The VAO containing a full‑screen triangle.</param>
    /// <param name="renderToScreen">If true, the result is also drawn to the screen.</param>
    /// <returns>The internal G‑Buffer containing the post‑processed output.</returns>
    public void RenderPostProcess(GBuffer gbuffer, int VAO, bool renderToScreen)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, renderToScreen ? 0 : gbuffer.GetFBO());

        Use();
        ApplyAutoUniforms();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Color);
        ApplyUniform("uColor", 0);

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Normal);
        ApplyUniform("uNormal", 1);
        
        GL.ActiveTexture(TextureUnit.Texture2);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Depth);
        ApplyUniform("uDepth", 2);
        
        GL.BindVertexArray(VAO);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        if (!renderToScreen) return;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.BindVertexArray(VAO);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }
}
