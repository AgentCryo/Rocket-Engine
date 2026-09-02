using RCS.Component_Engine;

namespace RCS.Components;

public struct EntityInfo : IComponent {
    public string Name;

    public EntityInfo(string name)
    {
        Name = name;
    }

    public Entity Owner { get; set; }
}