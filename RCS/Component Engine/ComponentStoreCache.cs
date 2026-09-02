namespace RCS.Component_Engine;

static class ComponentStoreCache<T> where T : IComponent
{
    public static ComponentStore<T>? Store;
}