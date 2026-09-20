using System.Collections.Generic;
using RimWorld;
using Verse;

// 刷新入口
// 除错入口
namespace NivarianSleepInFridges
{
    internal static class FridgeSleepUtility
    {
        internal const string NivarianRaceDefName = "NivarianRace_Pawn";
        internal const string UfGarbageBinDefName = "UFLI_Garbagebin_AS";

        internal static bool Enabled
        {
            get { return SleepInFridgesMod.Settings == null || SleepInFridgesMod.Settings.Enabled; }
        }

        internal static bool IsEligibleSleeper(Pawn pawn)
        {
            return pawn != null
                && pawn.IsColonist
                && pawn.def != null
                && pawn.def.defName == NivarianRaceDefName;
        }

        internal static bool IsFridgeAllowed(ThingDef def)
        {
            if (def == null || def.defName != UfGarbageBinDefName)
            {
                return true;
            }

            SleepInFridgesSettings settings = SleepInFridgesMod.Settings;
            return settings == null || settings.AllowUfGarbageBin;
        }

        internal static void RefreshAllMaps()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            List<ThingDef> fridgeDefs = FridgeDefRegistry.FridgeDefsSnapshot();
            List<Map> maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                for (int defIndex = 0; defIndex < fridgeDefs.Count; defIndex++)
                {
                    List<Thing> things = map.listerThings.ThingsOfDef(fridgeDefs[defIndex]);
                    for (int thingIndex = 0; thingIndex < things.Count; thingIndex++)
                    {
                        ThingWithComps thing = things[thingIndex] as ThingWithComps;
                        CompFridgeSleep comp = thing == null ? null : thing.GetComp<CompFridgeSleep>();
                        if (comp != null)
                        {
                            comp.RefreshBedProxy();
                        }
                    }
                }
            }
        }
    }
}
