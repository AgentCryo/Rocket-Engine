using OpenTK.Mathematics;
using RCS;
using RCS.Component_Engine;
using RCS.Components;

namespace Dev;

public class OrbitComponent(float radius, float height, float inclination, float speed, float verticalMotion, float verticalSpeed, float phase) : IOwnerComponent, IUpdatable {
    public Entity Owner { get; set; }

    float _phase = phase;

    public void Update(float deltaTime)
    {
        _phase += speed * deltaTime;

        float x = MathF.Cos(_phase) * radius;
        float z = MathF.Sin(_phase) * radius;

        float orbitY = MathF.Sin(_phase) * MathF.Sin(inclination) * radius;
        float vertical = MathF.Sin(_phase * verticalSpeed) * verticalMotion;

        var world = RCS_Core.GetActiveWorld();
        var transform = world.GetUnchecked<Transform>(Owner);

        transform.Position = new Vector3(x, height + orbitY + vertical, z);
    }
}