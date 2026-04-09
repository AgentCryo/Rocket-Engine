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
    public LightComponent(LightType type)
    {
        LightData = new RenderData.Light();
        LightData.Data = 0;
        LightType = type;
        SetDirection((0, 0));
    }
    
    public Entity Owner      { get; set; }
    public bool AutoRegister { get; set; } = true;
    bool _isGlobal = false;

    public RenderData.Light LightData;

    public LightType LightType {
        set => BitHelper.WriteBits(ref LightData.Data, 0, 2, (uint)value);
        get => (LightType)BitHelper.ReadBits(LightData.Data, 0, 2);
    }

    #region Create Helpers

    public static LightComponent CreatePoint(
        float radius,
        float intensity,
        Vector3 color,
        bool global = false) {
        return new LightComponent(LightType.Point)
            .SetRadius(radius)
            .SetIntensity(intensity)
            .SetColor(color)
            .SetGlobal(global);
    }

    public static LightComponent CreateSpot(
        float radius,
        float angleDeg,
        float intensity,
        Vector3 color,
        Vector2 directionDeg,
        bool global = false) {
        return new LightComponent(LightType.Spot)
            .SetRadius(radius)
            .SetAngle(float.DegreesToRadians(angleDeg))
            .SetIntensity(intensity)
            .SetColor(color)
            .SetDirection(directionDeg)
            .SetGlobal(global);
    }

    public static LightComponent CreateDirectional(
        Vector2 directionDeg,
        float intensity,
        Vector3 color,
        bool global = false) {
        return new LightComponent(LightType.Directional)
            .SetDirection(directionDeg)
            .SetIntensity(intensity)
            .SetColor(color)
            .SetGlobal(global);
    }

    #endregion

    #region Setters

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

    #endregion

    #region Transform Functions

    Func<Vector3>? _posListener = null;
    public LightComponent SetPositionListener(Func<Vector3> posListener) {_posListener = posListener; return this;}
    
    Func<Vector3>? _dirListener = null;
    public LightComponent SetDirectionListener(Func<Vector3> dirListener) {_dirListener = dirListener; return this;}
    
    public LightComponent Follow(Transform transform)
    {
        SetPositionListener(() => transform.Position);
        SetDirectionListener(() => transform.Forward);
        return this;
    }

    #endregion

    #region Light Data Interface

    public LightComponent Enable(params LightConfig[] configs)
    {
        foreach (var cfg in configs)
            Enable(cfg);
        return this;
    }
    
    public LightComponent Disable(params LightConfig[] configs)
    {
        foreach (var cfg in configs)
            Disable(cfg);
        return this;
    }
    
    public LightComponent Enable(LightConfig lightConfig)
    {
        switch (lightConfig) {
            case LightConfig.SmoothEdgeClamping: BitHelper.SetBit(ref LightData.Data, 2); break;
        }

        return this;
    }
    
    public LightComponent Disable(LightConfig lightConfig)
    {
        switch (lightConfig) {
            case LightConfig.SmoothEdgeClamping: BitHelper.ClearBit(ref LightData.Data, 2); break;
        }
        
        return this;
    }

    #endregion

    #region Component Interface

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

    #endregion
}

public class LightBuilder
{
    readonly LightComponent _light;
    LightBuilder(LightType type) => _light = new LightComponent(type);

    #region Types

    public static LightBuilder Point()        => new LightBuilder(LightType.Point);
    public static LightBuilder Spot()         => new LightBuilder(LightType.Spot);
    public static LightBuilder Directional()  => new LightBuilder(LightType.Directional);

    #endregion

    #region Setters

    public LightBuilder Radius(float r) {
        _light.SetRadius(r);
        return this;
    }

    public LightBuilder Intensity(float i) {
        _light.SetIntensity(i);
        return this;
    }

    public LightBuilder Color(Vector3 c) {
        _light.SetColor(c);
        return this;
    }

    public LightBuilder Global(bool g = true) {
        _light.SetGlobal(g);
        return this;
    }

    public LightBuilder DirectionDegrees(Vector2 eulerDegrees) {
        _light.SetDirection(eulerDegrees);
        return this;
    }

    public LightBuilder AngleDegrees(float deg) {
        _light.SetAngle(float.DegreesToRadians(deg));
        return this;
    }

    public LightBuilder Follow(Transform t) {
        _light.Follow(t);
        return this;
    }

    public LightBuilder Enable(params LightConfig[] cfg) {
        _light.Enable(cfg);
        return this;
    }

    public LightBuilder Disable(params LightConfig[] cfg) {
        _light.Disable(cfg);
        return this;
    }

    #endregion
    
    public LightComponent Build() => _light;
}
