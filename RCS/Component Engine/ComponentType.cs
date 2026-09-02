using RCS.Components;

namespace RCS.Component_Engine;

public static class ComponentType<T> where T : IComponent {
    public static readonly int Id = ComponentRegistry.Register<T>();
}