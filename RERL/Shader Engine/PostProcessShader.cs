using OpenTK.Graphics.OpenGL4;

namespace RERL.Shader_Engine;

public sealed class PostProcessShader
{
    public const string DefaultVert = "./RocketEngine/Shaders/Templates/DefaultPostProcess/defaultPostProcess.vert";

    readonly GraphicsShader _shader;

    public PostProcessShader(GraphicsShader shader) => _shader = shader;

    public PostProcessShader(string fragmentPath, ShaderVariant? variant = null) : this(new GraphicsShader(DefaultVert, fragmentPath, variant)) { }

    public GraphicsShader Shader => _shader;

    /// <summary>
    /// Registers a uniform whose value is refreshed automatically every time
    /// this pass renders - passes straight through to the underlying
    /// GraphicsShader so callers don't need to reach into .Shader themselves.
    /// </summary>
    public bool RegisterAutoUniform(string name, Func<object?> getter, bool silence = false) => _shader.RegisterAutoUniform(name, getter, silence);

    /// <summary>
    /// For values that don't change every frame.
    /// </summary>
    public bool Set(string name, object? value, bool transposeMatrix = false) => _shader.Set(name, value, transposeMatrix);

    public void Render(GBuffer gbuffer, int inputColorTexture, int vao, int targetFbo) {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);

        // Clear whatever we're about to write into - not just the screen.
        // Ping-pong targets persist across frames (they're only allocated
        // once), so without this each pass would draw over last frame's
        // leftover contents in that buffer instead of a fresh one.
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Clear(ClearBufferMask.DepthBufferBit);

        // Guard against blend state leaking in from the geometry pass and
        // silently accumulating onto whatever's still in the target.
        GL.Disable(EnableCap.Blend);

        _shader.Use();
        _shader.ApplyAutoUniforms();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, inputColorTexture);
        _shader.Set("uColor", 0);

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Position);
        _shader.Set("uPosition", 1);

        GL.ActiveTexture(TextureUnit.Texture2);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Normal);
        _shader.Set("uNormal", 2);

        GL.ActiveTexture(TextureUnit.Texture3);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Depth);
        _shader.Set("uDepth", 3);

        GL.BindVertexArray(vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
    }
}