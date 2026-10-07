using HarmonyLib;
using RimWorld;
using Verse;

// 舒适度补丁……其实是因为舒适度读的是单格建筑，所以是读的“冰箱”的舒适度，而不是“冰箱床”的。这里做一下逻辑检查，应该能很好修复。
// 其实直接补个xml，每次原版更新舒适度的时候，额外加一下应该也行？
namespace NivarianSleepInFridges
{
    [HarmonyPatch(typeof(PawnUtility), "GainComfortFromCellIfPossible")]
    internal static class Patch_PawnUtility_GainComfortFromCellIfPossible
    {
        private static bool Prefix(Pawn p, int delta, bool chairsOnly)
        {
            // 沿用原版频率，只有到舒适度更新时间才检查当前床
            if (chairsOnly || !p.Spawned || !p.IsHashIntervalTick(15, delta))
            {
                return true;
            }

            Building_FridgeBedProxy proxy = p.CurrentBed() as Building_FridgeBedProxy;
            if (proxy == null || !proxy.IsActive)
            {
                return true;
            }

            // 直接使用床属性
            PawnUtility.GainComfortFromThingIfPossible(p, proxy, delta);
            return false;
        }
    }
}
