#if !RIMWORLD_17
using Nivarian;
#endif
using RimWorld;
using Verse;

// 冰核恢复相关
// 抄的kp的
// 爽啦！！
namespace NivarianSleepInFridges
{
    internal static class FridgeIcyCoreUtility
    {
        private const float RecoveryPerRareTick = 0.01f * 250f / 150f;
#if RIMWORLD_17
        private static NeedDef icyCoreDef;
        internal static void Initialize()
        {
            icyCoreDef = DefDatabase<NeedDef>.GetNamedSilentFail("Nivarian_IcyCore");
        }
#endif

        internal static void RecoverOccupants(Building_FridgeBedProxy proxy)
        {
            if (proxy == null
                || !proxy.IsActive
                || proxy.ParentFridge == null
                || SleepInFridgesMod.Settings == null
                || !SleepInFridgesMod.Settings.IcyCoreRecoveryEnabled)
            {
                return;
            }

            CompPowerTrader power = proxy.ParentFridge.GetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn)
            {
                return;
            }

            foreach (Pawn pawn in proxy.CurOccupants)
            {
                if (!FridgeSleepUtility.IsEligibleSleeper(pawn) || pawn.CurrentBed() != proxy || pawn.needs == null)
                {
                    continue;
                }

#if RIMWORLD_17
                Need icyCore = icyCoreDef == null ? null : pawn.needs.TryGetNeed(icyCoreDef);
#else
                Need_IcyCore icyCore = pawn.needs.TryGetNeed<Need_IcyCore>();
#endif
                if (icyCore != null)
                {
                    icyCore.CurLevel += RecoveryPerRareTick;
                }
            }
        }
    }
}
