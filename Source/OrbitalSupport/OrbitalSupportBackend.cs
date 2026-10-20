using System;
using System.Collections.Generic;
using HarmonyLib;
using LumiParticle;
using Nivarian.ShipSupports;
using Nivarian_Race.Code.Defs;
using Nivarian_Race.Code.NivarianThing;
using Verse;

namespace NivarianSleepInFridges.OrbitalSupport
{
    public sealed class OrbitalSupportBackend : IOrbitalSupportBackend
    {
        private readonly Harmony patches = new Harmony("dkdcakedangao.NivarianSleepInFridgesQWQ.OrbitalSupport");
        private ThingDef shellDef;
        private NivarianMotherShipRequestDef requestDef;
        private Type originalShellClass;
        private Type originalWorkerClass;
        private ShipSupportWorkerBase originalWorker;
        private AccessTools.FieldRef<NivarianMotherShipRequestDef, ShipSupportWorkerBase> workerCache;
        private bool bindingsApplied;
        private bool disposed;

        public static IOrbitalSupportBackend Create()
        {
            var backend = new OrbitalSupportBackend();
            try
            {
                LumiStartup.EnsureInitialized();
                backend.ValidateAndBind();
                backend.patches.PatchAll(typeof(OrbitalSupportBackend).Assembly);
                return backend;
            }
            catch (Exception error)
            {
                try { backend.Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }

        private void ValidateAndBind()
        {
            Skyfaller_FridgeBomb.ValidateShellAccess();
            SupportWorker_FridgeCreepingBombardment.ValidateTargetingAccess();
            FridgeBombVisuals.ValidateAccess();
            FridgeBombFrostUtility.Initialize();
            if (Patch_FridgeBomb_SpawnAnimator.TargetMethod() == null)
                throw new MissingMethodException("LumiParticleEffects.SpawnAnimator(Map, data, lifetime, Thing)");
            if (AccessTools.Method(typeof(LumiParticle.Core.LumiVisualItem), "Recycle", Type.EmptyTypes) == null)
                throw new MissingMethodException("LumiVisualItem.Recycle()");
            workerCache = AccessTools.FieldRefAccess<NivarianMotherShipRequestDef, ShipSupportWorkerBase>("workerInt");
            shellDef = DefDatabase<ThingDef>.GetNamed("NSIF_FridgeBombIncoming");
            requestDef = DefDatabase<NivarianMotherShipRequestDef>.GetNamed("NSIF_Request_FridgeCreepingBombardment");
            if (shellDef.thingClass == null || !typeof(NivarianSkyfaller).IsAssignableFrom(shellDef.thingClass)
                || requestDef.bombardmentPattern == null)
                throw new InvalidOperationException("冰箱火力网基础 Def 配置不完整。");
            originalShellClass = shellDef.thingClass;
            originalWorkerClass = requestDef.workerClass;
            originalWorker = workerCache(requestDef);
            bindingsApplied = true;
            shellDef.thingClass = typeof(Skyfaller_FridgeBomb);
            requestDef.workerClass = typeof(SupportWorker_FridgeCreepingBombardment);
            workerCache(requestDef) = new SupportWorker_FridgeCreepingBombardment();
        }

        public void ClearSession()
        {
            List<Exception> errors = new List<Exception>();
            try { SupportWorker_FridgeCreepingBombardment.ClearAllPreviews(); }
            catch (Exception error) { errors.Add(error); }
            try { FridgeBombVisuals.Clear(); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("战备会话清理未完整完成。", errors);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            List<Exception> errors = new List<Exception>();
            try { ClearSession(); }
            catch (Exception error) { errors.Add(error); }
            try { FridgeBombVisuals.ReleaseResources(); }
            catch (Exception error) { errors.Add(error); }
            try { patches.UnpatchAll(patches.Id); }
            catch (Exception error) { errors.Add(error); }
            if (bindingsApplied)
            {
                // 即使已有外部持有 Worker，CanUseNow 仍通过 Host 的禁用状态拒绝新请求。
                try { shellDef.thingClass = originalShellClass; }
                catch (Exception error) { errors.Add(error); }
                try
                {
                    requestDef.workerClass = originalWorkerClass;
                    workerCache(requestDef) = originalWorker;
                }
                catch (Exception error) { errors.Add(error); }
                bindingsApplied = false;
            }
            if (errors.Count != 0) throw new AggregateException("战备后端清理未完整完成。", errors);
        }
    }
}
