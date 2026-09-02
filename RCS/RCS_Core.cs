namespace RCS;

/// <summary>
/// Core entry point for managing worlds and the active world.
/// </summary>
public static class RCS_Core
{
    static readonly Dictionary<string, World> Worlds = [];

    static string _activeWorld = "";

    /// <summary>
    /// Gets the currently active world.
    /// </summary>
    public static World GetActiveWorld()
    {
        if (string.IsNullOrEmpty(_activeWorld))
        {
            throw new InvalidOperationException(
                "Failed to get active world: Active world not set.");
        }

        if (!Worlds.TryGetValue(_activeWorld, out var world))
        {
            throw new InvalidOperationException(
                $"Active world '{_activeWorld}' was not found.");
        }

        return world;
    }

    /// <summary>
    /// Sets the active world by name.
    /// </summary>
    public static void SetActiveWorld(string name)
    {
        if (!Worlds.ContainsKey(name))
        {
            throw new KeyNotFoundException(
                $"World with name '{name}' was not found.");
        }

        _activeWorld = name;
    }

    /// <summary>
    /// Adds a world to the engine.
    /// </summary>
    public static void AddWorld(string name, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (Worlds.ContainsKey(name))
        {
            throw new InvalidOperationException(
                $"World with name '{name}' already exists.");
        }

        Worlds.Add(name, world);
    }

    /// <summary>
    /// Removes a world from the engine by name.
    /// </summary>
    public static void RemoveWorld(string name)
    {
        if (!Worlds.Remove(name))
        {
            throw new KeyNotFoundException(
                $"World with name '{name}' was not found.");
        }

        if (_activeWorld == name)
            _activeWorld = "";
    }

    /// <summary>
    /// Removes a world from the engine.
    /// </summary>
    public static void RemoveWorld(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (var pair in Worlds)
        {
            if (!ReferenceEquals(pair.Value, world))
                continue;

            Worlds.Remove(pair.Key);

            if (_activeWorld == pair.Key)
                _activeWorld = "";

            return;
        }

        throw new KeyNotFoundException("World was not found.");
    }
}