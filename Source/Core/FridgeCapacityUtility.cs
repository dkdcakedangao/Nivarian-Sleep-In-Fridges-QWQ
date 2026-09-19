using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

// 容量计算
// 这部分也是GPT老师帮忙的？
namespace NivarianSleepInFridges
{   

    // 容量减少相关的
    internal static class FridgeCapacityUtility
    {
        private const int DefaultSingleBedMaximumReduction = 10;
        private const int DefaultDoubleBedMaximumReduction = 20;
        private const int MaximumConfiguredReduction = 100;

        internal static int EffectiveCapacity(int originalCapacity, int sleepingCapacity)
        {
            int maximumReduction = MaximumReduction(sleepingCapacity);
            int reduction = Math.Min(originalCapacity / 2, maximumReduction);
            return Math.Max(0, originalCapacity - reduction);
        }

        // 滑块容量变化的修复
        // 修石山代码的
        internal static int OriginalCapacityForEffective(
            int effectiveCapacity,
            int sleepingCapacity,
            int maximumOriginalCapacity)
        {
            int maximumReduction = MaximumReduction(sleepingCapacity);
            int maximumEffectiveCapacity = EffectiveCapacity(maximumOriginalCapacity, sleepingCapacity);
            int clampedEffectiveCapacity = Math.Max(0, Math.Min(effectiveCapacity, maximumEffectiveCapacity));
            int originalCapacity = clampedEffectiveCapacity <= maximumReduction
                ? clampedEffectiveCapacity * 2
                : clampedEffectiveCapacity + maximumReduction;
            return Math.Min(originalCapacity, maximumOriginalCapacity);
        }

        private static int MaximumReduction(int sleepingCapacity)
        {
            SleepInFridgesSettings settings = SleepInFridgesMod.Settings;
            if (settings != null && !settings.CapacityReductionEnabled)
            {
                return 0;
            }

            int configuredReduction = sleepingCapacity >= 2
                ? (settings == null ? DefaultDoubleBedMaximumReduction : settings.DoubleBedMaximumReduction)
                : (settings == null ? DefaultSingleBedMaximumReduction : settings.SingleBedMaximumReduction);
            return Math.Max(0, Math.Min(configuredReduction, MaximumConfiguredReduction));
        }

        internal static int EffectiveCapacity(Building_Storage storage, CompFridgeSleep comp)
        {
            int originalCapacity;
            if (!AdaptiveStorageBridge.TryGetOriginalCapacity(storage, out originalCapacity))
            {
                originalCapacity = storage.MaxItemsInCell * storage.OccupiedRect().Area;
            }

            return comp != null && comp.Active
                ? EffectiveCapacity(originalCapacity, comp.SleepingCapacity)
                : originalCapacity;
        }

        internal static int VanillaCellCapacity(
            Building_Storage storage,
            CompFridgeSleep comp,
            IntVec3 cell,
            Map map,
            int originalCellCapacity)
        {
            if (comp == null || !comp.Active || AdaptiveStorageBridge.IsAdaptiveStorage(storage))
            {
                return originalCellCapacity;
            }

            CellRect occupiedRect = storage.OccupiedRect();
            int cellCount = occupiedRect.Area;
            if (cellCount <= 0 || !occupiedRect.Contains(cell))
            {
                return originalCellCapacity;
            }

            int originalCapacity = originalCellCapacity * cellCount;
            int effectiveCapacity = EffectiveCapacity(originalCapacity, comp.SleepingCapacity);
            int capacityPerCell = effectiveCapacity / cellCount;
            int cellsWithExtraCapacity = effectiveCapacity % cellCount;
            int cellIndex = (cell.z - occupiedRect.minZ) * occupiedRect.Width + cell.x - occupiedRect.minX;
            int distributedCellCapacity = capacityPerCell + (cellIndex < cellsWithExtraCapacity ? 1 : 0);

            SlotGroup slotGroup = storage.GetSlotGroup();
            int heldThings = slotGroup == null ? 0 : slotGroup.HeldThingsCount;
            int remainingCapacity = Math.Max(0, effectiveCapacity - heldThings);
            int currentCellItems = GridsUtility.GetItemCount(cell, map);
            return Math.Min(distributedCellCapacity, currentCellItems + remainingCapacity);
        }

        internal static void Refresh(ThingWithComps fridge)
        {
            AdaptiveStorageBridge.Refresh(fridge as Building_Storage);
        }
    }

    // 读容量相关的
    // 这部分完全就是GPT老师写的
    internal static class AdaptiveStorageBridge
    {
        private const string AdaptiveStorageTypeName = "AdaptiveStorage.ThingClass";
        private static Type adaptiveStorageType;
        private static FieldInfo currentSlotLimitField;
        private static PropertyInfo totalSlotsProperty;
        private static MethodInfo updateMaxItemsInCellMethod;
        private static MethodInfo notifySettingsChangedMethod;
        private static bool refreshErrorLogged;

