using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Items;

public static class ItemIds
{
    public static ItemId AuroraTicketId { get; } = new("pret:ITEM_AURORA_TICKET");
    public static ItemId BigPearlId { get; } = new("pret:ITEM_BIG_PEARL");
    public static ItemId CardKeyId { get; } = new("pret:ITEM_CARD_KEY");
    public static ItemId DireHitId { get; } = new("pret:ITEM_DIRE_HIT");
    public static ItemId DragonScaleId { get; } = new("pret:ITEM_DRAGON_SCALE");
    public static ItemId DreamMailId { get; } = new("pret:ITEM_DREAM_MAIL");
    public static ItemId FabMailId { get; } = new("pret:ITEM_FAB_MAIL");
    public static ItemId FireStoneId { get; } = new("pret:ITEM_FIRE_STONE");
    public static ItemId GuardSpecId { get; } = new("pret:ITEM_GUARD_SPEC");
    public static ItemId HeartScaleId { get; } = new("pret:ITEM_HEART_SCALE");
    public static ItemId IapapaBerryId { get; } = new("pret:ITEM_IAPAPA_BERRY");
    public static ItemId ItemfinderId { get; } = new("pret:ITEM_ITEMFINDER");
    public static ItemId KingsRockId { get; } = new("pret:ITEM_KINGS_ROCK");
    public static ItemId LeftoversId { get; } = new("pret:ITEM_LEFTOVERS");
    public static ItemId LemonadeId { get; } = new("pret:ITEM_LEMONADE");
    public static ItemId LuckyPunchId { get; } = new("pret:ITEM_LUCKY_PUNCH");
    public static ItemId LuxuryBallId { get; } = new("pret:ITEM_LUXURY_BALL");
    public static ItemId MachoBraceId { get; } = new("pret:ITEM_MACHO_BRACE");
    public static ItemId MasterBallId { get; } = new("pret:ITEM_MASTER_BALL");
    public static ItemId MaxRepelId { get; } = new("pret:ITEM_MAX_REPEL");
    public static ItemId MetalCoatId { get; } = new("pret:ITEM_METAL_COAT");
    public static ItemId MeteoriteId { get; } = new("pret:ITEM_METEORITE");
    public static ItemId MysticTicketId { get; } = new("pret:ITEM_MYSTIC_TICKET");
    public static ItemId NeverMeltIceId { get; } = new("pret:ITEM_NEVER_MELT_ICE");
    public static ItemId OldAmberId { get; } = new("pret:ITEM_OLD_AMBER");
    public static ItemId PokeDollId { get; } = new("pret:ITEM_POKE_DOLL");
    public static ItemId PpMaxId { get; } = new("pret:ITEM_PP_MAX");
    public static ItemId RainbowPassId { get; } = new("pret:ITEM_RAINBOW_PASS");
    public static ItemId RetroMailId { get; } = new("pret:ITEM_RETRO_MAIL");
    public static ItemId RubyId { get; } = new("pret:ITEM_RUBY");
    public static ItemId SacredAshId { get; } = new("pret:ITEM_SACRED_ASH");
    public static ItemId SapphireId { get; } = new("pret:ITEM_SAPPHIRE");
    public static ItemId SecretKeyId { get; } = new("pret:ITEM_SECRET_KEY");
    public static ItemId SitrusBerryId { get; } = new("pret:ITEM_SITRUS_BERRY");
    public static ItemId StickId { get; } = new("pret:ITEM_STICK");
    public static ItemId SunStoneId { get; } = new("pret:ITEM_SUN_STONE");
    public static ItemId ThunderStoneId { get; } = new("pret:ITEM_THUNDER_STONE");
    public static ItemId Tm04Id { get; } = new("pret:ITEM_TM04");
    public static ItemId Tm17Id { get; } = new("pret:ITEM_TM17");
    public static ItemId Tm25Id { get; } = new("pret:ITEM_TM25");
    public static ItemId TriPassId { get; } = new("pret:ITEM_TRI_PASS");
    public static ItemId UltraBallId { get; } = new("pret:ITEM_ULTRA_BALL");
    public static ItemId UpGradeId { get; } = new("pret:ITEM_UP_GRADE");
    public static ItemId VsSeekerId { get; } = new("pret:ITEM_VS_SEEKER");
    public static ItemId WaterStoneId { get; } = new("pret:ITEM_WATER_STONE");
    public const string TrainerTowerLobbyShopId = "TrainerTower_Lobby_Mart_Items";
    public static ImmutableArray<ItemId> GameCornerPrizeItems { get; } = new[] { "SMOKE_BALL", "MIRACLE_SEED", "CHARCOAL", "MYSTIC_WATER", "YELLOW_FLUTE" }.Select(name => new ItemId("pret:ITEM_" + name)).ToImmutableArray();
    public static ImmutableArray<ItemId> LostCaveItems { get; } = new[] { "SILK_SCARF", "SEA_INCENSE", "LAX_INCENSE", "LUXURY_BALL" }.Select(name => new ItemId("pret:ITEM_" + name)).ToImmutableArray();
}
