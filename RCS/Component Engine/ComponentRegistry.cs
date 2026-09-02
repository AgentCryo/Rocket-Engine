using RCS.Components;

namespace RCS.Component_Engine;

public static class ComponentRegistry
{
    static readonly Dictionary<Type, int> _ids = [];

    public static int Register<T>() where T : IComponent
    {
        var type = typeof(T);

        if (_ids.TryGetValue(type, out var id))
            return id;

        id = _ids.Count;
        _ids.Add(type, id);

        return id;
    }
}