        internal static void Initialize(Harmony harmony)
        {
            adaptiveStorageType = AccessTools.TypeByName(AdaptiveStorageTypeName);
            if (adaptiveStorageType == null)
            {
                return;
            }

            currentSlotLimitField = AccessTools.Field(adaptiveStorageType, "_currentSlotLimit");
            totalSlotsProperty = AccessTools.Property(adaptiveStorageType, "TotalSlots");
            updateMaxItemsInCellMethod = AccessTools.Method(adaptiveStorageType, "UpdateMaxItemsInCell");
            notifySettingsChangedMethod = AccessTools.Method(adaptiveStorageType, "Notify_SettingsChanged");
            MethodInfo currentSlotLimitGetter = AccessTools.PropertyGetter(adaptiveStorageType, "CurrentSlotLimit");
            MethodInfo currentSlotLimitSetter = AccessTools.PropertySetter(adaptiveStorageType, "CurrentSlotLimit");

            if (currentSlotLimitField == null
                || totalSlotsProperty == null
                || updateMaxItemsInCellMethod == null
                || currentSlotLimitGetter == null
                || currentSlotLimitSetter == null)
            {
                Log.Warning("[Nivarian Sleep In Fridges] Adaptive Storage capacity API was not found; using vanilla capacity handling.");
                adaptiveStorageType = null;
                return;
            }

            HarmonyMethod postfix = new HarmonyMethod(
                typeof(AdaptiveStorageBridge),
                "CurrentSlotLimitPostfix");
            harmony.Patch(currentSlotLimitGetter, null, postfix);

            HarmonyMethod prefix = new HarmonyMethod(
                typeof(AdaptiveStorageBridge),
                "CurrentSlotLimitPrefix");
            harmony.Patch(currentSlotLimitSetter, prefix, null);
        }

        internal static bool IsAdaptiveStorage(object storage)
        {
            return adaptiveStorageType != null && storage != null && adaptiveStorageType.IsInstanceOfType(storage);
        }

        internal static bool TryGetOriginalCapacity(Building_Storage storage, out int capacity)
        {
            capacity = 0;
            if (!IsAdaptiveStorage(storage))
            {
                return false;
            }

            int configuredLimit = (int)currentSlotLimitField.GetValue(storage);
            int totalSlots = (int)totalSlotsProperty.GetValue(storage, null);
            capacity = Math.Max(0, Math.Min(configuredLimit, totalSlots));
            return true;
        }

        internal static void Refresh(Building_Storage storage)
        {
            if (!IsAdaptiveStorage(storage))
            {
                return;
            }

            try
            {
                updateMaxItemsInCellMethod.Invoke(storage, null);
                if (notifySettingsChangedMethod != null)
                {
                    notifySettingsChangedMethod.Invoke(storage, null);
                }
            }
            catch (Exception exception)
            {
                if (!refreshErrorLogged)
                {
                    refreshErrorLogged = true;
                    Log.Error(
                        "[Nivarian Sleep In Fridges] Failed to refresh Adaptive Storage capacity: "
                        + exception);
                }
            }
        }

        private static void CurrentSlotLimitPostfix(object __instance, ref int __result)
        {
            ThingWithComps fridge = __instance as ThingWithComps;
            CompFridgeSleep comp = fridge == null ? null : fridge.GetComp<CompFridgeSleep>();
            if (comp != null && comp.Active)
            {
                __result = FridgeCapacityUtility.EffectiveCapacity(__result, comp.SleepingCapacity);
            }
        }

        private static void CurrentSlotLimitPrefix(object __instance, ref int __0)
        {
            ThingWithComps fridge = __instance as ThingWithComps;
            CompFridgeSleep comp = fridge == null ? null : fridge.GetComp<CompFridgeSleep>();
            if (comp == null || !comp.Active)
            {
                return;
            }

            int configuredLimit = (int)currentSlotLimitField.GetValue(__instance);
            int totalSlots = (int)totalSlotsProperty.GetValue(__instance, null);
            int originalCapacity = Math.Max(0, Math.Min(configuredLimit, totalSlots));
            int currentEffectiveCapacity = FridgeCapacityUtility.EffectiveCapacity(
                originalCapacity,
                comp.SleepingCapacity);
            if (__0 == currentEffectiveCapacity)
            {
                __0 = configuredLimit;
                return;
            }

            __0 = FridgeCapacityUtility.OriginalCapacityForEffective(
                __0,
                comp.SleepingCapacity,
                totalSlots);
        }
    }
}
