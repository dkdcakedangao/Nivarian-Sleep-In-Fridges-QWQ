using HarmonyLib;
using RimWorld;
using Verse;

// 偷吃的提示显示~
namespace NivarianSleepInFridges
{
    internal sealed class FridgeSnackingMessage : Message
    {
        internal FridgeSnackingMessage(string text, Pawn pawn)
            : base(text, MessageTypeDefOf.NeutralEvent, new LookTargets(pawn))
        {
            // 除错
            ResetTimer();
        }
    }

    [HarmonyPatch(typeof(Message), "ResetTimer")]
    internal static class Patch_Message_ResetTimer_Snacking
    {
        internal static void Postfix(Message __instance, ref float ___startingTime)
        {
            if (__instance is FridgeSnackingMessage)
            {
                // 缩短为 5 秒
                ___startingTime -= 8f;
            }
        }
    }
}
