using RCS;
using RCS.Component_Engine;
using RCS.Components;

namespace Dev;

public class CubeComponent : IOwnerComponent, IUpdatable
{
    public Entity Owner { get; set; }

    float _time;

    public void Update(float deltaTime)
    {
        var transform = RCS_Core.GetActiveWorld().Get<Transform>(Owner);
        transform.Position.X = float.Sin(_time += deltaTime) * 3;
    }
}