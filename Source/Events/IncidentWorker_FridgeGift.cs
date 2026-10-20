using RimWorld;
using Verse;

// 事件核心，也是执行的入口
namespace NivarianSleepInFridges
{
    public sealed class IncidentWorker_FridgeGift : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return FridgeGiftUtility.RefrigeratorDef != null
                && FridgeGiftUtility.HasColonist(parms.target as Map)
                && !Current.Game.GetComponent<FridgeGiftComponent>().HasPendingDrop;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            if (FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[FridgeGift][执行请求] currentTick=" + Find.TickManager.TicksGame
                    + " map=" + (map == null ? "null" : map.uniqueID.ToString()));
            if (!CanFireNowSub(parms))
            {
                if (FridgeDebugLog.Enabled)
                {
                    string reason = FridgeGiftUtility.RefrigeratorDef == null
                        ? FridgeGiftUtility.MissingRefrigeratorReason
                        : map == null || !map.IsPlayerHome ? "目标不是玩家殖民地"
                        : !FridgeGiftUtility.HasColonist(map) ? "目标地图没有合格涅瓦莲"
                        : "已有冰箱正在下降";
                    FridgeDebugLog.Message("[FridgeGift][执行结果] stage=launch success=false reason=" + reason);
                }
                return false;
            }
            ThingDef fridgeDef = FridgeGiftUtility.RefrigeratorDef;
            ThingDef dropDef = DefDatabase<ThingDef>.GetNamedSilentFail("NSIF_FridgeGiftIncoming");
            IntVec3 cell;
            if (dropDef == null)
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] stage=launch success=false reason=缺少 NSIF_FridgeGiftIncoming 定义。");
                return false;
            }
            if (!FridgeGiftUtility.TryFindCell(map, fridgeDef, out cell))
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] stage=launch success=false map="
                        + map.uniqueID + " reason=没有安全落点。");
                return false;
            }

            Thing fridge = ThingMaker.MakeThing(fridgeDef);
            fridge.SetFaction(Faction.OfPlayer);
            fridge.HitPoints = fridge.MaxHitPoints;
            Skyfaller_FridgeGift drop = (Skyfaller_FridgeGift)SkyfallerMaker.MakeSkyfaller(dropDef, fridge);
            Thing spawned = GenSpawn.Spawn(drop, cell, map);
            FridgeGiftComponent component = Current.Game.GetComponent<FridgeGiftComponent>();
            if (spawned == null || !drop.Spawned)
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] stage=launch success=false cell=" + cell
                        + " reason=" + (spawned == null ? "GenSpawn 返回 null" : "待降实例未实际生成"));
                if (!drop.Destroyed) drop.Destroy();
                component.NotifyFinished(drop);
                return false;
            }
            component.NotifyLaunched(drop);
            if (FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[FridgeGift][执行结果] stage=launch success=true map="
                    + map.uniqueID + " cell=" + cell + "；已开始下降，尚未完成落地。");
            return true;
        }
    }
}
