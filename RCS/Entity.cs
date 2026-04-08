using OpenTK.Mathematics;
using RCS.Components;

namespace RCS;

/// <summary>
/// Represents an entity that can contain and manage a collection of components.
/// </summary>
/// <param name="name">The name of the entity.</param>
public class Entity
{
    readonly string _name;
    readonly List<IComponent> _components = [];
    public readonly Transform Transform;
    readonly Dictionary<string, object> _data = new();

    public Entity(string name) {
        _name = name;
        Transform = new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One) {
            Owner = this
        };
    }

    #region Data Interface

    public void RegisterData(string name, object data) => _data.Add(name, data);
    public object GetData(string name) => _data[name];
    public void RemoveData(string name) => _data.Remove(name);
    public void SetData(string name, object data) => _data[name] = data;

    #endregion

    /// <summary>
    /// Gets the name of the entity.
    /// </summary>
    public string GetName() => _name;

    #region Component Interface

    /// <summary>
    /// Adds a component instance to the entity.
    /// </summary>
    /// <param name="component">The component to add.</param>
    /// <returns>The current <see cref="Entity"/> instance for chaining.</returns>
    public Entity AddComponent(IComponent component)
    {
        component.Owner = this;
        _components.Add(component);
        component.OnAdd();
        return this;
    }

    /// <summary>
    /// Creates and adds a new component of type <typeparamref name="T"/> to the entity.
    /// </summary>
    /// <typeparam name="T">The component type to add.</typeparam>
    /// <returns>The current <see cref="Entity"/> instance for chaining.</returns>
    public Entity AddComponent<T>() where T : IComponent, new()
    {
        var component = new T { Owner = this };
        _components.Add(component);
        component.OnAdd();
        return this;
    }

    /// <summary>
    /// Retrieves a component of type <typeparamref name="T"/> from the entity.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <returns>The component instance.</returns>
    /// <exception cref="Exception">
    /// Thrown if a component of the specified type does not exist.
    /// </exception>
    public T GetComponent<T>() where T : class, IComponent => _components.OfType<T>().FirstOrDefault() ?? throw new Exception($"ERR: Component of type: {typeof(T).Name} not found in: {_name}.");

    /// <summary>
    /// Attempts to retrieve a component of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <param name="component">The retrieved component, or <c>null</c> if not found.</param>
    /// <returns><c>true</c> if the component exists; otherwise, <c>false</c>.</returns>
    public bool TryGetComponent<T>(out T? component) where T : class, IComponent {
        component = _components.OfType<T>().FirstOrDefault();
        return component != null;
    }

    /// <summary>
    /// Removes all components from the entity.
    /// </summary>
    public void RemoveAllComponents() => _components.Clear();

    /// <summary>
    /// Removes a specific component instance from the entity.
    /// </summary>
    /// <param name="component">The component to remove.</param>
    public void RemoveComponent(IComponent component) => _components.Remove(component);
    

    #endregion
    
    /// <summary>
    /// Calls <see cref="IComponent.Load"/> on all components.
    /// </summary>
    public virtual void Load() {
        foreach (var component in _components)
            component.Load();
    }

    /// <summary>
    /// Calls <see cref="IComponent.Update(float)"/> on all components.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last update.</param>
    public virtual void Update(float deltaTime) {
        Transform.Update(deltaTime);
        foreach (var component in _components)
            component.Update(deltaTime);
    }
}
