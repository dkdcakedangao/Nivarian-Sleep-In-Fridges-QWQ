using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

// 正负缓存、日志输出，以及神秘的冰箱判定（依旧代码写到哪里，就放在哪里）
namespace NivarianSleepInFridges
{
    internal sealed class FridgeDefCacheEntry
    {
        internal readonly bool IsFridge;
        internal readonly string Reason;

        internal FridgeDefCacheEntry(bool isFridge, string reason)
        {
            IsFridge = isFridge;
            Reason = reason;
        }
    }

    internal static class FridgeDefRegistry
    {
        private static readonly Dictionary<ThingDef, FridgeDefCacheEntry> ProcessedDefs =
            new Dictionary<ThingDef, FridgeDefCacheEntry>();
        private static readonly HashSet<ThingDef> FridgeDefs = new HashSet<ThingDef>();

        internal static FridgeDefCacheEntry EnsureProcessed(
            ThingDef def,
            string source,
            out bool newlyProcessed)
        {
            newlyProcessed = false;
            if (def == null)
            {
                return new FridgeDefCacheEntry(false, "missing ThingDef");
            }

            FridgeDefCacheEntry cached;
            if (ProcessedDefs.TryGetValue(def, out cached))
            {
                FridgeDebugLog.CacheAccess(
                    "Cache hit from " + source + ": " + def.defName
                    + " => " + (cached.IsFridge ? "fridge" : "not a fridge")
                    + " (" + cached.Reason + ").");
                return cached;
            }

            string reason;
            bool isFridge = FridgeDefUtility.IsFridgeDef(def, out reason);
            cached = new FridgeDefCacheEntry(isFridge, reason);
            ProcessedDefs.Add(def, cached);
            newlyProcessed = true;

            FridgeDebugLog.Message(
                "Cached Def from " + source + ": " + def.defName
                + " => " + (isFridge ? "fridge" : "not a fridge")
                + " (" + reason + ").");

            if (!isFridge)
            {
                return cached;
            }

            bool componentInjected = EnsureCompProperties(def);
            FridgeDefs.Add(def);
            FridgeDebugLog.Message(
                "Fridge cache contains " + def.defName
                + "; count=" + FridgeDefs.Count
                + "; component=" + (componentInjected ? "injected" : "already present") + ".");
            return cached;
        }

        internal static List<ThingDef> FridgeDefsSnapshot()
        {
            return new List<ThingDef>(FridgeDefs);
        }

        private static bool EnsureCompProperties(ThingDef def)
        {
            if (def.comps == null)
            {
                def.comps = new List<CompProperties>();
            }

            for (int i = 0; i < def.comps.Count; i++)
            {
                CompProperties props = def.comps[i];
                if (props != null && props.compClass == typeof(CompFridgeSleep))
                {
                    return false;
                }
            }

            CompProperties_FridgeSleep fridgeSleep = new CompProperties_FridgeSleep();
            fridgeSleep.ResolveReferences(def);
            def.comps.Add(fridgeSleep);
            return true;
        }
    }

    internal static class FridgeDebugLog
    {
        private const string Prefix = "[Nivarian Sleep In Fridges][Debug] ";

        internal static void Message(string message)
        {
            SleepInFridgesSettings settings = SleepInFridgesMod.Settings;
            if (settings != null && settings.DebugLoggingEnabled)
            {
                Log.Message(Prefix + message);
            }
        }

        internal static void CacheAccess(string message)
        {
            SleepInFridgesSettings settings = SleepInFridgesMod.Settings;
            if (settings != null && settings.DebugLoggingEnabled && settings.VerboseCacheLoggingEnabled)
            {
                Log.Message(Prefix + message);
            }
        }
    }

    internal static class FridgeDefUtility
    {
        private const float MaximumRefrigerationTarget = 10f;

        internal static bool IsFridgeDef(ThingDef def, out string reason)
        {
            if (def == null)
            {
                reason = "missing ThingDef";
                return false;
            }

            if (def.thingClass == null || !typeof(Building_Storage).IsAssignableFrom(def.thingClass))
            {
                reason = "not a Building_Storage";
                return false;
            }

            if (TypeNameSignalsRefrigeration(def.thingClass))
            {
                reason = "building class name: " + TypeName(def.thingClass);
                return true;
            }

            if (def.comps != null)
            {
                for (int i = 0; i < def.comps.Count; i++)
                {
                    CompProperties props = def.comps[i];
                    if (props == null)
                    {
                        continue;
                    }

                    CompProperties_TempControl tempControl = props as CompProperties_TempControl;
                    if (tempControl != null && tempControl.defaultTargetTemperature <= MaximumRefrigerationTarget)
                    {
                        reason = "temperature target: " + tempControl.defaultTargetTemperature;
                        return true;
                    }

                    if (TypeNameSignalsRefrigeration(props.GetType()))
                    {
                        reason = "component properties class name: " + TypeName(props.GetType());
                        return true;
                    }

                    if (TypeNameSignalsRefrigeration(props.compClass))
                    {
                        reason = "component class name: " + TypeName(props.compClass);
                        return true;
                    }
                }
            }

            return HasCoolingExtension(def, out reason);
        }

        private static bool HasCoolingExtension(ThingDef def, out string reason)
        {
            if (def.modExtensions != null)
            {
                for (int i = 0; i < def.modExtensions.Count; i++)
                {
                    DefModExtension extension = def.modExtensions[i];
                    if (extension == null)
                    {
                        continue;
                    }

                    try
                    {
                        object temperature = ReadMember(extension, "temperature");
                        if (temperature == null)
                        {
                            continue;
                        }

                        object coolingOffset = ReadMember(temperature, "coolingOffset");
                        if (coolingOffset is float && (float)coolingOffset > 0f)
                        {
                            reason = "cooling extension: " + TypeName(extension.GetType());
                            return true;
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.Warning(
                            "[Nivarian Sleep In Fridges] Could not inspect temperature extension on "
                            + def.defName + ": " + exception.GetType().Name);
                    }
                }
            }

            reason = "no refrigeration signal";
            return false;
        }

        private static object ReadMember(object instance, string memberName)
        {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null && property.CanRead ? property.GetValue(instance, null) : null;
        }

        private static bool TypeNameSignalsRefrigeration(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string name = TypeName(type);
            return name.IndexOf("fridge", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("refrigerator", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string TypeName(Type type)
        {
            return type.FullName ?? type.Name;
        }
    }
}
