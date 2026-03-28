using RERL.ShaderTypes;

namespace RERL;

public interface Renderable
{
    public void RebuildMaterialSSBO();
    public void Render(int instanceCount = 1);
    public Shader? GetShader();
}