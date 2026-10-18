using Game.Application;
using Game.Domain.FirstBattle;
using Game.Domain.Events;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Pointer adapter for the currently drawn menus. Existing key handlers own
/// commands, validation, timing and callbacks; the world has no pointer action.</summary>
public partial class CampaignView
{
    private bool _menuMousePressed, _menuRightMousePressed;
    private ulong _menuMouseActionFrame = ulong.MaxValue;
    private static readonly Rect2 MouseMessage = new(0, 112, 240, 48);

    private void ResetMenuMouseInput() { _menuMousePressed = false; _menuRightMousePressed = false; }

    // Observe release before a focused LineEdit can consume it after a naming transition.
    // No action is ever dispatched here or deferred until release.
    public override void _Input(InputEvent input)
    {
        ObserveCampaignButtonInput(input);
        if (_startupReady && TryNamingJoypadInput(input)) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        { _menuMousePressed = false; _fieldTextHeld=false; }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        { _menuRightMousePressed = false; _fieldTextHeld = false; }
    }

    private bool TryMenuMouseInput(InputEvent input)
    {
        if (input is not InputEventMouseButton && input is not InputEventMouseMotion) return false;
        var mouse = input as InputEventMouseButton;
        bool motion = input is InputEventMouseMotion;
        bool wheel = mouse?.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown;
        bool right = mouse?.ButtonIndex == MouseButton.Right;
        if (!motion && !wheel && !right && mouse!.ButtonIndex != MouseButton.Left) return true;
        if (mouse is { Pressed: false })
        {
            if (right) _menuRightMousePressed = false;
            else if (!wheel) _menuMousePressed = false;
            return true;
        }
        if (motion || wheel)
        {
            // Dragging never changes a menu, including a popup created by the press.
            if (_menuMousePressed || _menuRightMousePressed ||
                input is InputEventMouseMotion { ButtonMask: not 0 }) return true;
        }
        else
        {
            if (_menuMousePressed || _menuRightMousePressed) return true;
            if (right) _menuRightMousePressed = true; else _menuMousePressed = true;
        }
        if (_busy && _session?.State.Pc?.View is CampaignPcView.Move or CampaignPcView.MoveItems or CampaignPcView.Deposit or CampaignPcView.Withdraw && mouse is { Pressed: true, DoubleClick: false })
        { _pcNotice = StorageMoveBusyNotice; QueueRedraw(); GetViewport().SetInputAsHandled(); return true; }
        if (mouse is { DoubleClick: true } || _menuMouseActionFrame == Engine.GetProcessFrames() ||
            _busy || (!NamingScreenActive && (_session is null || _fade > 0 || (_session.State.ScreenIsBlack && _session.EasyChatEditor is null && !_session.State.TrainerTowerRecordsVisible && !MoveTutorActive) ||
            _tracks.Any(t => t.Time < t.Duration)))) return true;

        // Godot already removes viewport stretch/letterboxing before _UnhandledInput.
        // Reject the real viewport border before removing the canvas/node transform.
        Vector2 position = ((InputEventMouse)input).Position;
        if (!GetViewport().GetVisibleRect().HasPoint(position)) return true;
        Vector2 point = GetGlobalTransformWithCanvas().AffineInverse() * position;
        if (!OpeningItemUiSkin.LogicalRect.HasPoint(point)) return true;
        if (NamingScreenActive)
        {
            if (!motion && !wheel)
            {
                if (right)
                {
                    if (_namingBinding!.Context.IsOpening) NamingEscape();
                }
                else NamingPointer(point);
                _menuMouseActionFrame = Engine.GetProcessFrames();
            }
            QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
        }
        if (_session is null) return true;
        if (_session.State.Nickname is { Submitted: false } || CaptureNamingActive || StorageBoxNaming ||
            _session.State.OldManTutorial is not null || _session.State.Evolution is { Stage: not CampaignEvolutionStage.LearnMove } ||
            _session.State.Repel?.Phase == CampaignRepelPhase.UseSound) return true;

        if (motion || wheel)
        {
            // This route cannot confirm. Hover only changes presentation cursors;
            // the wheel sends only Up/Down to an explicitly recognized visible list.
            int direction = !wheel ? 0 : mouse!.ButtonIndex == MouseButton.WheelUp ? -1 : 1;
            if (NavigateMenuPointer(point, direction))
            { _held = null; _repeat = 0; QueueRedraw(); GetViewport().SetInputAsHandled(); }
            return true;
        }
        if (right)
        {
            if (_session.EasyChatEditor is not null || _session.State.Evolution is { Stage: CampaignEvolutionStage.LearnMove } || _session.State.RareCandy is not null || TownMapActive || FieldMenuOpen || _session.State.Shop is not null ||
                _session.State.ItemMenu is not null || _session.State.Pc is not null ||
                _session.State.SafariBattle is not null || DoubleBattleVisible || ForestTrainerCombatVisible || _session.State.WildBattle is not null ||
                _session.State.Battle is not null || FieldDialogueVisible || MoveTutorActive || DaisyGroomingChoosing || SizeRecordChoosing || NameRaterChoosing || NpcTradeChoosing || NpcTradePresenting || TrainerTowerMenuActive || _session.State.TrainerTowerRecordsVisible || SeagallopMenuActive || SilphElevatorMenuActive || DepartmentElevatorMenuActive || RooftopMenuActive || GameCornerCoinMenuActive || GameCornerPrizeMenuActive || RocketElevatorMenuActive || SourceMenuActive || SourceGridMenuActive || FossilLabMenuActive || BillPcMenuActive || BillPcPictureActive ||
                _session.State.Repel?.Phase == CampaignRepelPhase.WoreOff)
            { MouseKey(Key.Backspace); QueueRedraw(); GetViewport().SetInputAsHandled(); }
            else if (CanOpenFieldMenu)
            { MouseKey(Key.Tab); QueueRedraw(); GetViewport().SetInputAsHandled(); }
            return true;
        }

        bool acted = _session.EasyChatEditor is not null ? EasyChatEditorMouse(point, null) : _session.State.Evolution is { Stage: CampaignEvolutionStage.LearnMove } ? MouseEvolutionLearning(point) : SourceMenuActive ? MouseSourceMenu(point) : SourceGridMenuActive ? MouseSourceGridMenu(point) : TrainerTowerMenuActive || _session.State.TrainerTowerRecordsVisible ? MouseTrainerTower(point) : MoveTutorActive ? MouseMoveTutor(point) : SeagallopMenuActive ? MouseSeagallopMenu(point) : SilphElevatorMenuActive ? MouseSilphElevator(point) : NpcTradeChoosing || NpcTradePresenting ? MouseNpcTrade(point) : DaisyGroomingChoosing ? MouseDaisyGrooming(point) : SizeRecordChoosing ? MouseSizeRecord(point) : NameRaterChoosing ? MouseNameRater(point) : GameCornerPrizeMenuActive ? MouseGameCornerPrizeMenu(point) : GameCornerCoinMenuActive ? MouseGameCornerCoinMenu(point) : RooftopMenuActive ? MouseRooftopMenu(point) : DepartmentElevatorMenuActive ? MouseDepartmentElevator(point) : _session.State.RareCandy is not null ? MouseRareCandy(point)
            : TownMapActive ? MouseTownMap(point)
            : FieldMenuOpen ? MouseFieldMenu(point)
            : _session.State.Shop is not null ? MouseShop(point)
            : _session.State.ItemMenu is not null ? MouseItems(point)
            : _session.State.Pc is not null ? MouseStorage(point)
            : _session.State.SafariBattle is not null ? MouseSafari(point)
            : DoubleBattleVisible ? MouseDouble(point)
            : ForestTrainerCombatVisible || _session.State.WildBattle is not null ? MouseWild(point)
            : _session.State.Battle is not null ? MouseLabBattle(point)
            : RocketElevatorMenuActive ? MouseRocketElevator(point)
            : FossilLabMenuActive ? MouseFossilLab(point) : BillPcMenuActive||BillPcPictureActive ? MouseBillPc(point) : MouseFieldDialogue(point);
        if (acted) { QueueRedraw(); GetViewport().SetInputAsHandled(); }
        return true;
    }

