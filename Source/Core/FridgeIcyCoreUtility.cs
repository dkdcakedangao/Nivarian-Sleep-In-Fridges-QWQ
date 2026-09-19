using Nivarian;
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

                Need_IcyCore icyCore = pawn.needs.TryGetNeed<Need_IcyCore>();
                if (icyCore != null)
                {
                    icyCore.CurLevel += RecoveryPerRareTick;
                }
            }
        }
    }
}
