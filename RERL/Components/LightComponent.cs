using System.Drawing;
using OpenTK.Mathematics;
using RCS;
using RCS.Component_Engine;
using RCS.Components;
using RERL;

namespace RERL.Components;

public enum LightConfig
{
    SmoothEdgeClamping
}

public enum LightType : uint
{
    Point       = 0b00,
    Spot        = 0b01,
    Directional = 0b10
}

public class LightComponent : IOwnerComponent
{
    public LightComponent(LightType type)
    {
        LightData = new RenderData.Light();
        LightData.Data = 0;

        LightType = type;

        SetDirection((0, 0));
    }

    public bool AutoRegister { get; set; } = true;

    bool _isGlobal;

    public RenderData.Light LightData;

    public LightType LightType
    {
        get => (LightType)BitHelper.ReadBits(
            LightData.Data,
            0,
            2);

        set => BitHelper.WriteBits(
            ref LightData.Data,
            0,
            2,
            (uint)value);
    }

    #region Create Helpers

    public static LightComponent CreatePoint(
        float radius,
        float intensity,
        Vector3 color,
        bool global = false)
    {
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
        bool global = false)
    {
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
        bool global = false)
    {
        return new LightComponent(LightType.Directional)
            .SetDirection(directionDeg)
            .SetIntensity(intensity)
            .SetColor(color)
            .SetGlobal(global);
    }

    #endregion

    #region Setters

    public LightComponent SetAutoRegister(bool autoRegister)
    {
        AutoRegister = autoRegister;
        return this;
    }

    public LightComponent SetColor(Vector3 color)
    {
        LightData.Color = color;
        return this;
    }

    public LightComponent SetIntensity(float value)
    {
        LightData.Intensity = value;
        return this;
    }

    public LightComponent SetPosition(Vector3 position)
    {
        LightData.Position = position;
        return this;
    }

    public LightComponent SetGlobal(bool isGlobal)
    {
        _isGlobal = isGlobal;

        BitHelper.WriteBit(
            ref LightData.Data,
            3,
            isGlobal);

        return this;
    }

    public LightComponent SetRadius(float radius)
    {
        LightData.Radius = radius;
        return this;
    }

    /// <summary>
    /// Sets the direction in Euler degrees.
    /// </summary>
    public LightComponent SetDirection(Vector2 eulerDegrees)
    {
        float pitch =
            float.DegreesToRadians(eulerDegrees.X);

        float yaw =
            float.DegreesToRadians(eulerDegrees.Y);

        Vector3 dir;

        dir.X =
            MathF.Sin(yaw) *
            MathF.Cos(pitch);

        dir.Y =
            MathF.Sin(pitch);

        dir.Z =
            MathF.Cos(yaw) *
            MathF.Cos(pitch);

        LightData.Direction =
            Vector3.Normalize(dir);

        return this;
    }

    /// <summary>
    /// Sets the spotlight angle in radians.
    /// </summary>
    public LightComponent SetAngle(float angle)
    {
        LightData.Angle = angle;
        return this;
    }

    #endregion

    #region Light Data Interface

    public LightComponent Enable(params LightConfig[] configs)
    {
        foreach (var config in configs)
            Enable(config);

        return this;
    }

    public LightComponent Disable(params LightConfig[] configs)
    {
        foreach (var config in configs)
            Disable(config);

        return this;
    }

    public LightComponent Enable(LightConfig lightConfig)
    {
        switch (lightConfig)
        {
            case LightConfig.SmoothEdgeClamping:
                BitHelper.SetBit(
                    ref LightData.Data,
                    2);
                break;
        }

        return this;
    }

    public LightComponent Disable(LightConfig lightConfig)
    {
        switch (lightConfig)
        {
            case LightConfig.SmoothEdgeClamping:
                BitHelper.ClearBit(
                    ref LightData.Data,
                    2);
                break;
        }

        return this;
    }

    #endregion

    /// <summary>
    /// Synchronizes this light with an entity transform.
    /// </summary>
    public void SyncTransform(Transform transform)
    {
        SetPosition(transform.Position);

        var direction = transform.Forward;

        SetDirection(
            new Vector2(
                MathF.Asin(direction.Y),
                MathF.Atan2(direction.X, direction.Z)));
    }

    public bool IsGlobal => _isGlobal;
    public Entity Owner { get; set; }
}

public class LightBuilder
{
    readonly LightComponent _light;

    LightBuilder(LightType type)
    {
        _light = new LightComponent(type);
    }

    public static LightBuilder Point() =>
        new(LightType.Point);

    public static LightBuilder Spot() =>
        new(LightType.Spot);

    public static LightBuilder Directional() =>
        new(LightType.Directional);

    public LightBuilder Radius(float r)
    {
        _light.SetRadius(r);
        return this;
    }

    public LightBuilder Intensity(float i)
    {
        _light.SetIntensity(i);
        return this;
    }

    public LightBuilder Color(Vector3 c)
    {
        _light.SetColor(c);
        return this;
    }

    public LightBuilder Global(bool g = true)
    {
        _light.SetGlobal(g);
        return this;
    }

    public LightBuilder DirectionDegrees(Vector2 eulerDegrees)
    {
        _light.SetDirection(eulerDegrees);
        return this;
    }

    public LightBuilder AngleDegrees(float deg)
    {
        _light.SetAngle(
            float.DegreesToRadians(deg));

        return this;
    }

    public LightBuilder Enable(params LightConfig[] cfg)
    {
        _light.Enable(cfg);
        return this;
    }

    public LightBuilder Disable(params LightConfig[] cfg)
    {
        _light.Disable(cfg);
        return this;
    }

    public LightComponent Build() => _light;
}