using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

// 医疗床的拦截
namespace NivarianSleepInFridges
{
    // 在原版给出医院评分后，确认是否存在真正的医疗床。在这里拦截~
    // 待优化
    [HarmonyPatch(typeof(RoomRoleWorker_Hospital), "GetScore")]
    internal static class Patch_RoomRoleWorkerHospital_GetScore
    {
        private static void Postfix(Room room, ref float __result)
        {
            if (__result <= 0f || room == null)
            {
                return;
            }

            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                Building_Bed bed = things[i] as Building_Bed;
                if (bed == null
                    || bed is Building_FridgeBedProxy
                    || bed.def == null
                    || bed.def.building == null
                    || !bed.def.building.bed_humanlike
                    || bed.ForPrisoners
                    || !bed.Medical)
                {
                    continue;
                }

                return;
            }

            __result = 0f;
        }
    }
}