    // A wheel event is one keyboard navigation step. It does not reserve a click
    // frame: several notches may arrive in one frame, and none can change UI mode.
    private bool MouseNavigationKey(int direction)
    {
        using var key = new InputEventKey { Keycode = direction < 0 ? Key.Up : Key.Down, Pressed = true };
        _UnhandledInput(key);
        return true;
    }
    private bool PointerRows(Vector2 point, int direction, ref int cursor, int count,
        float x, float y, float width, float step = 16, int start = 0)
    {
        if (count <= 0 || !new Rect2(x, y, width, count * step).HasPoint(point)) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        int selected = start + (int)((point.Y - y) / step);
        if (cursor == selected) return false;
        cursor = selected;
        return true;
    }
    private bool PointerList(Vector2 point, int direction, ref int cursor, int stackCount, bool pc = false, bool scrolls = true)
    {
        int start = scrolls ? Math.Max(0, Math.Min(cursor, stackCount) - 5) : 0;
        return PointerRows(point, direction, ref cursor, Math.Min(6, stackCount + 1 - start), pc ? 56 : 88, 8, pc ? 152 : 144, 16, start);
    }
    private bool PointerYesNo(Vector2 point, int direction, ref int cursor) => PointerRows(point, direction, ref cursor, 2, 184, 72, 56);
    private static bool PointerGrid(Vector2 point, int direction, ref int cursor, bool moves, int count)
    {
        float x = moves ? 8 : 128, width = moves ? 72 : 56;
        if (direction != 0 || !new Rect2(x, 120, width * 2, 32).HasPoint(point)) return false;
        int selected = (int)((point.X - x) / width) + 2 * (int)((point.Y - 120) / 16);
        if (selected >= count || selected == cursor) return false;
        cursor = selected;
        return true;
    }
    private bool NavigateMenuPointer(Vector2 point, int direction)
    {
        if(_session.EasyChatEditor is not null)return EasyChatEditorMouse(point,direction);
        if(SourceMenuActive)return NavigateSourceMenuPointer(point,direction);
        if(SourceGridMenuActive)return NavigateSourceGridPointer(point,direction);
        if(TrainerTowerMenuActive)return NavigateTrainerTowerPointer(point,direction);
        if(_session.State.TrainerTowerRecordsVisible)return false;
        if(SeagallopMenuActive)return NavigateSeagallopPointer(point,direction);
        if(SilphElevatorMenuActive)return NavigateSilphPointer(point,direction);
        if(MoveTutorActive)return NavigateMoveTutorPointer(point,direction);
        if(NpcTradeChoosing||NpcTradePresenting)return NavigateNpcTradePointer(point,direction);
        if(DaisyGroomingChoosing)return NavigateDaisyGroomingPointer(point,direction);
        if(SizeRecordChoosing)return NavigateSizeRecordPointer(point,direction);
        if(NameRaterChoosing)return NavigateNameRaterPointer(point,direction);
        if(GameCornerPrizeMenuActive)return NavigateGameCornerPrizePointer(point,direction);
        if(GameCornerCoinMenuActive)return NavigateGameCornerCoinPointer(point,direction);
        if(RooftopMenuActive)return NavigateRooftopPointer(point,direction);
        if(DepartmentElevatorMenuActive)return NavigateDepartmentPointer(point,direction);
        // Preserve exactly the same top-level owner as the click/drawing routes.
        if (_session.State.Evolution is { Stage: CampaignEvolutionStage.LearnMove }) return NavigateEvolutionLearning(point, direction);
        if (_session.State.RareCandy is {} candy)
            return _candyNotice is null && candy.Stage == CampaignRareCandyStage.LearnMove && NavigateMoveSelectionSummary(point, direction, ref _candyMoveCursor,
                _session.State.Party.Single(p => p.InstanceId == candy.Result.Pokemon.InstanceId).Pokemon.Moves.Length);
        if (TownMapActive) return false;
        if (FieldMenuOpen) return NavigateFieldMenuPointer(point, direction);
        if (_session.State.Shop is { } shop)
        {
            if (_shopNotice is not null || shop.Stage == CampaignShopStage.Message) return false;
            if (shop.Stage == CampaignShopStage.Menu) return PointerRows(point, direction, ref _shopCursor, 3, 144, 8, 96);
            if (shop.Stage == CampaignShopStage.Quantity)
                return direction != 0 && (MouseMessage.HasPoint(point) || new Rect2(0, 72, 80, 40).HasPoint(point)) && MouseNavigationKey(direction);
            if (shop.Stage == CampaignShopStage.Confirmation) return PointerRows(point, direction, ref _shopConfirm, 2, 0, 72, 64);
            int start = Math.Max(0, _shopCursor - 5);
            return PointerRows(point, direction, ref _shopCursor, Math.Min(6, _session.VisibleShopItems.Length + 1 - start), 80, 8, 160, 16, start);
        }
        if (_session.State.ItemMenu is not null) return NavigateItemsPointer(point, direction);
        if (_session.State.Pc is { } pc)
        {
            if (pc.Summary is not null) return _pcNotice is null && _session.StorageSummaryPokemon is { } summaryPokemon && NavigateOwnedSummaryPointer(point, direction, summaryPokemon);
            if (_pcNotice is not null || _storagePartyPeek) return false;
            if (StorageCustomizationOverlayActive) return NavigateStorageCustomizationPointer(point, direction);
            if (StorageCreatureOverlayActive) return NavigateStorageCreaturePointer(point, direction);
            if (pc.View == CampaignPcView.Move) return NavigateStorageMovePointer(point, direction);
            if (pc.View == CampaignPcView.MoveItems) return NavigateStorageItemPointer(point, direction);
            if (pc.View is CampaignPcView.Main or CampaignPcView.StorageMenu)
                return PointerRows(point, direction, ref _pcCursor, pc.View == CampaignPcView.Main ? _session.PcMenuTextSymbols.Length : 5, 8, 8, 184);
            if (pc.View == CampaignPcView.Deposit)
            {
                if (!new Rect2(8, 24, 224, (_session.State.Party.Length + 1) * 16).HasPoint(point)) return false;
                bool titleFocused = _storageTitleSelected; _storageTitleSelected = false;
                return PointerRows(point, direction, ref _pcCursor, _session.State.Party.Length + 1, 8, 24, 224) || titleFocused;
            }
            if (pc.View == CampaignPcView.Withdraw && new Rect2(88, 32, 144, 120).HasPoint(point))
            {
                if (direction != 0) return MouseNavigationKey(direction);
                int selected = (int)((point.X - 88) / 24) + 6 * (int)((point.Y - 32) / 24);
                if (_boxCursor == selected && !_storageTitleSelected) return false;
                _storageTitleSelected = false; _boxCursor = selected; return true;
            }
            return false;
        }
        if (_session.State.SafariBattle is not null) return NavigateSafariPointer(point, direction);
        if (DoubleBattleVisible) return NavigateDoublePointer(point,direction);
        if (ForestTrainerCombatVisible || _session.State.WildBattle is not null) return NavigateWildPointer(point, direction);
        if (_session.State.Battle is { } battle)
        {
            if (_battleNotice is not null || battle.Phase != CampaignBattlePhase.ChooseMove) return false;
            if (_battleMenu == "bag") return PointerList(point, direction, ref _battleCursor, _session.State.Inventory?.BagItems.Length ?? 0);
            if (_battleMenu == "bag-actions") return PointerRows(point, direction, ref _battleCursor, 2, 168, 130, 72);
            if (_battleMenu is "party" or "medicine-target") return PointerNativeParty(point,direction,ref _battleCursor,1);
            return _battleMenu is "actions" or "moves" && PointerGrid(point, direction, ref _battleCursor, _battleMenu == "moves",
                _battleMenu == "actions" ? 4 : FirstBattleRules.RequiresStruggle(battle.State.Player) ? 1 : battle.State.Player.Pokemon.Moves.Length);
        }
        if (!FieldDialogueVisible || !FieldTextComplete) return false;
        if(RocketElevatorMenuActive)return NavigateRocketPointer(point,direction);
        if(FossilLabMenuActive)return NavigateFossilLabPointer(point,direction);
        if(BillPcMenuActive||BillPcPictureActive)return NavigateBillPcPointer(point,direction);
        if(BikeShopMenuActive)return NavigateBikeShopPointer(point,direction);
        var wait = _session.State.Script.Wait;
        if (wait?.Kind is not (StoryWaitKind.Choice or StoryWaitKind.MenuChoice) ||
            _session.State.Dialogue?.Page + 1 != _session.State.Dialogue?.Pages.Length) return false;
        bool menu = wait.Kind == StoryWaitKind.MenuChoice;
        float x = menu ? 144 : FieldChoiceFrameX, y = menu ? 64 : FieldChoiceHitY, step = menu ? 14 : FieldChoiceRowHeight;
        float width = menu ? (((int)Math.Max(_sourceNormal.Font.GetStringSize("YES", fontSize: _sourceNormal.Size).X,
            _sourceNormal.Font.GetStringSize("NO", fontSize: _sourceNormal.Size).X) + 9) / 8 + 1) * 8 + 16 : 64;
        if (!new Rect2(x, y, width, step * 2).HasPoint(point)) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        bool yes = point.Y < y + step;
        if (_choiceYes == yes) return false;
        _choiceYes = yes; return true;
    }
    private bool NavigateFieldMenuPointer(Vector2 point, int direction)
    {
        if (_fieldMenuMode == "field-heal-animation") return false;
        if (_fieldMenuMode == "field-move-confirm") return PointerRows(point, direction, ref _fieldMoveConfirmChoice, 2, 176, 72, 64);
        if (_fieldMenuNotice is not null) return false;
        if (_fieldMenuMode is "pokedex-contents" or "pokedex-habitat" or "pokedex-area") return NavigatePokedexExtraPointer(point, direction);
        if (_fieldMenuMode == "pokedex-order") return NavigatePokedexOrderPointer(point, direction);
        if (_fieldMenuMode == "pokedex")
        {
            var layout = _pokedexMenu!.ListLayout;
            int rows = Math.Min(layout.VisibleRows, _pokedexList!.Rows.Length - _pokedexCursor.ScrollOffset);
            if (!new Rect2(layout.X, layout.Y + layout.TopY, layout.WindowWidth, rows * layout.RowHeight).HasPoint(point)) return false;
            if (direction != 0) return MouseNavigationKey(direction);
            int row = (int)((point.Y - layout.Y - layout.TopY) / layout.RowHeight);
            if (_pokedexCursor.Row == row) return false;
            _pokedexCursor = new(_pokedexCursor.ScrollOffset, row); return true;
        }
        if (_fieldMenuMode == "summary") return NavigateOwnedSummaryPointer(point, direction, _session.State.Party[_fieldPartyCursor]);
        if (_fieldMenuMode is "pokedex-entry" or "trainer-card") return false;
        if (_fieldMenuMode == "options") return PointerRows(point, direction, ref _optionsCursor, 7, 16, 58, 208, 13);
        if (_fieldMenuMode is "save-confirm" or "save-overwrite") return PointerRows(point, direction, ref _fieldSaveChoice, 2, 192, 73, 40);
        if (_fieldMenuMode == "start") return PointerRows(point, direction, ref _fieldMenuCursor, FieldMenuEntries().Length, 176, 8, 56, 15);
        if (_fieldMenuMode == "actions") return PointerRows(point, direction, ref _fieldPartyAction, FieldPartyActions().Length, 160, FieldPartyActionTop, 72);
        return PointerNativeParty(point, direction, ref _fieldPartyCursor, _session.State.Party.Length);
    }
    private bool NavigateItemsPointer(Vector2 point, int direction)
    {
        var menu = _session.State.ItemMenu!;
        if(menu.Toss is not null)return BagTossMouse(point,direction);
        if (_itemNotice is not null || menu.View == "message") return false;
        if (HeldItemScreen) return HeldItemMouse(point, direction);
        if (MachineActive)
        {
            if (_machineMode == "actions") return PointerRows(point, direction, ref _machineCursor, 3, 168, 104, 72, 16);
            if (_machineMode == "notice") return false;
            if (_machineMode is "teach-confirm" or "replace-confirm")
                return _machinePage + 1 == _machinePages.Length && PointerYesNo(point, direction, ref _machineCursor);
            if (_machineMode == "list") return PointerRows(point, direction, ref _machineCursor, MachineListVisibleCount, MachineListX, MachineListY, MachineListWidth, MachineListRowHeight, MachineListStart);
            if (_machineMode == "party") return PointerNativeParty(point, direction, ref _machineCursor, _session.State.Party.Length);
            if (_machineMode == "replacement") return NavigateMoveSelectionSummary(point, direction, ref _machineCursor, _session.State.Party[_machineParty].Pokemon.Moves.Length);
            return false;
        }
        if (MailScreen) return MailMouse(point, direction);
        if (MtMoonTargetActive && (PpBoostSelectionStale || VitaminSelectionStale || EvolutionSelectionStale)) return MouseKey();
        if (MtMoonTargetActive && _mtMoonMoveSlotOpen) return PointerRows(point, direction, ref _mtMoonMoveSlot, _session.State.Party[_fieldMedicineTarget].Pokemon.Moves.Length + 1, 12, 26, 220);
        if (_fieldMedicineTargetOpen) return PointerNativeParty(point, direction, ref _fieldMedicineTarget, _session.State.Party.Length);
        if (QuestPocketEnabled && _questPocket > 0)
        {
            if (_questPocketNotice is not null) return false;
            if (_questPocketPopup)
            {
                int count = QuestPocketActions().Length;
                return PointerRows(point, direction, ref _questPocketAction, count, 168, 154 - count * 16, 72);
            }
            return PointerList(point, direction, ref _questPocketCursor, QuestPocketStacks.Length, scrolls: true);
        }
        if (_itemQuantityOpen) return direction != 0 && MouseMessage.HasPoint(point) && MouseNavigationKey(direction);
        if (_itemPopup) return PointerRows(point, direction, ref _itemAction, ItemActionCount, 168, ItemActionY, 72);
        if (menu.View is "top" or "storage") return PointerRows(point, direction, ref _itemCursor, 3, 8, 8, menu.View == "top" ? 104 : 112);
        return PointerList(point, direction, ref _itemCursor,
            (menu.View == "withdraw" ? _session.State.Inventory!.PcItems : FieldItemStacks).Length, menu.View == "withdraw");
    }
    private bool NavigateWildPointer(Vector2 point, int direction)
    {
        var battle = PresentedWildBattle!;
        if (_wildNotice is not null) return false;
        if (_battlePartySelection is not null) return NavigateBattlePartyInspection(point, direction);
        if (_session.State.ForestTrainerBattle is null && battle.Capture is { } capture)
            return capture.Stage == CampaignCaptureStage.NicknameChoice && PointerYesNo(point, direction, ref _wildCursor);
        if (battle.Phase == CampaignBattlePhase.ChooseMove)
        {
            if (battle.Replacement == CampaignReplacementStage.Select)
                return PointerNativeParty(point, direction, ref _wildCursor, _session.State.Party.Length,cancel:false);
            if (battle.Replacement == CampaignReplacementStage.UseNext) return PointerYesNo(point, direction, ref _wildCursor);
            if (ForestTrainerShiftPending)
                return _wildPage + 1 == _wildPages.Length && PointerYesNo(point, direction, ref _wildCursor);
        }
        if (battle.Phase == CampaignBattlePhase.ChooseMove && SourceBattleForcedMoveSlot(battle) is {} forced) { _wildCursor=forced; return false; }
        if (_wildMenu == "learn-moves") return NavigateMoveSelectionSummary(point, direction, ref _wildCursor, WildRewardRecipient(battle).Pokemon.Moves.Length);
        if (_wildMenu == "learn-confirm") return PointerYesNo(point, direction, ref _wildCursor);
        if (_wildMenu == "learn-intro" || battle.Phase != CampaignBattlePhase.ChooseMove) return false;
        if (_wildMenu == "bag") return PointerList(point, direction, ref _wildCursor,
            CaptureBagStacks.Length,
            scrolls: true);
        if (_wildMenu == "bag-actions") return PointerRows(point, direction, ref _wildCursor, _captureBerries ? 3 : 2, 168, _captureBerries ? 106 : 130, 72);
        if (_wildMenu == "medicine-move") return PointerRows(point,direction,ref _wildCursor,_session.State.Party[_singleMedicinePartyIndex].Pokemon.Moves.Length+1,12,10,220);
        if (_wildMenu is "party" or "medicine-target")
            return PointerNativeParty(point, direction, ref _wildCursor, _session.State.Party.Length);
        return _wildMenu is "actions" or "moves" && PointerGrid(point, direction, ref _wildCursor, _wildMenu == "moves",
            _wildMenu == "actions" ? 4 : SourceBattleRequiresStruggle(battle) ? 1 : battle.State.Player.Pokemon.Moves.Length);
    }

