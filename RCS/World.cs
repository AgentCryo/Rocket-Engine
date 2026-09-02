using OpenTK.Mathematics;
using RCS.Component_Engine;
using RCS.Components;

namespace RCS;

public sealed class World
{
    uint _nextEntityId;

    readonly List<uint> _freeEntityIds = [];
    readonly List<uint> _generations = [];

    IComponentStore[] _componentStores = [];
    object[] _typedComponentStores = [];

    /// <summary>
    /// Creates a new entity.
    /// </summary>
    public Entity CreateEntity()
    {
        uint id;

        if (_freeEntityIds.Count > 0)
        {
            var last = _freeEntityIds.Count - 1;
            id = _freeEntityIds[last];
            _freeEntityIds.RemoveAt(last);
        }
        else
        {
            id = _nextEntityId++;
            EnsureGenerationCapacity(id);
        }

        return new Entity(id, _generations[(int)id]);
    }

    /// <summary>
    /// Creates a new entity with a name.
    /// </summary>
    public Entity CreateEntity(string name)
    {
        var entity = CreateEntity();
        Add(entity, new EntityInfo(name));
        return entity;
    }

    /// <summary>
    /// Destroys an entity and removes all of its components.
    /// </summary>
    public void DestroyEntity(Entity entity)
    {
        ValidateEntity(entity);

        foreach (var componentStore in _componentStores)
            componentStore?.Remove(entity);

        _generations[(int)entity.Id]++;
        _freeEntityIds.Add(entity.Id);
    }

    /// <summary>
    /// Finds an entity by name.
    /// </summary>
    public Entity FindEntity(string name)
    {
        var store = GetStore<EntityInfo>();
        var entities = store.Entities;

        for (var i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            ref var info = ref store.Get(entity);

            if (info.Name == name)
                return entity;
        }

        throw new KeyNotFoundException($"Entity with name '{name}' was not found.");
    }

    /// <summary>
    /// Attempts to find an entity by name.
    /// </summary>
    public bool TryFindEntity(string name, out Entity entity)
    {
        var store = GetStore<EntityInfo>();
        var entities = store.Entities;

        for (var i = 0; i < entities.Length; i++)
        {
            var candidate = entities[i];
            ref var info = ref store.Get(candidate);

            if (info.Name != name)
                continue;

            entity = candidate;
            return true;
        }

        entity = default;
        return false;
    }

    /// <summary>
    /// Adds a component to an entity.
    /// </summary>
    public void Add<T>(Entity entity, T component)
        where T : IComponent
    {
        ValidateEntity(entity);

        if (component is IOwnerComponent ownerComponent)
            ownerComponent.Owner = entity;

        GetStore<T>().Add(entity, component);
    }

    /// <summary>
    /// Determines whether an entity has a component.
    /// </summary>
    public bool Has<T>(Entity entity)
        where T : IComponent
    {
        return IsAlive(entity) && GetStore<T>().Has(entity);
    }

    /// <summary>
    /// Gets a component from an entity with full validation.
    /// </summary>
    public ref T Get<T>(Entity entity)
        where T : IComponent
    {
        ValidateEntity(entity);
        return ref GetStore<T>().Get(entity);
    }

    /// <summary>
    /// Gets a component without validating the entity or component.
    /// The caller must guarantee that the entity is alive and contains the component.
    /// </summary>
    public ref T GetUnchecked<T>(Entity entity)
        where T : IComponent
    {
        return ref GetStore<T>().GetUnchecked(entity);
    }

    /// <summary>
    /// Gets direct access to a component store.
    /// The store can be cached for high-performance repeated access.
    /// </summary>
    public ComponentStore<T> Store<T>()
        where T : IComponent
    {
        return GetStore<T>();
    }

    /// <summary>
    /// Removes a component from an entity.
    /// </summary>
    public bool Remove<T>(Entity entity)
        where T : IComponent
    {
        ValidateEntity(entity);
        return GetStore<T>().Remove(entity);
    }

    /// <summary>
    /// Determines whether an entity is currently alive.
    /// </summary>
    public bool IsAlive(Entity entity)
    {
        if (entity.Id >= (uint)_generations.Count)
            return false;

        return _generations[(int)entity.Id] == entity.Generation;
    }

