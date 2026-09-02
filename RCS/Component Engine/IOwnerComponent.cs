namespace RCS.Component_Engine;

public interface IOwnerComponent : IComponent
{
    public Entity Owner { get; set; }
}