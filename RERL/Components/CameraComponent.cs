using OpenTK.Mathematics;
using RCS;
using RCS.Component_Engine;
using RCS.Components;

namespace RERL.Components;

/// <summary>
/// A component containing camera state and projection settings.
/// </summary>
public class CameraComponent : IComponent
{
    readonly Camera Camera = new();

    /// <summary>
    /// Gets the underlying camera.
    /// </summary>
    public Camera GetCamera() => Camera;

    /// <summary>
    /// Sets the projection matrix using a vertical field of view.
    /// </summary>
    public void SetProjectionFovYInDegrees(
        float fovY,
        float aspect,
        float near,
        float far)
        => Camera.SetProjectionFovYInDegrees(
            fovY,
            aspect,
            near,
            far);

    /// <summary>
    /// Sets the projection matrix using a horizontal field of view.
    /// </summary>
    public void SetProjectionFovXInDegrees(
        float fovX,
        float aspect,
        float near,
        float far)
        => Camera.SetProjectionFovXInDegrees(
            fovX,
            aspect,
            near,
            far);

    /// <summary>
    /// Sets the camera's world position.
    /// </summary>
    public CameraComponent SetPosition(Vector3 position)
    {
        Camera.SetPosition(position);
        return this;
    }

    /// <summary>
    /// Sets the camera's rotation using Euler angles.
    /// </summary>
    public CameraComponent SetRotationInDegrees(Vector3 rotation)
    {
        Camera.SetRotation(
            Quaternion.FromEulerAngles(rotation));

        return this;
    }

    /// <summary>
    /// Sets the camera's rotation using a quaternion.
    /// </summary>
    public CameraComponent SetRotation(Quaternion rotation)
    {
        Camera.SetRotation(rotation);
        return this;
    }

    /// <summary>
    /// Updates the camera from an entity transform.
    /// </summary>
    public void SyncTransform(Transform transform)
    {
        SetPosition(transform.Position);
        SetRotation(transform.Rotation);

        Camera.UpdateViewMatrix();
    }
}