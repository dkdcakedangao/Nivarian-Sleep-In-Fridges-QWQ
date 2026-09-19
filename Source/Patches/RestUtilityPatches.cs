using HarmonyLib;
using RimWorld;
using Verse;

// 睡眠资格补丁 当然是只有 殖民者+涅瓦莲 才能睡冰箱啦~
// 床是否合法也是在这里（虽然我完全不知道有没有用，这部分测试的时候，只有壁挂式冰箱被拦了）
namespace NivarianSleepInFridges
{
    [HarmonyPatch(typeof(RestUtility), "IsValidBedFor")]
    internal static class Patch_RestUtility_IsValidBedFor
    {
        private static bool Prefix(Thing bedThing, Pawn sleeper, ref bool __result)
        {
            Building_FridgeBedProxy proxy = bedThing as Building_FridgeBedProxy;
            if (proxy == null)
            {
                return true;
            }

            if (!proxy.IsActive || !FridgeSleepUtility.IsEligibleSleeper(sleeper))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(RestUtility), "CanUseBedNow")]
    internal static class Patch_RestUtility_CanUseBedNow
    {
        private static bool Prefix(Thing bedThing, Pawn sleeper, ref bool __result)
        {
            Building_FridgeBedProxy proxy = bedThing as Building_FridgeBedProxy;
            if (proxy == null)
            {
                return true;
            }

            if (!proxy.IsActive || !FridgeSleepUtility.IsEligibleSleeper(sleeper))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(RestUtility), "CanUseBedEver")]
    internal static class Patch_RestUtility_CanUseBedEver
    {
        private static bool Prefix(Pawn p, ThingDef bedDef, ref bool __result)
        {
            if (bedDef == null
                || bedDef.thingClass == null
                || !typeof(Building_FridgeBedProxy).IsAssignableFrom(bedDef.thingClass))
            {
                return true;
            }

            if (!FridgeSleepUtility.IsEligibleSleeper(p))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
