using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

// 冰淇淋尾巴的联动
namespace NivarianSleepInFridges
{
    internal static class IcecreamTailCompatibility
    {
        private const string FlavorUtilityTypeName = "NivarianIcecreamTail.IcecreamTailFlavorUtility";
        private const string RecoveryHediffDefName = "IcecreamTailRecovery";

        internal static void Initialize(Harmony harmony)
        {
            Type flavorUtilityType = AccessTools.TypeByName(FlavorUtilityTypeName);
            if (flavorUtilityType == null)
            {
                return;
            }

            MethodInfo recoverySpeedFactor = AccessTools.Method(
                flavorUtilityType,
                "RecoverySpeedFactor",
                new Type[] { typeof(Hediff) });
            if (recoverySpeedFactor == null)
            {
                Log.Warning("[Nivarian Sleep In Fridges] Icecream Tail recovery API was not found; compatibility is disabled.");
                return;
            }

            HarmonyMethod postfix = new HarmonyMethod(
                typeof(IcecreamTailCompatibility),
                "RecoverySpeedFactorPostfix");
            harmony.Patch(recoverySpeedFactor, null, postfix);
        }

        private static void RecoverySpeedFactorPostfix(Hediff recovery, ref float __result)
        {
            SleepInFridgesSettings settings = SleepInFridgesMod.Settings;
            if (settings == null
                || !settings.IcecreamTailRecoveryBoostEnabled
                || recovery == null
                || recovery.def == null
                || recovery.def.defName != RecoveryHediffDefName)
            {
                return;
            }

            Pawn pawn = recovery.pawn;
            Building_FridgeBedProxy proxy = pawn == null ? null : pawn.CurrentBed() as Building_FridgeBedProxy;
            if (proxy == null || !proxy.IsActive || proxy.ParentFridge == null)
            {
                return;
            }

            CompPowerTrader power = proxy.ParentFridge.GetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn)
            {
                return;
            }

            __result *= Mathf.Clamp(settings.IcecreamTailRecoveryMultiplier, 1f, 10f);
        }
    }
}