    private bool MouseKey(Key key = Key.Enter)
    {
        // Reserve this frame before synchronous dispatch: a second callback cannot
        // act on the page/menu produced by this click, even after a synthetic release.
        _menuMouseActionFrame = Engine.GetProcessFrames();
        _held = null;
        using var pressed = new InputEventKey { Keycode = key, Pressed = true };
        _UnhandledInput(pressed);
        return true;
    }
    private bool MouseConfirm(Vector2 point, Rect2 bounds) => bounds.HasPoint(point) && MouseKey();
    private bool MouseSelect(Vector2 point, ref int cursor, int count, float x, float y, float width, float step = 16, int start = 0)
    {
        if (count <= 0 || !new Rect2(x, y, width, count * step).HasPoint(point)) return false;
        cursor = start + (int)((point.Y - y) / step);
        return MouseKey();
    }
    private bool MouseGrid(Vector2 point, ref int cursor, bool moves, int count)
    {
        float x = moves ? 8 : 128, width = moves ? 72 : 56;
        if (!new Rect2(x, 120, width * 2, 32).HasPoint(point)) return false;
        int selected = (int)((point.X - x) / width) + 2 * (int)((point.Y - 120) / 16);
        if (selected >= count) return false;
        cursor = selected; return MouseKey();
    }
    private bool MouseList(Vector2 point, ref int cursor, int stackCount, bool pc = false, bool scrolls = true)
    {
        int start = scrolls ? Math.Max(0, Math.Min(cursor, stackCount) - 5) : 0;
        return MouseSelect(point, ref cursor, Math.Min(6, stackCount + 1 - start), pc ? 56 : 88, 8, pc ? 152 : 144, 16, start);
    }
    private bool MouseBagArrows(Vector2 point, int pocket)
    {
        if (pocket > 0 && new Rect2(0, 64, 16, 16).HasPoint(point)) return MouseKey(Key.Left);
        if (pocket < 2 && _itemSkin.Rect("pocket-right-arrow").HasPoint(point)) return MouseKey(Key.Right);
        return false;
    }
    private bool MouseYesNo(Vector2 point, ref int cursor) => MouseSelect(point, ref cursor, 2, 184, 72, 56);

