using OpenTK.Mathematics;

namespace RERL;

/// <summary>
/// Represents a 3D camera with position, rotation, and view/projection matrices.
/// </summary>
public class Camera
{
    public bool IsDirty;
    
    Vector3    _position;
    Quaternion _rotation;
    
    Matrix4 _view;
    Matrix4 _projection;

    public float Near { get; private set; }
    public float Far { get; private set; }

    public Matrix4 GetProjection() => _projection;
    public Matrix4 GetView() => _view;

    /// <summary>
    /// Sets the projection matrix using a vertical field of view (in degrees).
    /// </summary>
    /// <param name="aspect">Screen width / Screen Height.</param>
    public void SetProjectionFovYInDegrees(float fovY, float aspect, float near, float far)
    {
        Near = near;
        Far = far;
        _projection = CreatePerspectiveFieldOfViewLH(float.DegreesToRadians(fovY), aspect, near, far);
    }

    /// <summary>
    /// Sets the projection matrix using a horizontal field of view (in degrees).
    /// </summary>
    /// <param name="aspect">Screen width / Screen Height.</param>
    public void SetProjectionFovXInDegrees(float fovX, float aspect, float near, float far)
    {
        Near = near;
        Far = far;
        float fovY = 2f * MathF.Atan(MathF.Tan(MathHelper.DegreesToRadians(fovX) / 2f) / aspect);
        _projection = CreatePerspectiveFieldOfViewLH(fovY, aspect, near, far);
    }

    public static Matrix4 CreatePerspectiveFieldOfViewLH(float fov, float aspect, float near, float far)
    {
        float f = 1f / MathF.Tan(fov / 2f);

        return new Matrix4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, far / (far - near), 1,
            0, 0, (-near * far) / (far - near), 0
        );
    }
    
    /// <summary>
    /// Updates the view matrix based on the camera's position and rotation.
    /// </summary>
    public void UpdateViewMatrix()
    {
        // Build camera world transform
        Matrix4 world =
            Matrix4.CreateFromQuaternion(_rotation) *
            Matrix4.CreateTranslation(_position);

        // View = inverse of world transform
        Matrix4.Invert(world, out _view);
    }

    public void SetPosition(Vector3 position) => _position = position;
    public void SetRotation(Quaternion rotation) => _rotation = rotation;
}