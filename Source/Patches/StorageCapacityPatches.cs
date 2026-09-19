using HarmonyLib;
using RimWorld;
using Verse;

// 原版容量补丁 拦截和转发
namespace NivarianSleepInFridges
{
    [HarmonyPatch(typeof(GridsUtility), "GetMaxItemsAllowedInCell")]
    internal static class Patch_GridsUtility_GetMaxItemsAllowedInCell
    {
        private static void Postfix(IntVec3 c, Map map, ref int __result)
        {
            Building_Storage storage = c.GetEdifice(map) as Building_Storage;
            if (storage == null)
            {
                return;
            }

            CompFridgeSleep comp = storage.GetComp<CompFridgeSleep>();
            __result = FridgeCapacityUtility.VanillaCellCapacity(storage, comp, c, map, __result);
        }
    }

    [HarmonyPatch(typeof(Building_Storage), "SpaceRemainingFor")]
    internal static class Patch_BuildingStorage_SpaceRemainingFor
    {
        private static void Postfix(Building_Storage __instance, ref int __result)
        {
            CompFridgeSleep comp = __instance.GetComp<CompFridgeSleep>();
            if (comp == null || !comp.Active)
            {
                return;
            }

            SlotGroup slotGroup = __instance.GetSlotGroup();
            int heldThings = slotGroup == null ? 0 : slotGroup.HeldThingsCount;
            __result = System.Math.Max(
                0,
                FridgeCapacityUtility.EffectiveCapacity(__instance, comp) - heldThings);
        }
    }
}
