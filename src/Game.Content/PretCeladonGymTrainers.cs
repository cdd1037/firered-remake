using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record CeladonGymTrainerDocument(int SchemaVersion,ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames);
/// <summary>Source-only loader. No catalog promotion, battle factory or campaign registration is implied.</summary>
public static class PretCeladonGymTrainers
{
    public static CeladonGymTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var d=ContentJson.ParseStrict<CeladonGymTrainerDocument>(Encoding.UTF8.GetString(Read(root,"content/generated/data/trainer-battles/celadon-gym.json")),SharedTrainerCatalog.Options(root, canonical.ReferenceOptions()));
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported CeladonGym trainer document schema.");
        CeladonGymTrainerPartyFactory.ValidateCatalog(new(d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.Trainer)));
        return d;
    }
    public static CeladonGymTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root)
    {var d=LoadDocument(canonical, root);return new(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private static byte[] Read(string root,string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative)&&!Path.IsPathRooted(relative)&&!relative.Contains(':')&&!relative.Contains('\\')&&!relative.Split('/').Any(p=>p is "" or "." or ".."),"Unsafe Celadon Gym source path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));Require(f.LinkTarget is null,"Celadon Gym symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent)Require(d.LinkTarget is null,"Celadon Gym parent symlink rejected.");
        var bytes=File.ReadAllBytes(f.FullName);
return bytes;
    }
}
