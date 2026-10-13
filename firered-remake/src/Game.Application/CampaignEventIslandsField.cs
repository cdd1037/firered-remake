using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignDeoxysRockEffect(string Kind, int InteractionNumber, int MovementTicks);

public sealed partial class CampaignSession
{
    private const string BirthIslandSource = "data/maps/BirthIsland_Exterior/scripts.inc";
    private const string NavelSummitSource = "data/maps/NavelRock_Summit/scripts.inc";
    private const string NavelBaseSource = "data/maps/NavelRock_Base/scripts.inc";
    private const string BirthTriangleScript = "BirthIsland_Exterior_EventScript_Triangle";
    private const string BirthDeoxysScript = "BirthIsland_Exterior_EventScript_Deoxys";
    private const string NavelHoOhScript = "NavelRock_Summit_EventScript_HoOh";
    private const string NavelLugiaScript = "NavelRock_Base_EventScript_Lugia";
    private const string NavelSummitMap = "map.navel-rock-summit";

    // This projection is derived from saved native variables, so Continue and UI-only
    // party returns restore the same palette without resetting map-entry state.
    public int? BirthIslandTrianglePalette => _content.EventIslandsEnabled && State.World.Player.MapId.Value == BirthIslandTriangleRules.Map
        ? State.Script.Variables.GetValueOrDefault(BirthIslandTriangleRules.InteractionVariable) : null;
    private static BirthIslandTriangleState TriangleState(CampaignState state) => new(
        state.Script.Variables.GetValueOrDefault(BirthIslandTriangleRules.InteractionVariable),
        state.Script.Variables.GetValueOrDefault(BirthIslandTriangleRules.StepVariable),
        state.Script.Flags.GetValueOrDefault(BirthIslandTriangleRules.AwakenedFlag));
    private CampaignState ApplyEventIslandWalkingStep(CampaignState state, string? sourceMap = null)
    {
        if (!_content.EventIslandsEnabled || (sourceMap ?? state.World.Player.MapId.Value) != BirthIslandTriangleRules.Map) return state;
        var next = BirthIslandTriangleRules.CountStep(TriangleState(state));
        return state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(BirthIslandTriangleRules.StepVariable, next.Steps) } };
    }
    private CampaignState ApplyEventIslandMapEntry(CampaignState state) => state with
    {
        DeoxysRockEffect = null,
        FieldWeather = _content.EventIslandsEnabled && state.World.Player.MapId.Value == NavelSummitMap ? "WEATHER_SHADE" : state.FieldWeather
    };
    private CampaignState ReturnToEventIslandField(CampaignState state)
    {
        if (!_content.EventIslandsEnabled || _content.MapDefinition(state.World.Player.MapId).Programs.OnReturnToField is not {} callback) return state;
        if (!state.Script.Completed || state.Script.Wait is not null || state.Script.Locked) throw new InvalidDataException("event_island_return_while_script_active");
        return Pump(StartStory(state, callback));
    }
    private string? ValidateEventIslandField(CampaignState state)
    {
        if (state.DeoxysRockEffect is not null) return "save_deoxys_rock_effect_active";
        if (!_content.EventIslandsEnabled) return null;
        if (state.World.Player.MapId.Value == NavelSummitMap)
        {
            if (state.FieldWeather is not (null or "WEATHER_SHADE") ||
                state.Script.Variables.GetValueOrDefault("VAR_TEMP_1") == 0 && state.FieldWeather != "WEATHER_SHADE") return "save_navel_summit_weather";
        }
        if (state.World.Player.MapId.Value != BirthIslandTriangleRules.Map) return null;
        var triangle = TriangleState(state);
        if (BirthIslandTriangleRules.Validate(triangle) is {} error) return "save_" + error;
        if (!state.Actors.TryGetValue(BirthIslandTriangleRules.RockActor, out var rock)) return "save_deoxys_rock_missing";
        var position = BirthIslandTriangleRules.Positions[triangle.InteractionNumber];
        if ((rock.X, rock.Y, rock.TemplateX, rock.TemplateY) != (position.X, position.Y, position.X, position.Y)) return "save_deoxys_rock_position";
        bool fought = state.Script.Flags.GetValueOrDefault("FLAG_FOUGHT_DEOXYS");
        bool flew = state.Script.Flags.GetValueOrDefault("FLAG_DEOXYS_FLEW_AWAY");
        if (rock.Active && state.Script.Flags.GetValueOrDefault(BirthIslandTriangleRules.RockHideFlag) || rock.Invisible ||
            (fought || flew || triangle.Awakened) && rock.Active) return "save_deoxys_rock_visibility";
        return null;
    }
    private static CampaignState TrianglePaletteEffect(CampaignState state, long sequence, int number) => state with
    {
        PresentationEffects = state.PresentationEffects.Add(new(sequence, "DeoxysTrianglePaletteResolved", state.World.Player.MapId,
            [number.ToString(System.Globalization.CultureInfo.InvariantCulture), "10", "ApplyGlobalFieldPaletteTint"]))
    };
    private CampaignState CompleteEventIslandPresentation(CampaignState before, CampaignState next)
    {
        if (before.DeoxysRockEffect is not {} effect) return next;
        if (before.World.Player.MapId.Value != BirthIslandTriangleRules.Map || before.Script.Wait?.Kind != StoryWaitKind.Presentation)
            throw new InvalidDataException("deoxys_rock_completion_context");
        if (effect.Kind == "destroy")
        {
            var rock = next.Actors[BirthIslandTriangleRules.RockActor];
            // RemoveObjectEventByLocalIdAndMap has the same hide-flag semantics as removeobject.
            next = next with { Actors = next.Actors.SetItem(rock.LocalId, rock with { Active = false, Invisible = false }),
                ObjectSlots = next.ObjectSlots?.RemoveRange(next.ObjectSlots.Where(x => x.Value == rock.LocalId).Select(x => x.Key)),
                Script = next.Script with { Flags = next.Script.Flags.SetItem(rock.HideFlag, true) } };
        }
        else if (effect.Kind != "move") throw new InvalidDataException("deoxys_rock_effect_kind");
        return next with { DeoxysRockEffect = null };
    }
    private bool TryApplyEventIslandFieldEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.EventIslandsEnabled) return false;
        var a = effect.Args;
        bool birth = state.World.Player.MapId.Value == BirthIslandTriangleRules.Map && effect.Source.Path == BirthIslandSource;
        bool hooh = state.World.Player.MapId.Value == NavelSummitMap && effect.Source.Path == NavelSummitSource && effect.Source.Symbol == NavelHoOhScript;
        bool lugia = state.World.Player.MapId.Value == "map.navel-rock-base" && effect.Source.Path == NavelBaseSource && effect.Source.Symbol == NavelLugiaScript;
        if (birth && effect.Opcode == "special" && a.SequenceEqual(new[] { "SetDeoxysTrianglePalette" }))
        {
            if (effect.Source.Symbol != "BirthIsland_Exterior_OnReturnToField" || BirthIslandTriangleRules.Validate(TriangleState(state)) is not null)
                throw new InvalidDataException("deoxys_palette_source_context");
            next = TrianglePaletteEffect(state, effect.Sequence, TriangleState(state).InteractionNumber); return true;
        }
        if (birth && effect.Opcode == "special" && a.SequenceEqual(new[] { "DoDeoxysTriangleInteraction" }))
        {
            if (effect.Source.Symbol != BirthTriangleScript || !state.Script.Locked || state.DeoxysRockEffect is not null ||
                !state.Actors.TryGetValue(BirthIslandTriangleRules.RockActor, out var rock) || !rock.Visible)
                throw new InvalidDataException("deoxys_triangle_source_context");
            var result = BirthIslandTriangleRules.Interact(TriangleState(state));
            next = state with { Script = state.Script with {
                Variables = state.Script.Variables.SetItem(BirthIslandTriangleRules.InteractionVariable, result.State.InteractionNumber)
                    .SetItem(BirthIslandTriangleRules.StepVariable, result.State.Steps).SetItem("VAR_RESULT", result.Result),
                Flags = state.Script.Flags.SetItem(BirthIslandTriangleRules.AwakenedFlag, result.State.Awakened) } };
            if (result.MovementTicks > 0)
            {
                next = TrianglePaletteEffect(next, effect.Sequence, result.State.InteractionNumber);
                next = next with { Actors = next.Actors.SetItem(rock.LocalId, rock with { X = result.X, Y = result.Y, TemplateX = result.X, TemplateY = result.Y }),
                    DeoxysRockEffect = new("move", result.State.InteractionNumber, result.MovementTicks), PresentationTicks = result.MovementTicks,
                    PresentationEffects = next.PresentationEffects.Add(new(effect.Sequence, "playse", state.World.Player.MapId, [result.Sound!]))
                        .Add(new(effect.Sequence, "DeoxysTriangleMoveResolved", state.World.Player.MapId,
                            [rock.LocalId, rock.X.ToString(), rock.Y.ToString(), result.X.ToString(), result.Y.ToString(), result.MovementTicks.ToString()])) };
            }
            return true;
        }
        if (birth && effect.Source.Symbol == BirthTriangleScript && effect.Opcode == "waitstate")
        {
            if (state.Script.Wait is not { Kind: StoryWaitKind.Presentation } wait) throw new InvalidDataException("deoxys_triangle_wait_missing");
            // The solved/already-awakened native task immediately reenables the script.
            if (state.DeoxysRockEffect is null) next = state with { Script = state.Script with { Wait = wait with { Kind = StoryWaitKind.NativeEffect } } };
            return true;
        }
        if (birth && effect.Source.Symbol == BirthDeoxysScript && effect.Opcode == "setfieldeffectargument")
        {
            if (!state.Script.Locked || !TriangleState(state).Awakened ||
                !(a.SequenceEqual(new[] { "0", BirthIslandTriangleRules.RockActor }) ||
                  a.SequenceEqual(new[] { "1", "MAP_NUM(MAP_BIRTH_ISLAND_EXTERIOR)" }) ||
                  a.SequenceEqual(new[] { "2", "MAP_GROUP(MAP_BIRTH_ISLAND_EXTERIOR)" })))
                throw new InvalidDataException("deoxys_destroy_argument_context");
            return true;
        }
        if (birth && effect.Source.Symbol == BirthDeoxysScript && effect.Opcode is "dofieldeffect" or "waitfieldeffect" &&
            a.SequenceEqual(new[] { "FLDEFF_DESTROY_DEOXYS_ROCK" }))
        {
            if (!state.Script.Locked || !TriangleState(state).Awakened || !state.Actors[BirthIslandTriangleRules.DeoxysActor].Active)
                throw new InvalidDataException("deoxys_destroy_source_context");
            if (effect.Opcode == "dofieldeffect")
            {
                if (state.DeoxysRockEffect is not null || !state.Actors[BirthIslandTriangleRules.RockActor].Visible)
                    throw new InvalidDataException("deoxys_destroy_rock_missing");
                // Native task priorities 80/90: setup, 121 shake ticks, 4*21 decay ticks,
                // then task 80 observes completion. Fragment/flash is the 122nd tick.
                next = state with { DeoxysRockEffect = new("destroy", 10, 206), PresentationTicks = 206,
                    PresentationEffects = state.PresentationEffects.Add(new(effect.Sequence, "DeoxysRockDestroyResolved", state.World.Player.MapId,
                        ["122", "4", "21", "4", "16", "12", "RGB_WHITE", "SE_THUNDER2", "SE_THUNDER"])) };
            }
            else if (state.DeoxysRockEffect?.Kind != "destroy" || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("deoxys_destroy_wait_missing");
            return true;
        }
        if ((hooh || lugia || birth && effect.Source.Symbol == BirthDeoxysScript) && effect.Opcode == "playmoncry")
        {
            string species = hooh ? "SPECIES_HO_OH" : lugia ? "SPECIES_LUGIA" : "SPECIES_DEOXYS";
            if (!a.SequenceEqual(new[] { species, "CRY_MODE_ENCOUNTER" })) throw new InvalidDataException("event_island_cry_context");
            return true; // Preserve the source encounter-cry mode for the audio adapter.
        }
        if ((hooh || lugia || birth && effect.Source.Symbol == BirthTriangleScript) && effect.Opcode == "special" && a.SequenceEqual(new[] { "QuestLog_CutRecording" }))
        {
            if (state.Script.QuestLogState != 0) throw new InvalidDataException("event_island_quest_log_context");
            return true;
        }
        if (hooh && effect.Opcode is "setweather" or "doweather")
        {
            if (!state.Script.Locked || effect.Opcode == "setweather" && !a.SequenceEqual(new[] { "0" }) || effect.Opcode == "doweather" && !a.IsEmpty)
                throw new InvalidDataException("navel_weather_source_context");
            next = state with { FieldWeather = null }; return true;
        }
        if (hooh && effect.Opcode == "special" && a.SequenceEqual(new[] { "LoopWingFlapSound" }))
        {
            if (!state.Script.Locked || state.Script.Variables.GetValueOrDefault("VAR_0x8004") != 3 || state.Script.Variables.GetValueOrDefault("VAR_0x8005") != 35)
                throw new InvalidDataException("navel_wing_sound_context");
            next = state with { PresentationEffects = state.PresentationEffects.Add(new(effect.Sequence, "HoOhWingFlapResolved", state.World.Player.MapId, ["3", "35", "SE_M_WING_ATTACK"])) }; return true;
        }
        if (lugia && effect.Opcode == "special" && a.SequenceEqual(new[] { "ShakeScreen" }))
        {
            int[] values = [.. new[] { "VAR_0x8004", "VAR_0x8005", "VAR_0x8006", "VAR_0x8007" }.Select(v => state.Script.Variables.GetValueOrDefault(v))];
            if (!state.Script.Locked || !values.SequenceEqual(new[] { 0, 3, 4, 2 })) throw new InvalidDataException("navel_lugia_shake_context");
            next = state with { PresentationEffects = state.PresentationEffects.Add(new(effect.Sequence, "LugiaShakeResolved", state.World.Player.MapId, ["0", "3", "4", "2"])) }; return true;
        }
        if (hooh && effect.Opcode == "special" && a.Length == 1 && a[0] is "SpawnCameraObject" or "RemoveCameraObject")
        {
            const string id = "LOCALID_CAMERA";
            if (!state.Script.Locked) throw new InvalidDataException("navel_camera_lock_context");
            if (a[0] == "SpawnCameraObject")
            {
                if (state.Actors.ContainsKey(id)) throw new InvalidDataException("navel_camera_already_spawned");
                int slot = state.ObjectSlots is null ? 1 : Enumerable.Range(1, 15).FirstOrDefault(i => !state.ObjectSlots.ContainsKey(i));
                if (slot == 0) throw new InvalidDataException("navel_camera_object_pool_full");
                var p = state.World.Player;
                var camera = new CampaignActor(id, 127, "OBJ_EVENT_GFX_YOUNGSTER", p.X, p.Y, 3, Direction.South, "MOVEMENT_TYPE_FACE_DOWN", "NULL", "0", true, true, p.X, p.Y);
                next = state with { Actors = state.Actors.Add(id, camera), ObjectSlots = state.ObjectSlots?.Add(slot, id) };
            }
            else
            {
                if (!state.Actors.TryGetValue(id, out var camera) || !camera.Active || !camera.Invisible) throw new InvalidDataException("navel_camera_missing");
                next = state with { Actors = state.Actors.Remove(id), ObjectSlots = state.ObjectSlots?.RemoveRange(state.ObjectSlots.Where(x => x.Value == id).Select(x => x.Key)) };
            }
            return true;
        }
        return false;
    }
}
