using OpenTK.Mathematics;
using RCS.Component_Engine;

namespace RCS.Components;

public class Transform : IOwnerComponent
{
    public Entity Owner { get; set; }

    public Vector3 Position = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;

    public Entity Parent { get; internal set; }
    public bool HasParent { get; internal set; }

    public Transform()
    {
    }

    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Rotation);
    public Vector3 Right => Vector3.Transform(Vector3.UnitX, Rotation);
    public Vector3 Up => Vector3.Transform(Vector3.UnitY, Rotation);

    public Matrix4 LocalMatrix => Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(Rotation) * Matrix4.CreateTranslation(Position);

    public Matrix4 WorldMatrix => RCS_Core.GetActiveWorld().GetWorldMatrix(Owner);

    public void SetTransform(Transform transform)
    {
        Position = transform.Position;
        Rotation = transform.Rotation;
        Scale = transform.Scale;
    }
}