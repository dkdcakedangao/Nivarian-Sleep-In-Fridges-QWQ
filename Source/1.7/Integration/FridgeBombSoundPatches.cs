using HarmonyLib;
using Verse;

namespace NivarianSleepInFridges
{
    // 留在主模块：视觉后端停用后，在途或已提交炮弹仍可安全结算爆炸。
    [HarmonyPatch(typeof(GenExplosion), nameof(GenExplosion.DoExplosion))]
    internal static class FridgeBombSoundPatches
    {
        private static ThingDef shellDef;
        private static SoundDef explosionSoundFallback;

        internal static void Initialize()
        {
            shellDef = DefDatabase<ThingDef>.GetNamedSilentFail("NSIF_FridgeBombIncoming");
            explosionSoundFallback = DefDatabase<SoundDef>.GetNamedSilentFail("Explosion_Bomb");
            if (shellDef != null && explosionSoundFallback == null)
                Log.Warning("[Nivarian Sleep In Fridges] Missing Explosion_Bomb; refrigerator shell sound repair is unavailable.");
        }

        private static void Prefix(Thing instigator, DamageDef damType, IntVec3 center, ref SoundDef explosionSound)
        {
            if (instigator == null || shellDef == null || instigator.def != shellDef
                || explosionSound != null || damType == null || damType.soundExplosion != null
                || explosionSoundFallback == null) return;

            explosionSound = explosionSoundFallback;
            if (FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[OrbitalSupport][音效补齐] shell=" + instigator.GetUniqueLoadID()
                    + " cell=" + center + " damage=" + damType.defName + " sound=" + explosionSound.defName);
        }
    }
}
