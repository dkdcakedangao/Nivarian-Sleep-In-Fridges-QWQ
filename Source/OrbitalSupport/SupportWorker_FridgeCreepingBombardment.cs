using System;
using System.Collections.Generic;
using HarmonyLib;
using LumiParticle.Core;
using LumiParticle.Data;
using LumiParticle.Effects;
using LumiParticle.Items;
using Nivarian;
using Nivarian.Helper;
using Nivarian.ShipSupports;
using Nivarian.ThingTask;
using Nivarian_Race.Code.Comps.ThingComps;
using Nivarian_Race.Code.Defs;
using Nivarian_Race.Code.Effects;
using Nivarian_Race.Code.NivarianThing.Base;
using RimWorld;
using UnityEngine;
using Verse;

namespace NivarianSleepInFridges.OrbitalSupport
{
    public sealed class SupportWorker_FridgeCreepingBombardment : ShipSupportWorkerDoubleCellSelectorBase
    {
        private static readonly HashSet<SupportWorker_FridgeCreepingBombardment> Selections = new();
        private static readonly AccessTools.FieldRef<Targeter, Action<LocalTargetInfo>> TargetAction =
            AccessTools.FieldRefAccess<Targeter, Action<LocalTargetInfo>>("action");
        private LumiVisualHandle<LumiParticleItem> preview;
        private Targeter ownedTargeter;
        private Action<LocalTargetInfo> selectionAction;

        internal static void ValidateTargetingAccess()
        {
            if (TargetAction == null) throw new MissingFieldException("Targeter.action");
        }