    private bool MouseFieldDialogue(Vector2 point)
    {
        if(BikeShopMenuActive)return MouseBikeShop(point);
        if (_session.State.ForestTrainerBattle?.Stage == CampaignForestTrainerStage.Intro && _wildNotice is not null)
            return MouseConfirm(point, MouseMessage);
        if (!FieldDialogueVisible) return false;
        if(!FieldTextComplete)return MouseConfirm(point,MouseMessage);
        var wait = _session.State.Script.Wait;
        if (wait?.Kind is StoryWaitKind.Choice or StoryWaitKind.MenuChoice &&
            _session.State.Dialogue?.Page + 1 == _session.State.Dialogue?.Pages.Length)
        {
            bool menu = wait.Kind == StoryWaitKind.MenuChoice;
            float x = menu ? 144 : FieldChoiceFrameX, y = menu ? 64 : FieldChoiceHitY, step = menu ? 14 : FieldChoiceRowHeight;
            float width = menu ? (((int)Math.Max(_sourceNormal.Font.GetStringSize("YES", fontSize: _sourceNormal.Size).X,
                _sourceNormal.Font.GetStringSize("NO", fontSize: _sourceNormal.Size).X) + 9) / 8 + 1) * 8 + 16 : 64;
            if (!new Rect2(x, y, width, step * 2).HasPoint(point)) return false;
            _choiceYes = point.Y < y + step;
            return MouseKey();
        }
        return (wait?.Kind is StoryWaitKind.Dialog or StoryWaitKind.MessagePrinting or StoryWaitKind.Choice or StoryWaitKind.MenuChoice or StoryWaitKind.ButtonPress ||
            _session.State.ForestTrainerBattle?.Stage == CampaignForestTrainerStage.Intro ||
            _session.State.DoubleBattle?.Stage == CampaignDoubleBattleStage.Intro ||
            _session.State.Repel?.Phase == CampaignRepelPhase.WoreOff) && MouseConfirm(point, MouseMessage);
    }

