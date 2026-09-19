using HarmonyLib;
using Verse;

// 除错 保留床和冰箱在同一格的
// 待优化
namespace NivarianSleepInFridges
{
    [HarmonyPatch(typeof(GenSpawn), "SpawningWipes")]
    internal static class Patch_GenSpawn_SpawningWipes
    {
        private static void Postfix(BuildableDef newEntDef, ref bool __result)
        {
            ThingDef thingDef = newEntDef as ThingDef;
            if (thingDef != null
                && thingDef.thingClass != null
                && typeof(Building_FridgeBedProxy).IsAssignableFrom(thingDef.thingClass))
            {
                __result = false;
            }
        }
    }
}
