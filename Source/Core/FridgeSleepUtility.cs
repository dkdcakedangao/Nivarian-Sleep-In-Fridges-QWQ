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

        internal static void RefreshAllMaps()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            List<Map> maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                List<Thing> things = new List<Thing>(maps[mapIndex].listerThings.AllThings);
                for (int thingIndex = 0; thingIndex < things.Count; thingIndex++)
                {
                    ThingWithComps thing = things[thingIndex] as ThingWithComps;
                    if (thing == null)
                    {
                        continue;
                    }

                    CompFridgeSleep comp = thing.GetComp<CompFridgeSleep>();
                    if (comp != null)
                    {
                        comp.RefreshBedProxy();
                    }
                }
            }
        }
    }
}
