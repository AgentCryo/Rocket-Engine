using RCS.Components;

namespace RCS.Component_Engine;

public sealed class ComponentStore<T> : IUpdatableStore
    where T : IComponent
{
    const uint InvalidIndex = uint.MaxValue;

    uint[] _sparse = [];
    Entity[] _entities = [];
    T[] _data = [];

    static readonly bool IsUpdatable = typeof(IUpdatable).IsAssignableFrom(typeof(T));

    public int Count { get; private set; }

    public ref T Get(Entity entity)
    {
        var index = GetIndex(entity);
        return ref _data[index];
    }

    // RCS-internal hot path. Caller guarantees the entity exists in this store.
    public ref T GetUnchecked(Entity entity) => ref _data[_sparse[(int)entity.Id]];

    public bool Has(Entity entity)
    {
        if (entity.Id >= (uint)_sparse.Length)
            return false;

        var index = _sparse[(int)entity.Id];

        if (index == InvalidIndex || index >= (uint)Count)
            return false;

        return _entities[index] == entity;
    }

    public void Add(Entity entity, T component)
    {
        if (Has(entity))
            throw new InvalidOperationException($"Entity {entity.Id} already has component {typeof(T).Name}.");

        EnsureSparseCapacity(entity.Id);
        EnsureDenseCapacity(Count + 1);

        var index = Count++;

        _entities[index] = entity;
        _data[index] = component;
        _sparse[(int)entity.Id] = (uint)index;
    }

    public bool Remove(Entity entity)
    {
        if (!Has(entity))
            return false;

        var index = (int)_sparse[(int)entity.Id];
        var lastIndex = Count - 1;

        if (index != lastIndex)
        {
            var movedEntity = _entities[lastIndex];
            var movedComponent = _data[lastIndex];

            _entities[index] = movedEntity;
            _data[index] = movedComponent;
            _sparse[(int)movedEntity.Id] = (uint)index;
        }

        _entities[lastIndex] = default;
        _data[lastIndex] = default!;

        _sparse[(int)entity.Id] = InvalidIndex;
        Count--;

        return true;
    }

    public ReadOnlySpan<Entity> Entities => _entities.AsSpan(0, Count);
    public ReadOnlySpan<T> Data => _data.AsSpan(0, Count);

    public void Update(float deltaTime)
    {
        if (!IsUpdatable)
            return;

        for (var i = 0; i < Count; i++)
            ((IUpdatable)_data[i]).Update(deltaTime);
    }

    int GetIndex(Entity entity)
    {
        if (!Has(entity))
            throw new KeyNotFoundException($"Entity {entity.Id} does not have component {typeof(T).Name}.");

        return (int)_sparse[(int)entity.Id];
    }

    void EnsureSparseCapacity(uint entityId)
    {
        if (entityId < (uint)_sparse.Length)
            return;

        if (entityId > int.MaxValue)
            throw new InvalidOperationException("Entity ID exceeds the addressable .NET array range.");

        var required = (int)entityId + 1;
        var oldLength = _sparse.Length;
        var newCapacity = oldLength == 0 ? 16 : oldLength * 2;

        while (newCapacity < required)
            newCapacity *= 2;

        Array.Resize(ref _sparse, newCapacity);
        Array.Fill(_sparse, InvalidIndex, oldLength, newCapacity - oldLength);
    }

    void EnsureDenseCapacity(int required)
    {
        if (_data.Length >= required)
            return;

        var newCapacity = _data.Length == 0 ? 16 : _data.Length * 2;

        while (newCapacity < required)
            newCapacity *= 2;

        Array.Resize(ref _data, newCapacity);
        Array.Resize(ref _entities, newCapacity);
    }
}