    private bool MouseItems(Vector2 point)
    {
        var menu = _session.State.ItemMenu!;
        if(menu.Toss is not null)return BagTossMouse(point);
        if (_itemNotice is not null) return MouseConfirm(point, MouseMessage);
        if (MailScreen) return MailMouse(point);
        if (HeldItemScreen) return HeldItemMouse(point);
        if (MachineActive) return MouseMachines(point);
        if (menu.View == "message") return MouseConfirm(point, MouseMessage);
        if (MtMoonTargetActive && (PpBoostSelectionStale || VitaminSelectionStale || EvolutionSelectionStale)) return MouseKey();
        if (MtMoonTargetActive && _mtMoonMoveSlotOpen) return MouseSelect(point, ref _mtMoonMoveSlot, _session.State.Party[_fieldMedicineTarget].Pokemon.Moves.Length + 1, 12, 26, 220);
        if (_fieldMedicineTargetOpen) return MouseNativeParty(point, ref _fieldMedicineTarget, _session.State.Party.Length);
        if (QuestPocketEnabled && _questPocket > 0)
        {
            if (_questPocketNotice is not null) return MouseConfirm(point, MouseMessage);
            if (_questPocketPopup)
            {
                int count = QuestPocketActions().Length;
                return MouseSelect(point, ref _questPocketAction, count, 168, 154 - count * 16, 72);
            }
            return MouseBagArrows(point, _questPocket) || MouseList(point, ref _questPocketCursor, QuestPocketStacks.Length, scrolls: true);
        }
        if (_itemQuantityOpen) return MouseConfirm(point, MouseMessage);
        if (_itemPopup) return MouseSelect(point, ref _itemAction, ItemActionCount, 168, ItemActionY, 72);
        if (menu.View is "top" or "storage") return MouseSelect(point, ref _itemCursor, 3, 8, 8, menu.View == "top" ? 104 : 112);
        if (QuestPocketEnabled && MouseBagArrows(point, 0)) return true;
        return MouseList(point, ref _itemCursor, (menu.View == "withdraw" ? _session.State.Inventory!.PcItems : FieldItemStacks).Length, menu.View == "withdraw");
    }

