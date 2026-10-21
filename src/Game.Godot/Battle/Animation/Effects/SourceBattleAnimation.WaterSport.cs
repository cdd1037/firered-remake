namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string WaterSportTaskName = "AnimTask_WaterSport";

    private bool ValidateWaterSportCommand(SourceAnimationCommand command)
    {
        if (command.Op != "createvisualtask" || command.Args.FirstOrDefault() != WaterSportTaskName) return false;
        if (command.Args.Length != 2 || Number(command.Args[1]) is < 0 or > 255)
            Fail(command, "Water Sport takes only source task priority.");
        if (_script.SineTable.Length != 320 || !_script.SpriteDependencies.Contains(SmallWaterOrb))
            Fail(command, "Water Sport raw child or source sine table is missing.");
        ValidateSquashWaterFireRecipe(command, SmallWaterOrb);
        RequireOverlaySpritePalette(command, "ANIM_TAG_GLOWY_BLUE_ORB");
        RequireVisualSeed(command);
        if (_validatingInvokedProgram)
        {
            if (!IsSourceBattlerPresent(_cue.Actor.Value)) Fail(command, "Water Sport requires a present actor.");
            ValidateReactionGeometry(command, _battlers[_cue.Actor.Value]);
            var closure = OverlayInvocationCommands();
            // The native destructor scans by task callback, without a stored task
            // id. A unique, non-reentrant emitter gives every raw child one owner.
            if (closure.Count(c => c.Op == "createvisualtask" && c.Args.FirstOrDefault() == WaterSportTaskName) != 1 ||
                closure.Any(c => c.Op == "createsprite" && c.Args.FirstOrDefault() == SmallWaterOrb) ||
                closure.Any(c => c.Op is "call" or "return" or "goto" or "choosetwoturnanim" || c.Op.StartsWith("jump", StringComparison.Ordinal)))
                Fail(command, "Water Sport raw droplets require one unambiguous task owner.");
        }
        return true;
    }

    private bool TryCreateWaterSportTask(SourceAnimationCommand command)
    {
        if (command.Args[0] != WaterSportTaskName) return false;
        if (!_loaded.Contains("ANIM_TAG_GLOWY_BLUE_ORB")) Fail(command, "Water Sport child graphics have not been loaded.");
        _tasks.Add(new WaterSportTask(this));
        return true;
    }

    private sealed class WaterSportTask : VisualTask
    {
        internal readonly short[] Data = new short[16];

        public WaterSportTask(SourceBattleAnimation owner)
        {
            var origin = owner._battlers[owner._cue.Actor.Value].Center(owner._doubles, true);
            Data[3] = (short)origin.X;
            Data[4] = (short)origin.Y;
            Data[7] = (short)(owner._cue.Actor.IsPlayer ? 1 : -1);
            Data[5] = (short)(Data[3] + Data[7] * 8);
            Data[6] = (short)(Data[4] - Data[7] * 8);
            Data[9] = -32;
        }

        public override void Step(SourceBattleAnimation owner)
        {
            var d = Data;
            switch (d[0])
            {
                case 0:
                    Emit(owner);
                    if (d[10] != 0) d[0]++;
                    break;
                case 1:
                    Emit(owner);
                    if (++d[1] > 16) { d[1] = 0; d[0]++; }
                    break;
                case 2:
                    Emit(owner);
                    d[5] += (short)(d[7] * 6);
                    if (d[5] is < -16 or > 256)
                    {
                        if (++d[12] > 2) { d[13] = 1; d[0] = 6; d[1] = 0; }
                        else { d[1] = 0; d[0]++; }
                    }
                    break;
                case 3:
                    Emit(owner);
                    d[6] -= (short)(d[7] * 2);
                    if (++d[1] > 7) d[0]++;
                    break;
                case 4:
                    Emit(owner);
                    d[5] -= (short)(d[7] * 6);
                    if (d[5] is < -16 or > 256) { d[12]++; d[1] = 0; d[0]++; }
                    break;
                case 5:
                    Emit(owner);
                    d[6] -= (short)(d[7] * 2);
                    if (++d[1] > 7) d[0] = 2;
                    break;
                case 6:
                    if (d[8] == 0) d[0]++;
                    break;
                default:
                    Finished = true;
                    break;
            }
        }

        private void Emit(SourceBattleAnimation owner)
        {
            var d = Data;
            if (++d[2] <= 1) return;
            d[2] = 0;
            int slot = owner.FindFreeSpriteSlot();
            // Failed CreateSprite does not increment the live-child counter or
            // consume visual RNG. The two-frame emission timer still resets.
            if (slot == SourceSpriteRegistry<VisualSprite>.Capacity) return;
            owner._sprites.Add(new WaterSportDroplet(slot, owner._script.SpriteRecipes[SmallWaterOrb],
                new(d[3], d[4]), new(d[5], d[6]), d[9], this));
            owner._oamY[slot] = 0;
            d[8]++;
        }
    }

    private sealed class WaterSportDroplet : VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly WaterSportTask _parent;
        private PixelPoint _position, _offset;
        private HorizontalArc _arc;
        private int _phase, _amplitude;

        public WaterSportDroplet(int slot, SourceSpriteRecipe recipe, PixelPoint origin, PixelPoint destination, int amplitude, WaterSportTask parent)
            : base(slot, 10)
        {
            _recipe = recipe; _parent = parent; _position = origin; _amplitude = amplitude;
            _arc = new(destination.X - origin.X, destination.Y - origin.Y, 16, amplitude);
        }

        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame => new(Slot, SmallWaterOrb, _position, _offset, 0, true, false, false, 256, _recipe.Asset, Subpriority) { Width = 8, Height = 8 };

        public override void Step(SourceBattleAnimation owner)
        {
            if (!_arc.Step(owner._script.SineTable, out var offset)) { _offset = offset; return; }
            if (_phase == 0)
            {
                _position = new(unchecked((short)(_position.X + _offset.X)), unchecked((short)(_position.Y + _offset.Y)));
                int dx = (owner.NextVisualRandom() & 31) - 16;
                int dy = (owner.NextVisualRandom() & 31) - 16;
                _amplitude = ~(owner.NextVisualRandom() & 7);
                _arc = new(dx, dy, 6, _amplitude);
                // The source intentionally retains x2/y2 on this handoff frame.
                _phase = 1;
            }
            else
            {
                _parent.Data[10] = 1;
                _parent.Data[8]--;
                Finished = true;
            }
        }
    }
}
