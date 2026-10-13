using RimWorld;
using Verse;

// 判断、查询的工具，检查前置
namespace NivarianSleepInFridges
{
    internal static class FridgeGiftUtility
    {
        internal static ThingDef RefrigeratorDef
        {
            get
            {
                return ModsConfig.IsActive("keeptpa.NivarianDraconiture")
                    ? DefDatabase<ThingDef>.GetNamedSilentFail("NivarianRefrigerator") : null;
            }
        }

        internal static bool IsEligibleColonist(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.Spawned
                && pawn.Faction == Faction.OfPlayer && pawn.IsFreeColonist
                && pawn.def.defName == "NivarianRace_Pawn" && pawn.Map.IsPlayerHome;
        }

        internal static bool HasColonist(Map map)
        {
            if (map == null || !map.IsPlayerHome) return false;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (IsEligibleColonist(pawn)) return true;
            }
            return false;
        }

        internal static bool IsSafeCell(Map map, IntVec3 center, ThingDef fridgeDef, Thing ignore = null)
        {
            if (map == null || fridgeDef == null || !center.InBounds(map)) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(center, Rot4.North, fridgeDef.size))
            {
                if (!cell.InBounds(map) || cell.Fogged(map) || cell.Roofed(map)) return false;
                var things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] != ignore) return false;
                }
            }
            return GenConstruct.CanPlaceBlueprintAt(fridgeDef, center, Rot4.North, map,
                thingToIgnore: ignore).Accepted;
        }

        internal static bool TryFindCell(Map map, ThingDef fridgeDef, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var home = map.areaManager.Home;
            var indices = map.cellIndices;
            int total = indices.NumGridCells;
            bool debug = FridgeDebugLog.Enabled;
            if (total == 0)
            {
                if (debug) FridgeDebugLog.Message("[FridgeGift][选点结果] success=false reason=地图无地块 map=" + map.uniqueID);
                return false;
            }
            int remainingHome = home.TrueCount;
            int index = Rand.Range(0, total);
            if (debug) FridgeDebugLog.Message("[FridgeGift][选点开始] map=" + map.uniqueID + " total="
                + total + " homeCount=" + remainingHome + " startIndex=" + index);

            // 随机起点、环形遍历，待优化
            for (int visited = 0; visited < total; visited++)
            {
                IntVec3 candidate = indices.IndexToCell(index);
                if (++index == total) index = 0;
                bool isHome = home[candidate];
                if (isHome) remainingHome--;

                // 暂存一个合格外部落点，之后只对居住区执行安全检查
                // 石山代码的核心
                if ((isHome || !cell.IsValid) && IsSafeCell(map, candidate, fridgeDef))
                {
                    if (debug)
                        FridgeDebugLog.Message(isHome
                            ? "[FridgeGift][选点结果] success=true area=居住区 cell=" + candidate
                                + " backup=" + cell + " visited=" + (visited + 1) + " map=" + map.uniqueID
                            : "[FridgeGift][备用落点缓存] cell=" + candidate + " visited="
                                + (visited + 1) + " map=" + map.uniqueID + "；仅为非居住区候选，尚未确定降落。");
                    cell = candidate;
                    if (isHome) return true;
                }
                if (remainingHome == 0 && cell.IsValid)
                {
                    if (debug) FridgeDebugLog.Message("[FridgeGift][选点结果] success=true area=非居住区 cell="
                        + cell + " visited=" + (visited + 1) + " map=" + map.uniqueID + " reason=居住区已检查完毕，使用备用缓存。");
                    return true;
                }
            }
            if (debug) FridgeDebugLog.Message("[FridgeGift][选点结果] success=" + cell.IsValid
                + " cell=" + cell + " visited=" + total + " map=" + map.uniqueID
                + (cell.IsValid ? " reason=使用非居住区备用缓存。" : " reason=无安全可建造落点，备用缓存为空。"));
            return cell.IsValid;
        }
    }
}
