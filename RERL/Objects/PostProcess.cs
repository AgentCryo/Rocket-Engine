using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace RERL.Objects;

public class PostProcess
{
    Shader _shader;
    RERL_Core.GBuffer _gbuffer;

    public PostProcess AttachPostProcessShader(string postProcessFragmentPath, GameWindow window)
    {
        _gbuffer = new RERL_Core.GBuffer(window.Size);

        _shader = new Shader().AttachShader("./Shaders/DefaultPostProcess/defaultPostProcess.vert",
            postProcessFragmentPath);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _gbuffer.GetFBO());
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _gbuffer.Color, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1,
            TextureTarget.Texture2D, _gbuffer.Normal, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            TextureTarget.Texture2D, _gbuffer.Depth, 0);
        
        return this;
    }

    //void BindFramebuffer()
    //{
    //    _gbuffer.Clear();
    //    
    //    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _gbuffer.GetFBO());
    //    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
    //        TextureTarget.Texture2D, _gbuffer.Color, 0);
    //    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1,
    //        TextureTarget.Texture2D, _gbuffer.Normal, 0);
    //    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
    //        TextureTarget.Texture2D, _gbuffer.Depth, 0);
    //    
    //    DrawBuffersEnum[] drawBuffers = [DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1];
    //    GL.DrawBuffers(drawBuffers.Length, drawBuffers);
    //}

    public RERL_Core.GBuffer RenderPostProcess(RERL_Core.GBuffer gbuffer, int VAO, bool renderToScreen)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _gbuffer.GetFBO());
        _gbuffer.Clear();

        _shader.Use();
        _shader.ApplyAutoUniforms();

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Color);
        _shader.ApplyUniform("uColor", 0);

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Normal);
        _shader.ApplyUniform("uNormal", 1);

        GL.ActiveTexture(TextureUnit.Texture2);
        GL.BindTexture(TextureTarget.Texture2D, gbuffer.Depth);
        _shader.ApplyUniform("uDepth", 2);

        GL.BindVertexArray(VAO);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        
        if (renderToScreen)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindVertexArray(VAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        return _gbuffer;
    }
}