using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

// 主体
namespace NivarianSleepInFridges
{
    public sealed class CompProperties_FridgeSleep : CompProperties
    {
        public CompProperties_FridgeSleep()
        {
            compClass = typeof(CompFridgeSleep);
        }
    }

    public sealed class CompFridgeSleep : ThingComp
    {
        private bool allowSleeping;
        private bool medical;
        private Building_FridgeBedProxy bedProxy;
        private int lastInteractionTick = -1;
        private readonly HashSet<int> interactorIdsThisTick = new HashSet<int>();

        internal bool Active
        {
            get
            {
                return allowSleeping
                    && FridgeSleepUtility.Enabled
                    && parent.Spawned
                    && parent.Faction == Faction.OfPlayer;
            }
        }

        internal int SleepingCapacity
        {
            get { return parent.Spawned ? FridgeBedLayout.For(parent).Capacity : 0; }
        }

        internal Building_FridgeBedProxy BedProxy
        {
            get { return bedProxy; }
        }

        internal bool TryRegisterInteraction(Pawn pawn)
        {
            int tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            int pawnId = pawn == null ? -1 : pawn.thingIDNumber;
            if (lastInteractionTick != tick)
            {
                lastInteractionTick = tick;
                interactorIdsThisTick.Clear();
            }

            return interactorIdsThisTick.Add(pawnId);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref allowSleeping, "allowSleeping", false);
            Scribe_Values.Look(ref medical, "medical", false);
            Scribe_References.Look(ref bedProxy, "bedProxy");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
            {
                LongEventHandler.ExecuteWhenFinished(RefreshBedProxy);
            }
            else
            {
                RefreshBedProxy();
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            FridgeCapacityUtility.Refresh(parent);
            RemoveBedProxy();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            RemoveBedProxy();
        }

        // 图标
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            Command_Toggle command = new Command_Toggle();
            command.defaultLabel = "NSIF_AllowSleeping".Translate();
            command.defaultDesc = "NSIF_AllowSleepingDesc".Translate();
            command.icon = ContentFinder<Texture2D>.Get(allowSleeping ? "UI/CanSleep" : "UI/CantSleep");
            command.isActive = delegate { return allowSleeping; };
            command.toggleAction = delegate
            {
                allowSleeping = !allowSleeping;
                RefreshBedProxy();
            };

            if (!FridgeSleepUtility.Enabled)
            {
                command.Disable("NSIF_MasterSwitchDisabled".Translate());
            }

            yield return command;

            if (!allowSleeping)
            {
                yield break;
            }

            Command_Toggle medicalCommand = new Command_Toggle();
            medicalCommand.defaultLabel = "CommandBedSetAsMedicalLabel".Translate();
            medicalCommand.defaultDesc = "CommandBedSetAsMedicalDesc".Translate();
            medicalCommand.icon = ContentFinder<Texture2D>.Get("UI/Commands/AsMedical");
            medicalCommand.isActive = delegate { return medical; };
            medicalCommand.toggleAction = delegate
            {
                medical = !medical;
                ApplyMedicalState();
            };

            if (!FridgeSleepUtility.Enabled)
            {
                medicalCommand.Disable("NSIF_MasterSwitchDisabled".Translate());
            }

            yield return medicalCommand;

            if (bedProxy != null && bedProxy.IsActive)
            {
                CompAssignableToPawn_FridgeBed assignable = bedProxy.GetComp<CompAssignableToPawn_FridgeBed>();
                if (assignable != null)
                {
                    foreach (Gizmo gizmo in assignable.CompGetGizmosExtra())
                    {
                        yield return gizmo;
                    }
                }
            }
        }

        // 刷新 除错
        // 石山代码的遮羞布
        
        internal void RefreshBedProxy()
        {
            FridgeCapacityUtility.Refresh(parent);
            if (!parent.Spawned)
            {
                RemoveBedProxy();
                return;
            }

            FridgeBedLayout desired = FridgeBedLayout.For(parent);
            CollectExistingProxy(desired);

            if (!Active)
            {
                RemoveBedProxy();
                return;
            }

            if (bedProxy != null && desired.Matches(bedProxy))
            {
                ApplyMedicalState();
                return;
            }

            RemoveBedProxy();

            Building_FridgeBedProxy proxy = ThingMaker.MakeThing(desired.BedDef) as Building_FridgeBedProxy;
            if (proxy == null)
            {
                Log.Error("[Nivarian Sleep In Fridges] Failed to create refrigerator bed proxy.");
                return;
            }

            proxy.ParentFridge = parent;
            proxy.SetFactionDirect(parent.Faction ?? Faction.OfPlayer);
            GenSpawn.Spawn(proxy, desired.Position, parent.Map, desired.Rotation, WipeMode.Vanish, false, false);
            bedProxy = proxy;
            ApplyMedicalState();
        }

        internal bool IsExpectedProxy(Building_FridgeBedProxy proxy)
        {
            return parent.Spawned && proxy != null && FridgeBedLayout.For(parent).Matches(proxy);
        }

        private void CollectExistingProxy(FridgeBedLayout desired)
        {
            Building_FridgeBedProxy matched = null;
            CollectExistingProxiesOfDef(NSIF_DefOf.NSIF_FridgeBedProxySingleCell, desired, ref matched);
            CollectExistingProxiesOfDef(NSIF_DefOf.NSIF_FridgeBedProxySingle, desired, ref matched);
            CollectExistingProxiesOfDef(NSIF_DefOf.NSIF_FridgeBedProxyDouble, desired, ref matched);
            bedProxy = matched;
        }

        private void CollectExistingProxiesOfDef(
            ThingDef proxyDef,
            FridgeBedLayout desired,
            ref Building_FridgeBedProxy matched)
        {
            List<Thing> things = parent.Map.listerThings.ThingsOfDef(proxyDef);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Building_FridgeBedProxy candidate = things[i] as Building_FridgeBedProxy;
                if (candidate == null || candidate.ParentFridge != parent)
                {
                    continue;
                }

                if (matched == null && desired.Matches(candidate))
                {
                    matched = candidate;
                }
                else
                {
                    DestroyProxy(candidate);
                }
            }
        }

        private void RemoveBedProxy()
        {
            DestroyProxy(bedProxy);
            bedProxy = null;
        }

        private void ApplyMedicalState()
        {
            if (bedProxy != null && bedProxy.Spawned && bedProxy.Medical != medical)
            {
                bedProxy.Medical = medical;
            }
        }

        private static void DestroyProxy(Building_FridgeBedProxy proxy)
        {
            if (proxy == null || proxy.Destroyed)
            {
                return;
            }

            List<Pawn> occupants = new List<Pawn>(proxy.CurOccupants);
            for (int i = 0; i < occupants.Count; i++)
            {
                Pawn pawn = occupants[i];
                if (pawn != null && pawn.jobs != null)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            }

            proxy.Destroy(DestroyMode.Vanish);
        }
    }
}