    private bool MouseMachines(Vector2 point)
    {
        if (_machineMode == "actions") return MouseSelect(point, ref _machineCursor, 3, 168, 104, 72, 16);
        if (_session.State.ItemMenu!.View == "message" || _machineMode == "notice") return MouseConfirm(point, MouseMessage);
        if (_machineMode is "teach-confirm" or "replace-confirm")
            return _machinePage + 1 < _machinePages.Length ? MouseConfirm(point, MouseMessage) : MouseYesNo(point, ref _machineCursor);
        if (_machineMode == "list") return MouseSelect(point, ref _machineCursor, MachineListVisibleCount, MachineListX, MachineListY, MachineListWidth, MachineListRowHeight, MachineListStart);
        if (_machineMode == "party") return MouseNativeParty(point, ref _machineCursor, _session.State.Party.Length);
        if (_machineMode == "replacement") return MouseMoveSelectionSummary(point, ref _machineCursor, _session.State.Party[_machineParty].Pokemon.Moves.Length);
        return false;
    }

    private bool MouseShop(Vector2 point)
    {
        var shop = _session.State.Shop!;
        if (_shopNotice is not null || shop.Stage == CampaignShopStage.Message) return MouseConfirm(point, MouseMessage);
        if (shop.Stage == CampaignShopStage.Menu) return MouseSelect(point, ref _shopCursor, 3, 144, 8, 96);
        if (shop.Stage == CampaignShopStage.Quantity) return MouseConfirm(point, MouseMessage);
        if (shop.Stage == CampaignShopStage.Confirmation) return MouseSelect(point, ref _shopConfirm, 2, 0, 72, 64);
        int start = Math.Max(0, _shopCursor - 5);
        return MouseSelect(point, ref _shopCursor, Math.Min(6, _session.VisibleShopItems.Length + 1 - start), 80, 8, 160, 16, start);
    }

