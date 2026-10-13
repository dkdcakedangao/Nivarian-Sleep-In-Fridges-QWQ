using HarmonyLib;
using RimWorld;
using Verse;

// 通知入口
namespace NivarianSleepInFridges
{
    [HarmonyPatch(typeof(MapPawns), "RegisterPawn")]
    internal static class Patch_MapPawns_RegisterPawn
    {
        // 仅运行时开关；每个存档初始化时恢复
        internal static bool TrackingEnabled;

        private static void Postfix(Pawn p)
        {
            if (!TrackingEnabled) return;
            if (p == null || p.Dead || !p.Spawned
                || p.Faction != Faction.OfPlayer || !p.IsFreeColonist) return;
            Game game = Current.Game;
            if (game == null) return;
            FridgeGiftComponent component = game.GetComponent<FridgeGiftComponent>();
            if (component != null) component.NotifyPawnRegistered(p);
        }
    }
}
