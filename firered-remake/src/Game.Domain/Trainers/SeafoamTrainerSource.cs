using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;

// Generated from the 21 pinned Route19/20 first-battle teams and 22 source actor entries.
public static partial class SeafoamTrainerPartyFactory
{
    public static readonly ImmutableDictionary<TrainerId, string> TrainerHashes = new Dictionary<TrainerId, string> {
        [new("pret:TRAINER_SWIMMER_MALE_REECE")] = "4c9378e1eb2e8d3e6a8e461bd8f4734af41d77652206a61b97985c5d462f6dff",
        [new("pret:TRAINER_SWIMMER_MALE_RICHARD")] = "01d1aa66e556783ecb60a361d6721e0afe3e3c022f0d4bc5f995cfdb1583e3ec",
        [new("pret:TRAINER_SWIMMER_MALE_TONY")] = "025c65356202291634b1aa840fa654e1c564681b32ee2ccc6f8774870bfa0d5d",
        [new("pret:TRAINER_SWIMMER_MALE_MATTHEW")] = "4f245eedc26b7bcbfa3e2c8dc5cb4b11e991b036c04ae2cb026b6d0a36eb1592",
        [new("pret:TRAINER_SWIMMER_MALE_DOUGLAS")] = "f158a5c2e2ca489a7af4112b21c3e7d980066f710d891c3ab106672467162699",
        [new("pret:TRAINER_SWIMMER_MALE_DAVID")] = "21a353c4f512e6d04d46b82a7d8ec9cd3a32fd7ffd621b86545a3bb92a8321f1",
        [new("pret:TRAINER_SWIMMER_MALE_AXLE")] = "28c1f3334f36d85788972758d337012cfe93aa46307e07007fb0ea8d2a32a8ed",
        [new("pret:TRAINER_SWIMMER_FEMALE_ALICE")] = "f1105a921e2c15effc3bfe0efbb2b94b6bdb497cbcc99070791c0da893896e4c",
        [new("pret:TRAINER_SWIMMER_FEMALE_CONNIE")] = "d26fcbb9ea8cf37665ee28806988e1f772d790943e130179e320f18fce7111cc",
        [new("pret:TRAINER_SWIMMER_FEMALE_ANYA")] = "7dc2463509b051066d403068c34c0f6aa56bab18aaa1f5a279caa711a4476a7d",
        [new("pret:TRAINER_SIS_AND_BRO_LIA_LUC")] = "22d205e9528a18c3dde905531508e7f3f26c89197998272400030b85243126be",
        [new("pret:TRAINER_SWIMMER_FEMALE_MELISSA")] = "fb5e958f49a9a05ca8c556c16da47400bdef3d46b71c4dc0042f7a432e2beb9e",
        [new("pret:TRAINER_PICNICKER_MISSY")] = "4bff3d53d3aebb3f5be612b168d4755561f0964d897318a6dc6967fdef3cb005",
        [new("pret:TRAINER_SWIMMER_FEMALE_NORA")] = "f9e825d630de58c554eb6c1323274fdd25fcce588f7c24715980045e87249595",
        [new("pret:TRAINER_BIRD_KEEPER_ROGER")] = "5f31ad791d5c8452bb6be6875c72450ce3f1d7c1899437e58c6f24f1661a9047",
        [new("pret:TRAINER_SWIMMER_MALE_DEAN")] = "a5bc75e4c941b5e3dfc9ff21de4ee538e3b065fe19bfc53600b99830859fd8cd",
        [new("pret:TRAINER_PICNICKER_IRENE")] = "f7bd4e9ee017f021120451250304297bd981a2a71382b96d06fd505110a89b1b",
        [new("pret:TRAINER_SWIMMER_MALE_BARRY")] = "52ed79b0f117aef5916734dac2e9e2dd550fac6ab9d537d34ce32a0341509f5b",
        [new("pret:TRAINER_SWIMMER_MALE_DARRIN")] = "6d4ea4b522d90bbe3bde98ba4a0dd4052169358bc0d54c6fb2308aa48cfbfab7",
        [new("pret:TRAINER_SWIMMER_FEMALE_SHIRLEY")] = "9c648b02b5a5c19525e7ba577cc6c21654673793fa503088a118fdafd73a3438",
        [new("pret:TRAINER_SWIMMER_FEMALE_TIFFANY")] = "f1301db03bf1942f66fbe96239b9f91d490f9c33fd3cb1cd84f1c08091b4e380",
    }.ToImmutableDictionary();
    public static readonly ImmutableDictionary<string, string> BattleHashes = new Dictionary<string, string> {
        ["Route19_EventScript_Reece"] = "7229e4feed002d37cf9494b8b7ecf3bc085a557f50a15f13edd1e431ae9dcd42",
        ["Route19_EventScript_Richard"] = "c5a56473971ea0dc94eee0fe9b37451a945dafe09365a5bcbde18f2ff46ccf24",
        ["Route19_EventScript_Tony"] = "5025576a4cd2fe5e23ead2f2d4ecf03a7377d9c63feb5da86c7f02da73b3d93d",
        ["Route19_EventScript_Matthew"] = "a53d3504ba40eb0f150675eddb11fd73bef96ddcc84142c56d2425c1b3cc4acc",
        ["Route19_EventScript_Douglas"] = "2be721bf0542f3949e557d888d761ce2afc12fd8be0c7d30401aaea79e2bb275",
        ["Route19_EventScript_David"] = "1c86cbaaad37fada658c5262f635b1f3948ee284984a58d3664ccc7ff9e03f1e",
        ["Route19_EventScript_Axle"] = "ce575c401d97d52ee14d95100e87a06f867ee09d3a028984fb9b5c40baa3b416",
        ["Route19_EventScript_Alice"] = "3ec3d380b113aafac11427cae816d909d1236d75753dcba95e015145a9075039",
        ["Route19_EventScript_Connie"] = "b897c7815cafa8ddf966c474123716925de57cbe782ed2eedcd523311863e9b4",
        ["Route19_EventScript_Anya"] = "332906c479f5348d37b263d067b9fa302b1fce744081caafed863f901668a903",
        ["Route19_EventScript_Lia"] = "414f143abd8fe722630e6f6a78aa09463c51da5208489e67cd3b220b3c635093",
        ["Route19_EventScript_Luc"] = "8c80d5464dd304ba56676a06adeaa7db0ab2f60cac19708e1ef506e8d9572fb6",
        ["Route20_EventScript_Melissa"] = "faf88498cec8b84bcffd46a60398fd4decf779ef60749f981ceda4f7e615588b",
        ["Route20_EventScript_Missy"] = "c5c4ceac0089a7bb85e214039185a8e77de36ae213ef0e8ef8888ea6ea84dc83",
        ["Route20_EventScript_Nora"] = "9746d34370fd9423fff8fb22c293a6e5db6a8f3357e8fb133089327c571b3ba3",
        ["Route20_EventScript_Roger"] = "f56720412eb1fc2372eb4573e0f7c145365e309fed012a5b641c06bcb25feae5",
        ["Route20_EventScript_Dean"] = "2d5b8fc725ea906dc68098f1fcd1ac4369d4c077e910c860a791e8a1d5da9bec",
        ["Route20_EventScript_Irene"] = "5eb8ef1148a9a0a13f00248cf5a962679be28c2fef0e0b6c7b04dd45765e7231",
        ["Route20_EventScript_Barry"] = "684e8f6f3791da816ac5cbd1e2b87ad7963b131e3cb5cd76da182ef7b7123894",
        ["Route20_EventScript_Darrin"] = "8a0a43fd040bf41ec7f451f02d100f056d661f8c3bb2ad9fbd5ba0626b9e7a26",
        ["Route20_EventScript_Shirley"] = "688b6d6ecd2600c5acc9f7eea04c530ff163f496d88d06fcbe43611d8c4cf745",
        ["Route20_EventScript_Tiffany"] = "fffab44fb2dc109fefd790d919555d2b419b351859e15f7132cbc67d3a654f16",
    }.ToImmutableDictionary(StringComparer.Ordinal);
}
