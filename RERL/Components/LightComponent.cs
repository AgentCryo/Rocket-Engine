using System.Drawing;
using OpenTK.Mathematics;
using RCS;
using RCS.Components;

namespace RERL.Components;

public class LightComponent : IComponent
{
    public Entity Owner { get; set; }
    public bool AutoRegister { get; set; } = true;

    public RenderData.Light LightData;

    public LightComponent SetAutoRegister(bool autoRegister) {
        AutoRegister = autoRegister;
        return this;
    }
    
    public LightComponent SetColor(Vector3 color)         {LightData.Color = color; return this;}
    public LightComponent SetIntensity(float value)       {LightData.Intensity = value; return this;}
    public LightComponent SetPosition(Vector3 position)   {LightData.Position = position; return this;}
    public LightComponent SetRadius(float radius)         {LightData.Radius = radius; return this;}
    public LightComponent SetDirection(Vector3 direction) {LightData.Direction = direction; return this;}
    public LightComponent SetAngle(float angle)           {LightData.Angle = angle; return this;}
    
    Func<Vector3>? _posListener = null;
    public LightComponent SetPositionListener(Func<Vector3> posListener) {_posListener = posListener; return this;}
    
    Func<Vector3>? _dirListener = null;
    public LightComponent SetDirectionListener(Func<Vector3> dirListener) {_dirListener = dirListener; return this;}
    
    public void Load() { }

    public void Update(float deltaTime)
    {
        if(_posListener != null) LightData.Position = _posListener.Invoke();
        if(_dirListener != null) LightData.Direction = _dirListener.Invoke();
    }
    
    public void OnAdd()
    {
        if (AutoRegister)
            RenderPipeline.RegisterLight(this);
    }
}