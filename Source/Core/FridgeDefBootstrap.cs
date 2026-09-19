using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

// 冰箱列表检测和输出
namespace NivarianSleepInFridges
{
    [StaticConstructorOnStartup]
    internal static class FridgeDefBootstrap
    {
        private static readonly HashSet<ThingDef> FridgeDefs = new HashSet<ThingDef>();

        static FridgeDefBootstrap()
        {
            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                if (!FridgeDefUtility.IsFridgeDef(def))
                {
                    continue;
                }

                FridgeDefs.Add(def);
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }

                bool alreadyInjected = false;
                for (int j = 0; j < def.comps.Count; j++)
                {
                    CompProperties props = def.comps[j];
                    if (props != null && props.compClass == typeof(CompFridgeSleep))
                    {
                        alreadyInjected = true;
                        break;
                    }
                }

                if (!alreadyInjected)
                {
                    def.comps.Add(new CompProperties_FridgeSleep());
                }
            }

            List<string> detectedNames = new List<string>();
            foreach (ThingDef fridgeDef in FridgeDefs)
            {
                detectedNames.Add(fridgeDef.defName);
            }

            detectedNames.Sort(StringComparer.Ordinal);
            Log.Message(
                "[Nivarian Sleep In Fridges] Detected "
                + FridgeDefs.Count
                + " refrigerator definition(s): "
                + string.Join(", ", detectedNames.ToArray()));
        }

    }

    // 石山核心
    // 传说中的冰箱检测系统！匠心巨著！
    // 只要是目标温度低于10°C，同时是容器/单纯的def包含冰箱的名儿
    // 这就是冰箱
    // 什么叫联合重工的保温垃圾桶也是冰箱？！
    internal static class FridgeDefUtility
    {
        private const float MaximumRefrigerationTarget = 10f;

        internal static bool IsFridgeDef(ThingDef def)
        {
            if (def == null || def.thingClass == null || !typeof(Building_Storage).IsAssignableFrom(def.thingClass))
            {
                return false;
            }

            if (TypeNameSignalsRefrigeration(def.thingClass))
            {
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
                        return true;
                    }

                    if (TypeNameSignalsRefrigeration(props.GetType()) || TypeNameSignalsRefrigeration(props.compClass))
                    {
                        return true;
                    }
                }
            }

            return HasCoolingExtension(def);
        }

        private static bool HasCoolingExtension(ThingDef def)
        {
            if (def.modExtensions == null)
            {
                return false;
            }

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
                        return true;
                    }
                }
                
                // 除错
                catch (Exception exception)
                {
                    Log.Warning(
                        "[Nivarian Sleep In Fridges] Could not inspect temperature extension on "
                        + def.defName
                        + ": "
                        + exception.GetType().Name);
                }
            }

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

            string name = type.FullName ?? type.Name;
            return name.IndexOf("fridge", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("refrigerator", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
