using RCS;
using RCS.Components;

namespace RERL.Components;

public class LightComponent : IComponent
{
    public Entity Owner { get; set; }
    public bool AutoRegister { get; set; } = true;
    
    public LightComponent SetAutoRegister(bool autoRegister) {
        AutoRegister = autoRegister;
        return this;
    }

    public void Load() { }
    public void Update(float deltaTime) { }
    public void OnAdd()
    {
        if (AutoRegister)
            RenderPipeline.RegisterLight(this);
    }
}