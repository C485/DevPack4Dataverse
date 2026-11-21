namespace DevPack4Dataverse.Mapper.Lock;

/// <summary>
/// Optional lock capability: tells the caller whether a distributed lock was lost while it was
/// held, for example because a renewal failed or another process took over after the TTL.
/// </summary>
public interface IEntityLockState
{
    bool IsLost { get; }
}
