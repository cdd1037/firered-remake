namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // Normal and faded are separate source buffers. Immutable transforms are
    // exact for same-bank copies, without reconstructing an indexed battler asset.
    // The source buffer is u16[0x1000], so each operand selects sixteen colors.
    private readonly Dictionary<int,SourcePaletteState> _unfadedPaletteStates=[];
    private sealed record PaletteBackupBank(int Bank,SourcePaletteState? State);
    private Dictionary<int,PaletteBackupBank>? _paletteBackup;
    private static bool IsPaletteBackupTask(string name)=>name is "AnimTask_AllocBackupPalBuffer" or "AnimTask_FreeBackupPalBuffer" or "AnimTask_CopyPalUnfadedToBackup" or "AnimTask_CopyPalUnfadedFromBackup" or "AnimTask_CopyPalFadedToUnfaded";
    private int BackupSelectedPalette(int selector)=>selector switch{0=>1,1=>16+_cue.Actor.Value,2=>16+_cue.Target.Value,_=>throw new InvalidDataException("Palette copy selector differs.")};
    private void ResetPaletteTransform(int bank)
    {_unfadedPaletteStates.Remove(bank);_paletteBlends.Remove(bank);_invertedPalettes.Remove(bank);if(bank>=16)_greyscale.Remove(bank-16);}
    private SourcePaletteBlend? UnfadedPaletteBlend(int bank)=>_unfadedPaletteStates.TryGetValue(bank,out var state)?new(bank,0,0){Unfaded=state}:null;
    private SourcePaletteState? FadedPaletteState(int bank)
    {
        var blend=_paletteBlends.GetValueOrDefault(bank);bool inverted=_invertedPalettes.Contains(bank),grey=bank>=16&&_greyscale.Contains(bank-16);
        return blend is null&&!inverted&&!grey?null:new(blend,inverted,grey);
    }
    private void SetUnfadedPaletteState(int bank,SourcePaletteState? state)
    {if(state is null)_unfadedPaletteStates.Remove(bank);else _unfadedPaletteStates[bank]=state;}
    private bool ValidatePaletteBackupOperand(SourceAnimationCommand c)
    {
        var a=c.Args;if(c.Op!="createvisualtask"||!IsPaletteBackupTask(a.FirstOrDefault()??""))return false;
        bool buffer=a[0] is "AnimTask_AllocBackupPalBuffer" or "AnimTask_FreeBackupPalBuffer";
        int length=buffer?2:a[0]=="AnimTask_CopyPalFadedToUnfaded"?3:4;
        if(a.Length!=length||Number(a[1]) is <0 or >255)Fail(c,"Palette backup task shape or priority differs.");
        if(!buffer&&(Number(a[2]) is <0 or >2||length==4&&Number(a[3]) is <0 or >255))Fail(c,"Palette backup selection exceeds its source buffer.");
        return true;
    }
    private void ValidatePaletteBackupProgram()
    {
        var commands=OverlayInvocationCommands();var first=commands.FirstOrDefault(c=>c.Op=="createvisualtask"&&IsPaletteBackupTask(c.Args[0]));if(first is null)return;
        if(commands.Any(c=>c.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"))Fail(first,"Palette backup ownership requires a linear invoked closure.");
        // This adapter has no numeric source palette for cross-bank copies or
        // direct partial writes. Only reviewed uniform-palette companions enter it.
        if(commands.Any(c=>c.Op is "fadetobg" or "changebg" or "restorebg"||
            c.Op=="createvisualtask"&&!IsPaletteBackupTask(c.Args[0])&&c.Args[0] is not ("AnimTask_ShakeMon" or "AnimTask_ShakeMon2" or "AnimTask_InvertScreenColor" or "AnimTask_BlendBattleAnimPal")||
            c.Op=="createsprite"&&c.Args[0] is not ("gOverheatFlameSpriteTemplate" or "gSimplePaletteBlendSpriteTemplate" or "gComplexPaletteBlendSpriteTemplate" or "gBasicHitSplatSpriteTemplate")))
            Fail(first,"Palette backup companion can replace or write an unrepresented source palette.");
        bool allocated=false;int version=0;Dictionary<int,(int Bank,int Version)> backups=[];Dictionary<int,int> normal=[];
        foreach(var c in commands)
        {
            var a=c.Args;if(c.Op!="createvisualtask"||!IsPaletteBackupTask(a[0]))continue;
            if(a[0]=="AnimTask_AllocBackupPalBuffer")
            {if(allocated)Fail(c,"Palette backup already has an owner.");allocated=true;backups.Clear();continue;}
            if(!allocated)Fail(c,"Palette backup buffer has no owner.");
            if(a[0]=="AnimTask_FreeBackupPalBuffer"){allocated=false;backups.Clear();continue;}
            int bank=BackupSelectedPalette(Number(a[2]));
            if(a[0]=="AnimTask_CopyPalFadedToUnfaded"){normal[bank]=++version;continue;}
            int index=Number(a[3]);
            if(a[0]=="AnimTask_CopyPalUnfadedToBackup"){backups[index]=(bank,normal.GetValueOrDefault(bank));continue;}
            if(!backups.TryGetValue(index,out var saved)||saved.Bank!=bank)Fail(c,"Palette restore requires its initialized same-bank backup.");
            normal[bank]=saved.Version;
        }
        if(allocated||normal.Values.Any(v=>v!=0))Fail(first,"Palette backup closure must free its buffer and restore normal palettes.");
    }
    private bool TryRunPaletteBackupTask(SourceAnimationCommand c)
    {
        string name=c.Args[0];if(!IsPaletteBackupTask(name))return false;
        if(name=="AnimTask_AllocBackupPalBuffer")
        {if(_paletteBackup is not null)throw new InvalidOperationException("Palette backup already allocated.");_paletteBackup=[];return true;}
        if(_paletteBackup is null)throw new InvalidOperationException("Palette backup is not allocated.");
        if(name=="AnimTask_FreeBackupPalBuffer"){_paletteBackup=null;return true;}
        int bank=BackupSelectedPalette(_animationArgs[0]);
        if(name=="AnimTask_CopyPalFadedToUnfaded"){SetUnfadedPaletteState(bank,FadedPaletteState(bank));return true;}
        int index=_animationArgs[1];
        if(index is <0 or >255)throw new InvalidDataException("Palette backup offset is outside its buffer.");
        if(name=="AnimTask_CopyPalUnfadedToBackup")_paletteBackup[index]=new(bank,_unfadedPaletteStates.GetValueOrDefault(bank));
        else
        {
            if(!_paletteBackup.TryGetValue(index,out var saved)||saved.Bank!=bank)throw new InvalidDataException("Palette restore has no initialized same-bank source.");
            SetUnfadedPaletteState(bank,saved.State);
        }
        return true;
    }
}
