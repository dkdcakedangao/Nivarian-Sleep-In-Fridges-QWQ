using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

// 除错~这是现在唯一的扫描了，也只在你按了debug页面的那个快速除错时，会运行扫描一次
namespace NivarianSleepInFridges
{
    internal sealed class FridgeRepairReport
    {
        internal int ScannedStorages;
        internal int NewlyProcessedDefs;
        internal int FridgeStorages;
        internal int RepairedInstances;
        internal int Failures;
    }

    internal static class FridgeRepairUtility
    {
        private static readonly FieldInfo CompsField = AccessTools.Field(typeof(ThingWithComps), "comps");
        private static readonly FieldInfo CompsByTypeField = AccessTools.Field(typeof(ThingWithComps), "compsByType");

        internal static FridgeRepairReport RepairCurrentMap()
        {
            FridgeRepairReport report = new FridgeRepairReport();
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return report;
            }

            List<Thing> things = new List<Thing>(map.listerThings.AllThings);
            for (int i = 0; i < things.Count; i++)
            {
                Building_Storage storage = things[i] as Building_Storage;
                if (storage == null)
                {
                    continue;
                }

                report.ScannedStorages++;
                bool newlyProcessed;
                FridgeDefCacheEntry entry = FridgeDefRegistry.EnsureProcessed(
                    storage.def,
                    "quick repair",
                    out newlyProcessed);
                if (newlyProcessed)
                {
                    report.NewlyProcessedDefs++;
                }

                if (!entry.IsFridge)
                {
                    continue;
                }

                report.FridgeStorages++;
                CompFridgeSleep existing = storage.GetComp<CompFridgeSleep>();
                if (existing != null)
                {
                    existing.RefreshBedProxy();
                    continue;
                }

                if (TryAttachComp(storage))
                {
                    report.RepairedInstances++;
                }
                else
                {
                    report.Failures++;
                }
            }

            FridgeDebugLog.Message(
                "Quick repair complete: storages=" + report.ScannedStorages
                + ", newDefs=" + report.NewlyProcessedDefs
                + ", fridges=" + report.FridgeStorages
                + ", repaired=" + report.RepairedInstances
                + ", failures=" + report.Failures + ".");
            return report;
        }

        private static bool TryAttachComp(Building_Storage storage)
        {
            if (storage == null || storage.GetComp<CompFridgeSleep>() != null)
            {
                return true;
            }

            if (CompsField == null || CompsByTypeField == null)
            {
                Log.Error("[Nivarian Sleep In Fridges] Could not access ThingWithComps component fields for quick repair.");
                return false;
            }

            CompProperties_FridgeSleep properties = FindProperties(storage.def);
            if (properties == null)
            {
                Log.Error("[Nivarian Sleep In Fridges] Missing refrigerator sleep properties on " + storage.def.defName + ".");
                return false;
            }

            List<ThingComp> comps = CompsField.GetValue(storage) as List<ThingComp>;
            if (comps == null)
            {
                comps = new List<ThingComp>();
                CompsField.SetValue(storage, comps);
            }

            CompFridgeSleep added = new CompFridgeSleep();
            added.parent = storage;
            comps.Add(added);
            try
            {
                added.Initialize(properties);
                RebuildCompIndex(storage, comps);
                added.PostPostMake();
                if (storage.Spawned)
                {
                    added.PostSpawnSetup(false);
                }

                FridgeDebugLog.Message("Quick repair attached CompFridgeSleep to " + storage.GetUniqueLoadID() + ".");
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    if (storage.Spawned)
                    {
                        added.PostDeSpawn(storage.Map, DestroyMode.Vanish);
                    }
                }
                catch
                {
                }

                comps.Remove(added);
                RebuildCompIndex(storage, comps);
                Log.Error(
                    "[Nivarian Sleep In Fridges] Failed to attach CompFridgeSleep to "
                    + storage.GetUniqueLoadID() + ": " + exception);
                return false;
            }
        }

        private static CompProperties_FridgeSleep FindProperties(ThingDef def)
        {
            if (def == null || def.comps == null)
            {
                return null;
            }

            for (int i = 0; i < def.comps.Count; i++)
            {
                CompProperties_FridgeSleep properties = def.comps[i] as CompProperties_FridgeSleep;
                if (properties != null)
                {
                    return properties;
                }
            }

            return null;
        }

        private static void RebuildCompIndex(ThingWithComps thing, List<ThingComp> comps)
        {
            Dictionary<Type, List<ThingComp>> grouped = new Dictionary<Type, List<ThingComp>>();
            for (int i = 0; i < comps.Count; i++)
            {
                ThingComp comp = comps[i];
                if (comp == null)
                {
                    continue;
                }

                Type type = comp.GetType();
                List<ThingComp> values;
                if (!grouped.TryGetValue(type, out values))
                {
                    values = new List<ThingComp>();
                    grouped.Add(type, values);
                }

                values.Add(comp);
            }

            Dictionary<Type, ThingComp[]> index = new Dictionary<Type, ThingComp[]>();
            foreach (KeyValuePair<Type, List<ThingComp>> pair in grouped)
            {
                index.Add(pair.Key, pair.Value.ToArray());
            }

            CompsByTypeField.SetValue(thing, index);
        }
    }
}
