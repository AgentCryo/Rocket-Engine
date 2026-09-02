using RCS;

namespace RCS.Component_Engine;

public interface IComponentStore
{
    bool Has(Entity entity);
    bool Remove(Entity entity);
}