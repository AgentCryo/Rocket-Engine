using OpenTK.Mathematics;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace RCS.Components;

/// <summary>
/// A built‑in component that provides position, rotation, scale,
/// hierarchical parenting, and world/local matrix calculations.
/// </summary>
/// <param name="position">The initial position of the transform.</param>
/// <param name="rotation">The initial rotation of the transform as a quaternion.</param>
/// <param name="scale">The initial scale of the transform.</param>
public class Transform(Vector3 position, Quaternion rotation, Vector3 scale) : IComponent
{
    /// <summary>
    /// The entity that owns this component.
    /// </summary>
    public Entity? Owner { get; set; } = null;

    /// <summary>
    /// The local position of the transform.
    /// </summary>
    public Vector3 Position = position;

    /// <summary>
    /// The local rotation of the transform, as a quaternion.
    /// </summary>
    public Quaternion Rotation = rotation;
    public Vector3 EulerAngles
    {
        get => Rotation.ToEulerAngles() * (180f / MathF.PI);
        set => Rotation = Quaternion.FromEulerAngles(
            value * (MathF.PI / 180f)
        );
    }

    /// <summary>
    /// The local scale of the transform.
    /// </summary>
    public Vector3 Scale = scale;

    /// <summary>
    /// The parent transform in the hierarchy, or <c>null</c> if this is a root transform.
    /// </summary>
    public Transform? Parent = null;

    /// <summary>
    /// The list of child transforms attached to this transform.
    /// </summary>
    public List<Transform> Children = [];

    /// <summary>
    /// Returns a transform with zero position and rotation, and a scale of one.
    /// </summary>
    public static Transform Identity => new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One);
    
    public Transform SetPosition(Vector3 position)
    {
        Position = position;
        return this;
    }

    /// <summary>
    /// Sets the local rotation of the transform, in degrees.
    /// </summary>
    /// <param name="rotation">The new rotation.</param>
    /// <returns>The current <see cref="Transform"/> instance.</returns>
    public Transform SetRotationInDegrees(Vector3 rotation)
    {
        Rotation = Quaternion.FromEulerAngles(rotation);
        return this;
    }
    
    /// <summary>
    /// Sets the local rotation of the transform, as a quaternion.
    /// </summary>
    /// <param name="rotation">The new rotation.</param>
    /// <returns>The current <see cref="Transform"/> instance.</returns>
    public Transform SetRotation(Quaternion rotation)
    {
        Rotation = rotation;
        return this;
    }
    
    public Transform SetScale(Vector3 scale)
    {
        Scale = scale;
        return this;
    }
    
    public Transform SetTransform(Transform transform)
    {
        Position = transform.Position;
        Rotation = transform.Rotation;
        Scale = transform.Scale;
        return this;
    }

        
    Func<Vector3>? _posListener = null;
    public Transform SetPositionListener(Func<Vector3> posListener) {_posListener = posListener; return this;}
    
    Func<Vector3>? _rotDegListener = null;
    public Transform SetRotationInDegreesListener(Func<Vector3> rotDegListener) {_rotDegListener = rotDegListener; return this;}
    
    Func<Quaternion>? _rotListener = null;
    public Transform SetRotationListener(Func<Quaternion> rotListener) {_rotListener = rotListener; return this;}
    
    Func<Vector3>? _scaleListener = null;
    public Transform SetScaleListener(Func<Vector3> scaleListener) {_scaleListener = scaleListener; return this;}

    
    /// <summary>
    /// Gets the forward direction of the transform in world space.
    /// </summary>
    public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, Rotation);

    /// <summary>
    /// Adds a child transform to this transform.
    /// </summary>
    /// <returns>The current <see cref="Transform"/> instance.</returns>
    public Transform AddChild(Transform child)
    {
        child.Parent = this;
        Children.Add(child);
        return this;
    }
    
    /// <summary>
    /// Sets this as a child of a transform.
    /// </summary>
    /// <returns>The current <see cref="Transform"/> instance.</returns>
    public Transform SetParent(Transform parent)
    {
        parent.AddChild(this);
        return this;
    }

    /// <summary>
    /// Gets the transformation matrix for this transform.
    /// </summary>
    public Matrix4 TransformationMatrix
    {
        get
        {
            var translation = Matrix4.CreateTranslation(Position);
            var rot = Matrix4.CreateFromQuaternion(Rotation);
            var scale = Matrix4.CreateScale(Scale);

            return scale * rot * translation;
        }
    }

    /// <summary>
    /// Gets the world transformation matrix for this transform,
    /// including all parent transforms.
    /// </summary>
    public Matrix4 WorldMatrix
    {
        get
        {
            if (Parent != null)
                return TransformationMatrix * Parent.WorldMatrix;
            else
                return TransformationMatrix;
        }
    }


    public void Load() {}

    public void Update(float deltaTime) {
        if(_posListener != null) Position = _posListener.Invoke();
        if(_rotDegListener != null) Rotation = Quaternion.FromEulerAngles(_rotDegListener.Invoke());
        if(_rotListener != null) Rotation = _rotListener.Invoke();
        if(_scaleListener != null) Scale = _scaleListener.Invoke();
    }
    
    public void OnAdd() {}
}
