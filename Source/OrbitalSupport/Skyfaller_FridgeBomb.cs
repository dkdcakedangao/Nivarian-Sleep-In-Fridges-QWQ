using System;
using HarmonyLib;
using Nivarian_Race.Code.Defs;
using Nivarian_Race.Code.NivarianThing;
using Verse;

namespace NivarianSleepInFridges.OrbitalSupport
{
    // 本体负责运动、跃迁、拖尾和爆炸；本类识别冰箱弹，并在命中后留下本体冰刺。
    public sealed class Skyfaller_FridgeBomb : NivarianSkyfaller
    {
        private static readonly AccessTools.FieldRef<NivarianSkyfaller, NivarianBombardmentShellDef> Shell =
            AccessTools.FieldRefAccess<NivarianSkyfaller, NivarianBombardmentShellDef>("shellDef");

        // 提前触发字段访问器初始化，使签名失配在接入阶段被隔离。
        internal static void ValidateShellAccess()
        {
            if (Shell == null) throw new System.MissingFieldException("NivarianSkyfaller.shellDef");
        }

        internal bool IsFridgeShell => Shell(this)?.defName == "NSIF_BombardmentAction_Fridge";

        protected override void Impact()
        {
            if (hasImpacted) return;
            try
            {
                Map map = Map;
                IntVec3 center = Position;
                bool leaveSpikes = OrbitalSupportHost.IsAvailable && IsFridgeShell;
                // 本体命中会销毁弹体，地图、位置及动作必须提前保存。不能重放 base.Impact。
                base.Impact();
                if (!leaveSpikes || !OrbitalSupportHost.IsAvailable) return;
                FridgeBombFrostUtility.Spawn(map, center);
            }
            catch (Exception error)
            {
                // 本体可能在设置命中标记前就失败；先锁住，绝不重试已经部分执行的攻击。
                hasImpacted = true;
                try { OrbitalSupportHost.Disable(error); }
                finally
                {
                    try { if (!Destroyed) Destroy(); }
                    catch (Exception cleanup)
                    {
                        // 销毁也失败时，残留弹体不再参与 Tick；不新增轮询清理。
                        Find.TickManager.DeRegisterAllTickabilityFor(this);
                        Log.Warning("[Nivarian Sleep In Fridges] Failed to remove impacted shell "
                            + GetUniqueLoadID() + "; ticking stopped. " + cleanup);
                    }
                }
            }
        }
    }
}
