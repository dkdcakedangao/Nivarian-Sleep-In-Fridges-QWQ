using System.Collections.Generic;
using RimWorld;
using Verse;

// 床的合法判断 床到底是不是床（？）
namespace NivarianSleepInFridges
{
    public sealed class Building_FridgeBedProxy : Building_Bed
    {
        public ThingWithComps ParentFridge;

        public bool IsActive
        {
            get
            {
                if (!Spawned || ParentFridge == null || !ParentFridge.Spawned || !FridgeSleepUtility.Enabled)
                {
                    return false;
                }

                CompFridgeSleep comp = ParentFridge.GetComp<CompFridgeSleep>();
                return comp != null && comp.Active;
            }
        }

        internal bool HidesSleeper
        {
            get { return def == NSIF_DefOf.NSIF_FridgeBedProxySingleCell; }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ParentFridge, "parentFridge");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                LongEventHandler.ExecuteWhenFinished(RemoveIfOrphaned);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            yield break;
        }

        public override void DrawGUIOverlay()
        {
        }

        public override void TickRare()
        {
            base.TickRare();
            FridgeMoodUtility.RefreshSpaceThoughts(this);
            FridgeIcyCoreUtility.RecoverOccupants(this);
        }

        private void RemoveIfOrphaned()
        {
            if (!Spawned)
            {
                return;
            }

            CompFridgeSleep comp = ParentFridge == null ? null : ParentFridge.GetComp<CompFridgeSleep>();
            if (ParentFridge == null
                || !ParentFridge.Spawned
                || ParentFridge.Map != Map
                || comp == null
                || !comp.IsExpectedProxy(this))
            {
                Destroy(DestroyMode.Vanish);
            }
        }
    }

    public sealed class CompAssignableToPawn_FridgeBed : CompAssignableToPawn_Bed
    {
        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                foreach (Pawn pawn in base.AssigningCandidates)
                {
                    if (FridgeSleepUtility.IsEligibleSleeper(pawn))
                    {
                        yield return pawn;
                    }
                }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            Building_Bed bed = parent as Building_Bed;
            if (bed != null)
            {
                Props.maxAssignedPawnsCount = bed.SleepingSlotsCount;
            }

            base.PostSpawnSetup(respawningAfterLoad);
        }

        public override AcceptanceReport CanAssignTo(Pawn pawn)
        {
            if (!FridgeSleepUtility.IsEligibleSleeper(pawn))
            {
                return "NSIF_OnlyNivarianColonists".Translate();
            }

            return base.CanAssignTo(pawn);
        }

        public override void TryAssignPawn(Pawn pawn)
        {
            if (FridgeSleepUtility.IsEligibleSleeper(pawn))
            {
                base.TryAssignPawn(pawn);
            }
        }

        public override void ForceAddPawn(Pawn pawn)
        {
            Building_FridgeBedProxy proxy = parent as Building_FridgeBedProxy;
            if (proxy != null
                && proxy.IsActive
                && !proxy.Medical
                && FridgeSleepUtility.IsEligibleSleeper(pawn))
            {
                base.ForceAddPawn(pawn);
            }
        }

        protected override bool ShouldShowAssignmentGizmo()
        {
            Building_FridgeBedProxy proxy = parent as Building_FridgeBedProxy;
            return proxy != null && proxy.IsActive && base.ShouldShowAssignmentGizmo();
        }
    }

    [DefOf]
    public static class NSIF_DefOf
    {
        public static ThingDef NSIF_FridgeBedProxySingleCell;
        public static ThingDef NSIF_FridgeBedProxySingle;
        public static ThingDef NSIF_FridgeBedProxyDouble;
        public static ThoughtDef NSIF_SpaceSingleTiny;
        public static ThoughtDef NSIF_SpaceSingleComfortable;
        public static ThoughtDef NSIF_SpaceSingleLarge;
        public static ThoughtDef NSIF_SpaceDoubleJustRight;
        public static ThoughtDef NSIF_SpaceDoubleLarge;
        public static ThoughtDef NSIF_SleepDisturbed;
        public static ThoughtDef NSIF_CuteDragonInFridge;

        static NSIF_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(NSIF_DefOf));
        }
    }
}
