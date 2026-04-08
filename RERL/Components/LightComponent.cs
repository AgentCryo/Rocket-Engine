using System.Drawing;
using OpenTK.Mathematics;
using RCS;
using RCS.Components;
using RERL;

namespace RERL.Components;

public enum LightConfig {
    SmoothEdgeClamping
}

public enum LightType : uint
{
    Point       = 0b00,
    Spot        = 0b01,
    Directional = 0b10
}

public class LightComponent : IComponent
{
    public Entity Owner      { get; set; }
    public bool AutoRegister { get; set; } = true;
    bool _isGlobal = false;

    public RenderData.Light LightData;

    public LightType LightType {
        set => BitHelper.WriteBits(ref LightData.Data, 0, 2, (uint)value);
        get => (LightType)BitHelper.ReadBits(LightData.Data, 0, 2);
    }
    
    public LightComponent(LightType type)
    {
        LightData = new RenderData.Light();
        LightData.Data = 0;
        LightType = type;
    }

    public LightComponent SetAutoRegister(bool autoRegister) {
        AutoRegister = autoRegister;
        return this;
    }
    
    public LightComponent SetColor    (Vector3 color)    { LightData.Color = color;       return this; }
    public LightComponent SetIntensity(float value)      { LightData.Intensity = value;   return this; }
    public LightComponent SetPosition (Vector3 position) { LightData.Position = position; return this; }

    public LightComponent SetGlobal(bool isGlobal)
    {
        _isGlobal = isGlobal;
        BitHelper.WriteBit(ref LightData.Data, 3, isGlobal);
        return this;
    }
    public LightComponent SetRadius   ( float radius)    { LightData.Radius = radius;     return this; }
    
    /// <param name="eulerDegrees">Sets the direction in euler degrees.</param>
    public LightComponent SetDirection(Vector2 eulerDegrees)
    {
        float pitch = float.DegreesToRadians(eulerDegrees.X);
        float yaw   = float.DegreesToRadians(eulerDegrees.Y);

        Vector3 dir;
        dir.X = MathF.Sin(yaw) * MathF.Cos(pitch);
        dir.Y = MathF.Sin(pitch);
        dir.Z = MathF.Cos(yaw) * MathF.Cos(pitch);

        LightData.Direction = Vector3.Normalize(dir);
        return this;
    }

    /// <param name="angle">Sets the angle in radians.</param>
    public LightComponent SetAngle(float angle)           {LightData.Angle = angle; return this;}
    
    Func<Vector3>? _posListener = null;
    public LightComponent SetPositionListener(Func<Vector3> posListener) {_posListener = posListener; return this;}
    
    Func<Vector3>? _dirListener = null;
    public LightComponent SetDirectionListener(Func<Vector3> dirListener) {_dirListener = dirListener; return this;}

    public LightComponent Enable(LightConfig lightConfig)
    {
        switch (lightConfig) {
            case Components.LightConfig.SmoothEdgeClamping: BitHelper.SetBit(ref LightData.Data, 2); break;
        }

        return this;
    }
    
    public LightComponent Disable(LightConfig lightConfig)
    {
        switch (lightConfig) {
            case Components.LightConfig.SmoothEdgeClamping: BitHelper.ClearBit(ref LightData.Data, 2); break;
        }
        
        return this;
    }
    
    public void Load() { }

    public void Update(float deltaTime)
    {
        if(_posListener != null) LightData.Position = _posListener.Invoke();
        if(_dirListener != null) LightData.Direction = _dirListener.Invoke();
    }
    
    public void OnAdd()
    {
        if (!AutoRegister) return;
        if (_isGlobal) RenderPipeline.RegisterGlobalLight(this); else RenderPipeline.RegisterLight(this);
    }
}