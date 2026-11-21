using Microsoft.Xrm.Sdk;

namespace DevPack4Dataverse.Mapper.Internal;

/// <summary>
/// Restores alternate-key values after a save. The values are removed from the request body
/// before an upsert, but the cache and post-save validation still need to see them.
/// </summary>
internal static class AlternateKeyAttributes
{
    public static void Restore(Entity entity, EntityKey? key)
    {
        if (entity == null || key == null || !key.IsAlternateKey)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in key.AlternateKey)
        {
            entity.Attributes[pair.Key] = pair.Value;
        }
    }
}
