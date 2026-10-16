using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record FuchsiaGymTrainerDocument(int SchemaVersion,ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames);
/// <summary>Source-only loader. No catalog promotion, battle factory or campaign registration is implied.</summary>
public sealed record FuchsiaGymTrainerCatalogContent(ImmutableDictionary<TrainerId,TrainerDefinition> Trainers,ImmutableDictionary<TrainerId,CeruleanTrainerBattleDefinition> Battles);
public static class PretFuchsiaGymTrainers
{
    public static FuchsiaGymTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var d=ContentJson.ParseStrict<FuchsiaGymTrainerDocument>(Encoding.UTF8.GetString(Read(root,"content/generated/data/trainer-battles/fuchsia-gym.json")),SharedTrainerCatalog.Options(root, canonical.ReferenceOptions()));
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported FuchsiaGym trainer document schema.");
        FuchsiaGymTrainerPartyFactory.ValidateCatalog(new(d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.Trainer)));
        return d;
    }
    public static FuchsiaGymTrainerCatalogContent Load(PretCanonicalPokemonCatalog canonical, string root)
    {var d=LoadDocument(canonical, root);return new(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private static byte[] Read(string root,string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative)&&!Path.IsPathRooted(relative)&&!relative.Contains(':')&&!relative.Contains('\\')&&!relative.Split('/').Any(p=>p is "" or "." or ".."),"Unsafe Fuchsia Gym source path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));Require(f.LinkTarget is null,"Fuchsia Gym symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent)Require(d.LinkTarget is null,"Fuchsia Gym parent symlink rejected.");
        var bytes=File.ReadAllBytes(f.FullName);
return bytes;
    }
}