    private bool MouseLabBattle(Vector2 point)
    {
        var battle = _session.State.Battle!;
        if (_battleNotice is not null || battle.Phase != CampaignBattlePhase.ChooseMove) return MouseConfirm(point, MouseMessage);
        if (_battleMenu == "bag") return MouseList(point, ref _battleCursor, _session.State.Inventory?.BagItems.Length ?? 0);
        if (_battleMenu == "bag-actions") return MouseSelect(point, ref _battleCursor, 2, 168, 130, 72);
        if (_battleMenu is "party" or "medicine-target") return MouseNativeParty(point,ref _battleCursor,1);
        return _battleMenu is "actions" or "moves" && MouseGrid(point, ref _battleCursor, _battleMenu == "moves",
            _battleMenu == "actions" ? 4 : FirstBattleRules.RequiresStruggle(battle.State.Player) ? 1 : battle.State.Player.Pokemon.Moves.Length);
    }

    private bool MouseWild(Vector2 point)
    {
        var battle = PresentedWildBattle!;
        if (_wildNotice is not null) return MouseConfirm(point, MouseMessage);
        if (_battlePartySelection is not null) return MouseBattlePartyInspection(point);
        if (_session.State.ForestTrainerBattle is null && battle.Capture is { } capture)
        {
            if (capture.Stage == CampaignCaptureStage.NicknameChoice) return MouseYesNo(point, ref _wildCursor);
            if (capture.Stage is CampaignCaptureStage.DexNotice or CampaignCaptureStage.SentToPc) return MouseConfirm(point, MouseMessage);
            if (capture.Stage == CampaignCaptureStage.DexEntry && _captureDex is { } dex)
                return MouseConfirm(point, new Rect2(dex.Layout.FooterRightX - 40, dex.Layout.FooterY, 40, 16));
            return false;
        }
        if (battle.Phase == CampaignBattlePhase.ChooseMove)
        {
            if (battle.Replacement == CampaignReplacementStage.Select)
                return MouseNativeParty(point, ref _wildCursor, _session.State.Party.Length,cancel:false);
            if (battle.Replacement == CampaignReplacementStage.UseNext) return MouseYesNo(point, ref _wildCursor);
            if (ForestTrainerShiftPending)
                return _wildPage + 1 < _wildPages.Length ? MouseConfirm(point, MouseMessage) : MouseYesNo(point, ref _wildCursor);
        }
        if (battle.Phase == CampaignBattlePhase.ChooseMove && SourceBattleForcedMoveSlot(battle) is {} forced)
        { _wildCursor=forced; return MouseConfirm(point,new Rect2(LabBattleUiSkin.MoveText(forced)-new Vector2(8,0),new Vector2(72,16))); }
        if (_wildMenu == "learn-intro") return MouseConfirm(point, MouseMessage);
        if (_wildMenu == "learn-moves") return MouseMoveSelectionSummary(point, ref _wildCursor, WildRewardRecipient(battle).Pokemon.Moves.Length);
        if (_wildMenu == "learn-confirm") return MouseYesNo(point, ref _wildCursor);
        if (battle.Phase != CampaignBattlePhase.ChooseMove) return MouseConfirm(point, MouseMessage);
        if (_wildMenu == "bag")
        {
            return !_captureBerries && MouseBagArrows(point, _captureBagPocket) ||
                MouseList(point, ref _wildCursor, CaptureBagStacks.Length, scrolls: true);
        }
        if (_wildMenu == "bag-actions") return MouseSelect(point, ref _wildCursor, _captureBerries ? 3 : 2, 168, _captureBerries ? 106 : 130, 72);
        if (_wildMenu == "medicine-move") return MouseSelect(point,ref _wildCursor,_session.State.Party[_singleMedicinePartyIndex].Pokemon.Moves.Length+1,12,10,220);
        if (_wildMenu is "party" or "medicine-target")
            return MouseNativeParty(point, ref _wildCursor, _session.State.Party.Length);
        return _wildMenu is "actions" or "moves" && MouseGrid(point, ref _wildCursor, _wildMenu == "moves",
            _wildMenu == "actions" ? 4 : SourceBattleRequiresStruggle(battle) ? 1 : battle.State.Player.Pokemon.Moves.Length);
    }
}
