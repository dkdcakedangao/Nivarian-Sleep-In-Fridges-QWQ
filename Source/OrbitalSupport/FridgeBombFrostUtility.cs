using System;
using System.Collections.Generic;
using Verse;

namespace NivarianSleepInFridges.OrbitalSupport
{
    // 仅在冰箱弹命中时检查半径 5；冰刺的外观、伤害、减速和寿命由本体负责。
    internal static class FridgeBombFrostUtility
    {
        private const float Radius = 5f;
        private const int MaximumSpikes = 24;
        private static ThingDef spikeDef;

        internal static void Initialize()
        {
            spikeDef = DefDatabase<ThingDef>.GetNamedSilentFail("Nivarian_FrostSpike_CoreStrong");
            if (spikeDef == null || spikeDef.thingClass == null || !typeof(Nivarian.IcySpike).IsAssignableFrom(spikeDef.thingClass)
                || spikeDef.size != IntVec2.One)
                throw new InvalidOperationException("Nivarian_FrostSpike_CoreStrong must be a one-cell IcySpike.");
        }

        private static bool CanSpawnAt(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map) || !cell.Walkable(map)) return false;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def == spikeDef || GenSpawn.SpawningWipes(spikeDef, things[i].def)) return false;
            }
            return true;
        }

        internal static void Spawn(Map map, IntVec3 center)
        {
            List<IntVec3> candidates = new List<IntVec3>();
            int spawned = 0;
            int skipped = 0;
            string reason = "none";
            try
            {
                if (map == null || !center.InBounds(map))
                    throw new InvalidOperationException("Impact map or position is unavailable.");
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Radius, true))
                    if (CanSpawnAt(map, cell)) candidates.Add(cell);

                // 部分 Fisher-Yates：均匀、无放回抽取最多 24 格，不扫描地图、不长期缓存地块。
                int count = Math.Min(MaximumSpikes, candidates.Count);
                for (int i = 0; i < count; i++)
                {
                    int selected = Rand.Range(i, candidates.Count);
                    IntVec3 cell = candidates[selected];
                    candidates[selected] = candidates[i];
                    candidates[i] = cell;
                    // 防止其他模组在前一根冰刺 Spawn 时改变相邻候选格。
                    if (!CanSpawnAt(map, cell)) { skipped++; continue; }
                    Thing spike = ThingMaker.MakeThing(spikeDef);
                    try
                    {
                        Thing result = GenSpawn.Spawn(spike, cell, map);
                        if (result == null || !spike.Spawned)
                            throw new InvalidOperationException("Ice spike did not spawn at " + cell);
                        spawned++;
                    }
                    catch (Exception error)
                    {
                        try { if (!spike.Destroyed) spike.Destroy(); }
                        catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                        throw;
                    }
                }
                if (count == 0) reason = "no walkable, non-wiping cells without existing spikes";
                else if (skipped != 0) reason = "some selected cells changed before spawning";
            }
            catch (Exception error)
            {
                reason = error.GetType().Name + ": " + error.Message;
                throw;
            }
            finally
            {
                if (OrbitalSupportHost.DebugEnabled)
                    OrbitalSupportHost.Trace("[冰刺生成] map=" + (map == null ? "null" : map.uniqueID.ToString())
                        + " cell=" + center + " radius=" + Radius + " candidates=" + candidates.Count
                        + " spawned=" + spawned + " skipped=" + skipped + " reason=" + reason);
            }
        }
    }
}
