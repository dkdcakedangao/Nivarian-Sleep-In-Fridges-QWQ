using HarmonyLib;
using RimWorld;
using Verse;

// 新补丁喵~判断冰箱的新入口，现在是InitializeComps，不再是之前那个可能存在的启动遍历了~
namespace NivarianSleepInFridges
{
    [HarmonyPatch(typeof(Building_Storage), "PostMake")]
    internal static class Patch_BuildingStorage_PostMake
    {
        private static void Prefix(Building_Storage __instance)
        {
            bool ignored;
            FridgeDefRegistry.EnsureProcessed(__instance.def, "PostMake", out ignored);
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "InitializeComps")]
    internal static class Patch_ThingWithComps_InitializeComps
    {
        private static void Prefix(ThingWithComps __instance)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars)
            {
                return;
            }

            Building_Storage storage = __instance as Building_Storage;
            if (storage == null)
            {
                return;
            }

            bool ignored;
            FridgeDefRegistry.EnsureProcessed(storage.def, "LoadingVars", out ignored);
        }
    }
}
