namespace RCS;

public readonly struct Entity : IEquatable<Entity>
{
    public readonly uint Id;
    public readonly uint Generation;

    public Entity(uint id, uint generation)
    {
        Id = id;
        Generation = generation;
    }

    public bool Equals(Entity other) => Id == other.Id && Generation == other.Generation;
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, Generation);

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}