using OpenTK.Mathematics;
using RCS;
using RCS.Component_Engine;
using RCS.Components;

namespace Dev;

public class PongComponent : IOwnerComponent, IUpdatable
{
    public Entity Owner { get; set; }

    readonly float _width;
    readonly float _height;
    Vector2 _velocity;

    public PongComponent(float width, float height)
    {
        _width = width;
        _height = height;
        _velocity = new Vector2(1, 1).Normalized() * 3f;
    }

    public void Update(float deltaTime)
    {
        var transform = RCS_Core.GetActiveWorld().Get<Transform>(Owner);

        transform.Position.X += _velocity.X * deltaTime;
        transform.Position.Y += _velocity.Y * deltaTime;

        if (transform.Position.X < -_width || transform.Position.X > _width) _velocity.X *= -1;

        if (transform.Position.Y < -_height || transform.Position.Y > _height) _velocity.Y *= -1;
    }
}