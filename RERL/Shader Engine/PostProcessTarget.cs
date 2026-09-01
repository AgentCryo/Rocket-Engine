using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace RERL.Shader_Engine;

/// <summary>
/// A single-color-attachment scratch render target used as an intermediate
/// buffer between chained post-process passes. Deliberately separate from
/// GBuffer - a pass must never render into the same buffer it's sampling
/// from (that's undefined behavior / a feedback loop), and GFrame's
/// Position/Normal/Depth need to stay untouched and readable across the
/// whole post-process chain.
/// </summary>
public sealed class PostProcessTarget : IDisposable
{
    public int FBO { get; private set; }
    public int ColorTexture { get; private set; }

    public PostProcessTarget(Vector2i size)
    {
        Allocate(size);
    }

    void Allocate(Vector2i size)
    {
        ColorTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, ColorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, size.X, size.Y, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        FBO = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, FBO);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ColorTexture, 0);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Resize(Vector2i size)
    {
        Dispose();
        Allocate(size);
    }

    public void Dispose()
    {
        if (FBO != 0) GL.DeleteFramebuffer(FBO);
        if (ColorTexture != 0) GL.DeleteTexture(ColorTexture);
        FBO = 0;
        ColorTexture = 0;
    }
}