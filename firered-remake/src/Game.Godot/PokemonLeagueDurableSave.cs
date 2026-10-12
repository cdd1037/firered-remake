using Game.Application;

/// <summary>Completes a save request only after the committed primary, not a backup, reads back exactly.</summary>
internal static class PokemonLeagueDurableSave
{
    internal static void Write<T>(ISaveStorage storage,string slot,T state,long generation,string payloadHash,
        string rules,string content,Func<T,string?> validate)
    {
        if(CanonicalJson.Hash(state)!=payloadHash)throw new InvalidDataException("hall_of_fame_request_hash_differs");
        new SaveService(storage).Save(slot,state,generation,rules,content,validate);
        var primary=storage.Read(slot)??throw new IOException("hall_of_fame_primary_missing_after_commit");
        var saved=SaveCodec.Decode<T>(primary,rules,content,slot,validate);
        if(saved.Generation!=generation||CanonicalJson.Hash(saved.State)!=payloadHash)
            throw new IOException("hall_of_fame_primary_readback_differs");
    }
}