        internal static void ClearAllPreviews()
        {
            List<Exception> errors = new();
            foreach (var worker in new List<SupportWorker_FridgeCreepingBombardment>(Selections))
            {
                try
                {
                    // 只取消仍属于自己的 Targeter 回调，不能结束别人的瞄准。
                    if (worker.ownedTargeter != null && TargetAction(worker.ownedTargeter) == worker.selectionAction)
                        worker.ownedTargeter.StopTargeting();
                }
                catch (Exception error) { errors.Add(error); }
                try { worker.StopTargeting(); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException("冰箱战备预览清理失败。", errors);
        }

        public override void DoEffect(Map map, NivarianMotherShipRequestDef def)
        {
            if (!OrbitalSupportHost.IsAvailable) return;
            try
            {
                ClearAllPreviews();
                StopTargeting();
                OrbitalSupportHost.Trace("开始两阶段选点。");
                BeginSelection(map, def, false);
            }
            catch (Exception error) { OrbitalSupportHost.Disable(error); }
        }

        public override AcceptanceReport CanUseNow(Map map, NivarianMotherShipRequestDef def)
        {
            if (!OrbitalSupportHost.IsAvailable || map == null || def.bombardmentPattern == null)
                return new AcceptanceReport("NSIF_FridgeBombUnavailable".Translate());
            return AcceptanceReport.WasAccepted;
        }

        protected override void StartSelectCell2(Map map, NivarianMotherShipRequestDef def)
        {
            BeginSelection(map, def, true);
        }

        private void BeginSelection(Map map, NivarianMotherShipRequestDef def, bool second)
        {
            // 不调用基类的第二次选点：它会无条件 Used()，失败也可能扣费。
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = true,
                canTargetBuildings = true
            };
            ownedTargeter = Find.Targeter;
            selectionAction = target =>
            {
                if (!OrbitalSupportHost.IsAvailable) return;
                try
                {
                    if (second)
                    {
                        OnSelectCell2(target.Cell, map, def);
                        OrbitalSupportHost.Trace("方向点=" + cell2);
                        SelectComplete(map, def);
                    }
                    else
                    {
                        OnSelectCell1(target.Cell, map, def);
                        OrbitalSupportHost.Trace("起点=" + cell1);
                        StartSelectCell2(map, def);
                    }
                }
                catch (Exception error) { OrbitalSupportHost.Disable(error); }
            };
            Selections.Add(this);
            ownedTargeter.BeginTargeting(parameters, selectionAction, null,
                target => OrbitalSupportHost.IsAvailable && target.Cell.InBounds(map)
                    && (!second || (cell1.InBounds(map) && target.Cell != cell1)),
                null, StopTargeting, null, true,
                target => OnGUIDraw(def), target => OnWorldDraw(UI.MouseCell(), map, def));
        }

        protected override void SelectComplete(Map map, NivarianMotherShipRequestDef def)
        {
            if (!OrbitalSupportHost.IsAvailable) return;
            ClearPreviewEffects();
            if (!cell1.InBounds(map) || !cell2.InBounds(map) || cell1 == cell2) return;
            GameComp_NivarianMotherShip mothership = Current.Game.GetComponent<GameComp_NivarianMotherShip>();
            if (!mothership.CanUse(def, map)) return;
            Thing marker = null;
            bool submitted = false;
            try
            {
                float angle = NivarianMathHelper.GetAngleBetweenVectors(cell1.ToVector3Shifted(), cell2.ToVector3Shifted());
                marker = GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Nivarian_VisualFlare"), cell1, map);
                if (!(marker is ProgrammableThing programmable))
                    throw new InvalidOperationException("Nivarian_VisualFlare is not a ProgrammableThing.");
                NivarianTechRingUtility.TryEnableOnFlare(marker, def);
                programmable.PushTask(new WaitTask("NSIF_wait-fridge-bombardment", 90));
                programmable.PushTask(new StartBombardmentTask(def.bombardmentPattern, marker, cell1, angle));
                submitted = true;
                // 提交成功后只结算一次；异常时也不重新提交或重复扣费。
                Used(def, map);
                OrbitalSupportHost.Trace("轰炸任务已提交，费用与冷却结算完成。");
            }
            catch (Exception exception)
            {
                if (!submitted && marker != null && !marker.Destroyed)
                {
                    try { marker.Destroy(); }
                    catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
                }
                OrbitalSupportHost.Disable(exception);
                Messages.Message("NSIF_FridgeBombUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
        }

        protected override void OnWorldDraw(IntVec3 mouseCell, Map map, NivarianMotherShipRequestDef def)
        {
            if (!OrbitalSupportHost.IsAvailable) return;
            try { DrawPreview(mouseCell, map, def); }
            catch (Exception error) { OrbitalSupportHost.Disable(error); }
        }

        private void DrawPreview(IntVec3 mouseCell, Map map, NivarianMotherShipRequestDef def)
        {
            IntVec3 center = cell1.IsValid ? cell1 : mouseCell;
            if (!center.InBounds(map)) return;
            NivarianTechRingStyle.DrawFor(def, center.ToVector3Shifted());
            if (!cell1.IsValid || !mouseCell.InBounds(map) || mouseCell == cell1)
            {
                ClearPreviewEffects();
                return;
            }
            float angle = NivarianMathHelper.GetAngleBetweenVectors(cell1.ToVector3Shifted(), mouseCell.ToVector3Shifted());
            if (preview == null || !preview.IsAlive)
            {
                preview?.Dispose();
                preview = LumiParticleEffects.SpawnParticle(map, new LumiParticleData
                {
                    prefabName = "particle_arrow_nonLoop",
                    position = cell1.ToVector3Shifted(),
                    size = 1f,
                    rotation = Quaternion.Euler(0f, angle, 0f),
                    altitudeLayer = AltitudeLayer.MetaOverlays
                }, LumiParticleEffects.PreviewRenewable(0.5f, "NSIF.FridgeBombardment.Preview"));
            }
            else
            {
                preview.Item?.UpdateRotation(Quaternion.Euler(0f, angle, 0f));
            }
            preview?.Renew();
        }

        protected override void ClearPreviewEffects()
        {
            var previous = preview;
            preview = null;
            previous?.Dispose();
        }

        protected override void StopTargeting()
        {
            bool wasSelecting = Selections.Remove(this);
            cell1 = IntVec3.Invalid;
            cell2 = IntVec3.Invalid;
            ownedTargeter = null;
            selectionAction = null;
            try { ClearPreviewEffects(); }
            catch (Exception error)
            {
                if (!OrbitalSupportHost.IsAvailable) throw;
                OrbitalSupportHost.Disable(error);
            }
            if (wasSelecting) OrbitalSupportHost.Trace("选点完成或取消，已释放预览。");
        }
    }
}