    /// <summary>
    /// Gets all entities containing a component.
    /// </summary>
    public ReadOnlySpan<Entity> Query<T>()
        where T : IComponent
    {
        return GetStore<T>().Entities;
    }

    /// <summary>
    /// Gets all component data for a component type.
    /// Data uses the same dense ordering as Query<T>().
    /// </summary>
    public ReadOnlySpan<T> QueryData<T>()
        where T : IComponent
    {
        return GetStore<T>().Data;
    }

    /// <summary>
    /// Gets all entities and component data for a component type.
    /// The entity and component spans share the same indices.
    /// </summary>
    public void Query<T>(
        out ReadOnlySpan<Entity> entities,
        out ReadOnlySpan<T> components)
        where T : IComponent
    {
        var store = GetStore<T>();

        entities = store.Entities;
        components = store.Data;
    }

    /// <summary>
    /// Gets all entities containing both components.
    /// </summary>
    public List<Entity> Query<T1, T2>()
        where T1 : IComponent
        where T2 : IComponent
    {
        var first = GetStore<T1>();
        var second = GetStore<T2>();

        var result = new List<Entity>(
            Math.Min(first.Count, second.Count));

        var entities = first.Entities;

        for (var i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];

            if (second.Has(entity))
                result.Add(entity);
        }

        return result;
    }

    /// <summary>
    /// Sets the parent of an entity.
    /// </summary>
    public void SetParent(Entity child, Entity parent)
    {
        ValidateEntity(child);
        ValidateEntity(parent);

        if (child == parent)
            throw new InvalidOperationException(
                "An entity cannot be its own parent.");

        var current = parent;

        while (true)
        {
            if (current == child)
                throw new InvalidOperationException(
                    "Parenting would create a transform cycle.");

            var currentTransform = Get<Transform>(current);

            if (!currentTransform.HasParent)
                break;

            current = currentTransform.Parent;
        }

        var childTransform = Get<Transform>(child);

        childTransform.Parent = parent;
        childTransform.HasParent = true;
    }

    /// <summary>
    /// Gets the world-space transform matrix of an entity.
    /// </summary>
    public Matrix4 GetWorldMatrix(Entity entity)
    {
        ValidateEntity(entity);

        var transform = Get<Transform>(entity);
        var matrix = transform.LocalMatrix;

        while (transform.HasParent)
        {
            transform = Get<Transform>(transform.Parent);
            matrix *= transform.LocalMatrix;
        }

        return matrix;
    }

    /// <summary>
    /// Updates all components implementing IUpdatable.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var store in _componentStores)
        {
            if (store is IUpdatableStore updateableStore)
                updateableStore.Update(deltaTime);
        }
    }

    ComponentStore<T> GetStore<T>()
        where T : IComponent
    {
        var typeId = ComponentType<T>.Id;

        EnsureStoreCapacity(typeId);

        var cached = _typedComponentStores[typeId];

        if (cached is not null)
            return (ComponentStore<T>)cached;

        var store = _componentStores[typeId];

        if (store is not null)
        {
            var typedStore = (ComponentStore<T>)store;
            _typedComponentStores[typeId] = typedStore;
            return typedStore;
        }

        var newStore = new ComponentStore<T>();

        _componentStores[typeId] = newStore;
        _typedComponentStores[typeId] = newStore;

        return newStore;
    }

    void EnsureStoreCapacity(int typeId)
    {
        if (typeId < _componentStores.Length)
            return;

        var newCapacity = _componentStores.Length == 0
            ? 16
            : _componentStores.Length * 2;

        while (newCapacity <= typeId)
            newCapacity *= 2;

        Array.Resize(ref _componentStores, newCapacity);
        Array.Resize(ref _typedComponentStores, newCapacity);
    }

    void ValidateEntity(Entity entity)
    {
        if (!IsAlive(entity))
        {
            throw new InvalidOperationException(
                $"Entity {entity.Id}:{entity.Generation} is not alive.");
        }
    }

    void EnsureGenerationCapacity(uint id)
    {
        if (id > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Entity ID exceeds the addressable .NET array range.");
        }

        var required = (int)id + 1;

        while (_generations.Count < required)
            _generations.Add(0);
    }
}