using DevPack4Dataverse.Mapper.Lock;

namespace DevPack4Dataverse.Mapper.Internal;

internal static class EntityLockGuard
{
    public static void EnsureHeld(IEntityLockHandle handle)
    {
        if (handle is IEntityLockState { IsLost: true })
        {
            throw new MapperLockException(
                "The distributed lock was lost before the write. The operation was not sent."
            );
        }
    }
}
