using System.Collections.Generic;
using RimWorld;
using Verse;

// 心情相关的，都放在这里
// 和容量相关的部分待优化
// 这段有部分是gpt老师写的，问题不大
// 虽然写了之后，还是要我自己除错就是了
namespace NivarianSleepInFridges
{
    internal static class FridgeMoodUtility
    {
        private static readonly ThoughtDef[] SpaceThoughts =
        {
            NSIF_DefOf.NSIF_SpaceSingleTiny,
            NSIF_DefOf.NSIF_SpaceSingleComfortable,
            NSIF_DefOf.NSIF_SpaceSingleLarge,
            NSIF_DefOf.NSIF_SpaceDoubleJustRight,
            NSIF_DefOf.NSIF_SpaceDoubleLarge
        };

        internal static void RefreshSpaceThoughts(Building_FridgeBedProxy proxy)
        {
            if (proxy == null
                || !proxy.IsActive
                || proxy.ParentFridge == null
                || (SleepInFridgesMod.Settings != null && !SleepInFridgesMod.Settings.SleepMoodEnabled))
            {
                return;
            }

            ThoughtDef thought = SpaceThoughtFor(proxy);
            if (thought == null)
            {
                return;
            }

            foreach (Pawn pawn in proxy.CurOccupants)
            {
                if (!IsActuallySleeping(pawn, proxy))
                {
                    continue;
                }

                RemoveOtherSpaceThoughts(pawn, thought);
                GainOrRefreshMemory(pawn, thought);
            }
        }

        internal static void NotifyStorageInteraction(Pawn interactor, ThingWithComps fridge)
        {
            if (interactor == null
                || fridge == null
                || SleepInFridgesMod.Settings == null
                || !SleepInFridgesMod.Settings.InteractionMoodEnabled)
            {
                return;
            }

            CompFridgeSleep comp = fridge.GetComp<CompFridgeSleep>();
            Building_FridgeBedProxy proxy = comp == null ? null : comp.BedProxy;
            if (comp == null || !comp.Active || proxy == null || !proxy.IsActive || !comp.TryRegisterInteraction(interactor))
            {
                return;
            }

            bool disturbedAnyone = false;
            foreach (Pawn sleeper in proxy.CurOccupants)
            {
                if (!IsActuallySleeping(sleeper, proxy))
                {
                    continue;
                }

                GainOrRefreshMemory(sleeper, NSIF_DefOf.NSIF_SleepDisturbed);
                disturbedAnyone = true;
            }

            if (disturbedAnyone)
            {
                GainOrRefreshMemory(interactor, NSIF_DefOf.NSIF_CuteDragonInFridge);
            }
        }

        internal static ThingWithComps FridgeAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return null;
            }

            SlotGroup slotGroup = StoreUtility.GetSlotGroup(cell, map);
            ThingWithComps parent = slotGroup == null ? null : slotGroup.parent as ThingWithComps;
            return parent != null && parent.GetComp<CompFridgeSleep>() != null ? parent : null;
        }

        internal static ThingWithComps FridgeHolding(Thing thing)
        {
            if (thing == null)
            {
                return null;
            }

            if (thing.Spawned)
            {
                ThingWithComps fridge = FridgeAt(thing.Position, thing.Map);
                if (fridge != null)
                {
                    return fridge;
                }
            }

            return FridgeFromHolder(thing.ParentHolder);
        }

        internal static ThingWithComps FridgeFromHolder(IThingHolder holder)
        {
            IThingHolder current = holder;
            for (int depth = 0; current != null && depth < 8; depth++)
            {
                ThingWithComps thing = current as ThingWithComps;
                if (thing != null && thing.GetComp<CompFridgeSleep>() != null)
                {
                    return thing;
                }

                ThingComp comp = current as ThingComp;
                if (comp != null)
                {
                    ThingWithComps compParent = comp.parent;
                    if (compParent != null && compParent.GetComp<CompFridgeSleep>() != null)
                    {
                        return compParent;
                    }

                    current = compParent == null ? null : compParent.ParentHolder;
                    continue;
                }

                current = current.ParentHolder;
            }

            return null;
        }

        internal static Pawn PawnFromHolder(IThingHolder holder)
        {
            IThingHolder current = holder;
            for (int depth = 0; current != null && depth < 8; depth++)
            {
                Pawn pawn = current as Pawn;
                if (pawn != null)
                {
                    return pawn;
                }

                Pawn_CarryTracker carryTracker = current as Pawn_CarryTracker;
                if (carryTracker != null)
                {
                    return carryTracker.pawn;
                }

                Pawn_InventoryTracker inventory = current as Pawn_InventoryTracker;
                if (inventory != null)
                {
                    return inventory.pawn;
                }

                current = current.ParentHolder;
            }

            return null;
        }

        private static ThoughtDef SpaceThoughtFor(Building_FridgeBedProxy proxy)
        {
            int area = proxy.ParentFridge.OccupiedRect().Area;
            if (proxy.def == NSIF_DefOf.NSIF_FridgeBedProxyDouble)
            {
                return area == 4 ? NSIF_DefOf.NSIF_SpaceDoubleJustRight : NSIF_DefOf.NSIF_SpaceDoubleLarge;
            }

            if (area == 1)
            {
                return NSIF_DefOf.NSIF_SpaceSingleTiny;
            }

            return area == 2 ? NSIF_DefOf.NSIF_SpaceSingleComfortable : NSIF_DefOf.NSIF_SpaceSingleLarge;
        }

        private static bool IsActuallySleeping(Pawn pawn, Building_FridgeBedProxy proxy)
        {
            return pawn != null
                && pawn.jobs != null
                && pawn.jobs.curDriver != null
                && pawn.jobs.curDriver.asleep
                && pawn.CurrentBed() == proxy;
        }

        private static void RemoveOtherSpaceThoughts(Pawn pawn, ThoughtDef keep)
        {
            MemoryThoughtHandler memories = Memories(pawn);
            if (memories == null)
            {
                return;
            }

            for (int i = 0; i < SpaceThoughts.Length; i++)
            {
                ThoughtDef thought = SpaceThoughts[i];
                if (thought != null && thought != keep)
                {
                    memories.RemoveMemoriesOfDef(thought);
                }
            }
        }

        private static void GainOrRefreshMemory(Pawn pawn, ThoughtDef thought)
        {
            MemoryThoughtHandler memories = Memories(pawn);
            if (memories == null || thought == null)
            {
                return;
            }

            Thought_Memory memory = memories.GetFirstMemoryOfDef(thought);
            if (memory == null)
            {
                memories.TryGainMemory(thought);
            }
            else
            {
                memory.age = 0;
            }
        }

        private static MemoryThoughtHandler Memories(Pawn pawn)
        {
            return pawn == null
                || pawn.needs == null
                || pawn.needs.mood == null
                || pawn.needs.mood.thoughts == null
                ? null
                : pawn.needs.mood.thoughts.memories;
        }
    }
}
