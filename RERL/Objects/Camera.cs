using OpenTK.Mathematics;

namespace RERL.Objects;

public class Camera
{
    Vector3 _position = new();
    Vector3 _rotation = new();
    
    Matrix4 _view;
    Matrix4 _projection;

    public Matrix4 Projection => _projection;
    public Matrix4 View => _view;

    public void SetProjectionFovYInDegrees(float fovY, float aspect, float near, float far)
    {
        _projection = Matrix4.CreatePerspectiveFieldOfView(float.DegreesToRadians(fovY), aspect, near, far);
    }
    public void SetProjectionFovXInDegrees(float fovX, float aspect, float near, float far)
    {
        float fovY = 2f * MathF.Atan(MathF.Tan(MathHelper.DegreesToRadians(fovX) / 2f) / aspect);
        _projection = Matrix4.CreatePerspectiveFieldOfView(fovY, aspect, near, far);
    }

    public void UpdateViewMatrix()
    {
        Quaternion q = Quaternion.FromEulerAngles(
            float.DegreesToRadians(_rotation.X),
            float.DegreesToRadians(_rotation.Y),
             float.DegreesToRadians(_rotation.Z));

        Vector3 forward = q * -Vector3.UnitZ;
        Vector3 up = q * Vector3.UnitY;

        _view = Matrix4.LookAt(_position, _position + forward, up);
    }
    
    public void SetPosition(Vector3 position) => _position = position;
    public void SetRotation(Vector3 rotation) => _rotation = rotation;
}