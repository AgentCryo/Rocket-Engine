namespace RCS.Component_Engine;

public interface IUpdatableStore : IComponentStore
{
    void Update(float deltaTime);
}