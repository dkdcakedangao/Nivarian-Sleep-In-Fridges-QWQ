using System;
using System.Reflection;
using HarmonyLib;
using Verse;

// 捕获成功的存取操作，并把统一的冰箱与互动者信息交给心情系统
// 这段完全就是GPT老师写的
// 我完全不知道怎么写~
namespace NivarianSleepInFridges
{
    internal struct FridgeTransferState
    {
        internal ThingWithComps Fridge;
        internal Pawn Pawn;
    }

    [HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new Type[] { typeof(Thing), typeof(int), typeof(bool) })]
    internal static class Patch_PawnCarryTracker_TryStartCarryCount
    {
        private static void Prefix(Pawn_CarryTracker __instance, Thing item, ref FridgeTransferState __state)
        {
            __state.Fridge = FridgeMoodUtility.FridgeHolding(item);
            __state.Pawn = __instance.pawn;
        }

        private static void Postfix(int __result, FridgeTransferState __state)
        {
            if (__result > 0)
            {
                FridgeMoodUtility.NotifyStorageInteraction(__state.Pawn, __state.Fridge);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_CarryTracker), "TryStartCarry", new Type[] { typeof(Thing) })]
    internal static class Patch_PawnCarryTracker_TryStartCarryThing
    {
        private static void Prefix(Pawn_CarryTracker __instance, Thing item, ref FridgeTransferState __state)
        {
            __state.Fridge = FridgeMoodUtility.FridgeHolding(item);
            __state.Pawn = __instance.pawn;
        }

        private static void Postfix(bool __result, FridgeTransferState __state)
        {
            if (__result)
            {
                FridgeMoodUtility.NotifyStorageInteraction(__state.Pawn, __state.Fridge);
            }
        }
    }

    [HarmonyPatch]
    internal static class Patch_PawnCarryTracker_TryDropCarriedThing
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_CarryTracker), "TryDropCarriedThing", new Type[]
            {
                typeof(IntVec3),
                typeof(ThingPlaceMode),
                typeof(Thing).MakeByRefType(),
                typeof(Action<Thing, int>)
            });
        }

        private static void Postfix(Pawn_CarryTracker __instance, bool __result, Thing resultingThing)
        {
            if (__result && resultingThing != null)
            {
                FridgeMoodUtility.NotifyStorageInteraction(
                    __instance.pawn,
                    FridgeMoodUtility.FridgeHolding(resultingThing));
            }
        }
    }
    
    [HarmonyPatch]
    internal static class Patch_PawnCarryTracker_TryDropCarriedThingCount
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_CarryTracker), "TryDropCarriedThing", new Type[]
            {
                typeof(IntVec3),
                typeof(int),
                typeof(ThingPlaceMode),
                typeof(Thing).MakeByRefType(),
                typeof(Action<Thing, int>)
            });
        }

        private static void Postfix(Pawn_CarryTracker __instance, bool __result, Thing resultingThing)
        {
            if (__result && resultingThing != null)
            {
                FridgeMoodUtility.NotifyStorageInteraction(
                    __instance.pawn,
                    FridgeMoodUtility.FridgeHolding(resultingThing));
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwner), "TryTransferToContainer", new Type[]
    {
        typeof(Thing),
        typeof(ThingOwner),
        typeof(int),
        typeof(bool)
    })]
    internal static class Patch_ThingOwner_TryTransferToContainer
    {
        private static void Prefix(ThingOwner __instance, ThingOwner otherContainer, ref FridgeTransferState __state)
        {
            ThingWithComps sourceFridge = FridgeMoodUtility.FridgeFromHolder(__instance.Owner);
            ThingWithComps destinationFridge = FridgeMoodUtility.FridgeFromHolder(otherContainer.Owner);
            __state.Fridge = sourceFridge ?? destinationFridge;
            __state.Pawn = sourceFridge != null
                ? FridgeMoodUtility.PawnFromHolder(otherContainer.Owner)
                : FridgeMoodUtility.PawnFromHolder(__instance.Owner);
        }

        private static void Postfix(int __result, FridgeTransferState __state)
        {
            if (__result > 0)
            {
                FridgeMoodUtility.NotifyStorageInteraction(__state.Pawn, __state.Fridge);
            }
        }
    }

    [HarmonyPatch(typeof(ThingOwner), "TryTransferToContainer", new Type[]
    {
        typeof(Thing),
        typeof(ThingOwner),
        typeof(bool)
    })]
    internal static class Patch_ThingOwner_TryTransferToContainerAll
    {
        private static void Prefix(ThingOwner __instance, ThingOwner otherContainer, ref FridgeTransferState __state)
        {
            Capture(__instance, otherContainer, ref __state);
        }

        private static void Postfix(bool __result, FridgeTransferState __state)
        {
            if (__result)
            {
                FridgeMoodUtility.NotifyStorageInteraction(__state.Pawn, __state.Fridge);
            }
        }

        internal static void Capture(ThingOwner source, ThingOwner destination, ref FridgeTransferState state)
        {
            ThingWithComps sourceFridge = FridgeMoodUtility.FridgeFromHolder(source.Owner);
            ThingWithComps destinationFridge = FridgeMoodUtility.FridgeFromHolder(destination.Owner);
            state.Fridge = sourceFridge ?? destinationFridge;
            state.Pawn = sourceFridge != null
                ? FridgeMoodUtility.PawnFromHolder(destination.Owner)
                : FridgeMoodUtility.PawnFromHolder(source.Owner);
        }
    }

    [HarmonyPatch]
    internal static class Patch_ThingOwner_TryTransferToContainerResult
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ThingOwner), "TryTransferToContainer", new Type[]
            {
                typeof(Thing),
                typeof(ThingOwner),
                typeof(int),
                typeof(Thing).MakeByRefType(),
                typeof(bool)
            });
        }

        private static void Prefix(ThingOwner __instance, ThingOwner otherContainer, ref FridgeTransferState __state)
        {
            Patch_ThingOwner_TryTransferToContainerAll.Capture(__instance, otherContainer, ref __state);
        }

        private static void Postfix(int __result, FridgeTransferState __state)
        {
            if (__result > 0)
            {
                FridgeMoodUtility.NotifyStorageInteraction(__state.Pawn, __state.Fridge);
            }
        }
    }
}
