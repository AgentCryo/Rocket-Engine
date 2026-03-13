using RCS;
using RCS.Components;
using OpenTK.Mathematics;

namespace Dev;

public class PongComponent : IComponent
{
    public Entity Owner { get; set; }

    readonly float _width;
    readonly float _height;

    Vector2 _velocity;

    public PongComponent(float width, float height)
    {
        _width = width;
        _height = height;

        // initial direction
        _velocity = new Vector2(1, 1).Normalized() * 3f;
    }

    public void Load() {}

    public void OnAdd() {}

    public void Update(float deltaTime)
    {
        if (!Owner.TryGetComponent(out Transform? transform) || transform == null)
            return;

        // move
        transform.Position.X += _velocity.X * deltaTime;
        transform.Position.Y += _velocity.Y * deltaTime;

        // bounce on X
        if (transform.Position.X < -_width || transform.Position.X > _width)
            _velocity.X *= -1;

        // bounce on Y
        if (transform.Position.Y < -_height || transform.Position.Y > _height)
            _velocity.Y *= -1;
    }
}