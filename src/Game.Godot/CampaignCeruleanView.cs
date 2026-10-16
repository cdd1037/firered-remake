using Game.Domain.Items;
using Game.Content;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;
public partial class CampaignView
{
    private void LoadCeruleanPresentation()
    {
        if (_catalog.CeruleanPokemon is not { } source) return;

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
        foreach(var asset in PretCeruleanTrainerArt.Load(_root))
        {
            using var image=NativeImages.LoadPng(asset.Png, 64, 64);
            _forestTrainerPortraits.Add(asset.Id,ImageTexture.CreateFromImage(image));
        }
        var machineIcon=ItemIconAssets.LoadProject(_root, "content/generated/data/machine-learning/cerulean.json", MachineRules.Tm03Id).Single();
        _earlyItemIcons.Add(machineIcon.Item,ItemTexture("data/machine-learning", machineIcon));
    }
}
