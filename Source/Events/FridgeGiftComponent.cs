using RimWorld;
using Verse;

// 状态管理中心，开局、读档时的种族检查，debug基本上都在这里了喵~
namespace NivarianSleepInFridges
{
    public sealed class FridgeGiftComponent : GameComponent
    {
        private int scheduledTick = -1;
        private bool completed;
        private Skyfaller_FridgeGift pendingDrop;

        // 仅运行时状态
        private bool initialized;
        private bool checkRequested;
        private bool waitingForColonist;
        private Thing_FridgeGiftTimer timer;

        public FridgeGiftComponent(Game game)
        {
            Patch_MapPawns_RegisterPawn.TrackingEnabled = false;
        }

        public override void StartedNewGame()
        {
            RequestInitialCheck("StartedNewGame");
        }

        public override void LoadedGame()
        {
            RequestInitialCheck("LoadedGame");
        }

        private void RequestInitialCheck(string source)
        {
            initialized = true;
            checkRequested = true;
            waitingForColonist = false;
            Patch_MapPawns_RegisterPawn.TrackingEnabled = !completed && !HasPendingDrop;
            if (FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[FridgeGift][初始化] source=" + source + " currentTick="
                    + Find.TickManager.TicksGame + " scheduledTick=" + scheduledTick
                    + " completed=" + completed + " pendingDrop=" + HasPendingDrop);
            if (!completed && !HasPendingDrop) EnsureTimer();
        }

        internal void NotifyPawnRegistered(Pawn pawn)
        {
            // debug~~
            if (FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[FridgeGift][人物登记] pawn="
                    + pawn.GetUniqueLoadID() + " race=" + pawn.def.defName
                    + " eligible=" + FridgeGiftUtility.IsEligibleColonist(pawn)
                    + " spawned=" + pawn.Spawned + " freeColonist=" + pawn.IsFreeColonist
                    + " playerFaction=" + (pawn.Faction == Faction.OfPlayer)
                    + " initialized=" + initialized + " completed=" + completed
                    + " pendingDrop=" + (pendingDrop != null) + " checkRequested=" + checkRequested
                    + " waitingForColonist=" + waitingForColonist + " scheduledTick=" + scheduledTick);
            if (!initialized || completed || pendingDrop != null || checkRequested) return;
            if (scheduledTick >= 0 && !waitingForColonist) return;
            if (FridgeGiftUtility.IsEligibleColonist(pawn))
            {
                checkRequested = true;
                EnsureTimer();
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][请求检查] currentTick=" + Find.TickManager.TicksGame
                        + " scheduledTick=" + scheduledTick + "；保留原计时，下一次 Rare 调度重检。");
            }
        }

        internal bool HasPendingDrop
        {
            get { return pendingDrop != null && pendingDrop.Spawned; }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref scheduledTick, "NSIF_fridgeGiftTick", -1);
            Scribe_Values.Look(ref completed, "NSIF_fridgeGiftCompleted", false);
            Scribe_References.Look(ref pendingDrop, "NSIF_fridgeGiftPending");
        }

        private void EnsureTimer()
        {
            if (timer != null) return;
            timer = (Thing_FridgeGiftTimer)ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("NSIF_FridgeGiftTimer"));
            timer.Initialize(this);
            Find.TickManager.RegisterAllTickabilityFor(timer);
        }

        private void StopTimer()
        {
            if (timer == null) return;
            Find.TickManager.DeRegisterAllTickabilityFor(timer);
            timer.Detach();
            timer = null;
        }

        // 原版 Rare 分桶真正每 250 Tick
        internal void ProcessTimerTick(Thing_FridgeGiftTimer sender)
        {
            if (sender != timer) return;
            if (!initialized || completed || HasPendingDrop)
            {
                StopTimer();
                return;
            }
            if (pendingDrop != null)
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] success=false reason=待降实例已不在地图，结束自动事件。");
                NotifyFinished(pendingDrop);
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (!checkRequested && now < scheduledTick) return;
            if (FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[FridgeGift][检查] currentTick=" + now + " scheduledTick="
                    + scheduledTick + " checkRequested=" + checkRequested);
            checkRequested = false;
            if (FridgeGiftUtility.RefrigeratorDef == null)
            {
                Patch_MapPawns_RegisterPawn.TrackingEnabled = false;
                waitingForColonist = true;
                StopTimer();
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] success=false reason=缺少 Draconiture 前置或 NivarianRefrigerator 定义；休眠等待，不开始新计时。");
                return;
            }

            bool foundColonist = false;
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (!FridgeGiftUtility.HasColonist(map)) continue;
                foundColonist = true;
                if (scheduledTick < 0)
                {
                    scheduledTick = now + 60000;
                    waitingForColonist = false;
                    if (FridgeDebugLog.Enabled)
                        FridgeDebugLog.Message("[FridgeGift][计时开始] map=" + map.uniqueID
                            + " currentTick=" + now + " scheduledTick=" + scheduledTick + " delayTicks=60000");
                    return;
                }
                if (now < scheduledTick)
                {
                    waitingForColonist = false;
                    if (FridgeDebugLog.Enabled)
                        FridgeDebugLog.Message("[FridgeGift][计时恢复] currentTick=" + now
                            + " scheduledTick=" + scheduledTick + "；尚未到期，不重置计时。");
                    return;
                }
                IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("NSIF_FridgeGift");
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][自动执行] map=" + map.uniqueID + " currentTick="
                        + now + " scheduledTick=" + scheduledTick + "；原计时已到期。");
                if (incident == null && FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] success=false reason=缺少 NSIF_FridgeGift 事件定义。");
                if (incident != null && incident.Worker.TryExecute(new IncidentParms { target = map })) return;
                if (completed) return;
            }
            if (foundColonist)
            {
                completed = true;
                Patch_MapPawns_RegisterPawn.TrackingEnabled = false;
                StopTimer();
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][自动结束] success=false reason=所有合格地图执行失败，自动事件永久结束。");
            }
            else
            {
                waitingForColonist = scheduledTick < 0 || now >= scheduledTick;
                if (waitingForColonist) StopTimer();
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][无合格人物] currentTick=" + now
                        + " scheduledTick=" + scheduledTick + " waitingForColonist=" + waitingForColonist
                        + "；不开始新计时，保留已有截止时间。");
            }
        }

        internal void NotifyLaunched(Skyfaller_FridgeGift drop)
        {
            pendingDrop = drop;
            Patch_MapPawns_RegisterPawn.TrackingEnabled = false;
            StopTimer();
        }

        internal void NotifyFinished(Skyfaller_FridgeGift drop)
        {
            if (pendingDrop == drop) pendingDrop = null;
            completed = true;
            Patch_MapPawns_RegisterPawn.TrackingEnabled = false;
            StopTimer();
        }
    }
}
