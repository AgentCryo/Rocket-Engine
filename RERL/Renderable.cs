using RERL.Shader_Engine;

namespace RERL;

public interface Renderable
{
    void RebuildMaterialSSBO();
    void Render(int instanceCount = 1);
    GraphicsShader? GetShader();
}