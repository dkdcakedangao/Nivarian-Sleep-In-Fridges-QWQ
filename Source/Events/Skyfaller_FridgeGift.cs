using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// 落地以及清理相关的，基本上就是原版的下落动画，播放特效，生成冰箱什么的
namespace NivarianSleepInFridges
{
    public sealed class Skyfaller_FridgeGift : Skyfaller
    {
        // 待优化
        protected override void HitRoof() { }

        protected override void Impact()
        {
            hasImpacted = true;
            Map map = Map;
            Thing fridge = innerContainer.Count > 0 ? innerContainer[0] : null;
            IntVec3 cell = Position;
            if (fridge == null)
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] stage=landing success=false reason=待降容器中没有冰箱。");
                Destroy();
                return;
            }
            if (!FridgeGiftUtility.IsSafeCell(map, cell, fridge.def, this))
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][落地重检] map=" + map.uniqueID + " originalCell="
                        + cell + "；原落点失效，重新搜索。");
                if (!FridgeGiftUtility.TryFindCell(map, fridge.def, out cell))
                {
                    if (FridgeDebugLog.Enabled)
                        FridgeDebugLog.Message("[FridgeGift][执行结果] stage=landing success=false reason=原落点失效且无备用安全落点。");
                    Destroy();
                    return;
                }
            }

            innerContainer.Remove(fridge);
            Thing spawned = GenSpawn.Spawn(fridge, cell, map, Rot4.North);
            if (spawned == null || !fridge.Spawned)
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] stage=landing success=false cell=" + cell
                        + " reason=" + (spawned == null ? "GenSpawn 返回 null" : "冰箱未实际生成"));
                if (!fridge.Destroyed && !fridge.Spawned) fridge.Destroy();
                Destroy();
                return;
            }
            Current.Game.GetComponent<FridgeGiftComponent>().NotifyFinished(this);
            if (FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[FridgeGift][执行结果] stage=landing success=true map="
                    + map.uniqueID + " cell=" + cell + " fridge=" + fridge.GetUniqueLoadID());
            PlayLandingEffect(cell, map);
            Find.LetterStack.ReceiveLetter("NSIF_FridgeGiftLetterLabel".Translate(),
                "NSIF_FridgeGiftLetterText".Translate(), LetterDefOf.NeutralEvent, fridge);
            Destroy();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned && !hasImpacted)
            {
                if (FridgeDebugLog.Enabled)
                    FridgeDebugLog.Message("[FridgeGift][执行结果] stage=landing success=false reason=待降实例在落地前离开地图。");
                Current.Game.GetComponent<FridgeGiftComponent>().NotifyFinished(this);
            }
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!hasImpacted && FridgeDebugLog.Enabled)
                FridgeDebugLog.Message("[FridgeGift][执行结果] stage=landing success=false reason=待降实例在落地前被销毁。");
            Current.Game.GetComponent<FridgeGiftComponent>().NotifyFinished(this);
            base.Destroy(mode);
        }

        private static void PlayLandingEffect(IntVec3 cell, Map map)
        {
            FleckMaker.Static(cell, map, FleckDefOf.ExplosionFlash, 20f);
            DefDatabase<SoundDef>.GetNamed("MortarBomb_Explode").PlayOneShot(new TargetInfo(cell, map));
            FleckDef smoke = DefDatabase<FleckDef>.GetNamed("NSIF_FridgeGiftSmoke");
            // 烟雾的特效，我特别喜欢这个一圈圈的烟雾特效
            for (int ring = 0; ring <= 3; ring++)
            {
                int count = ring == 0 ? 1 : ring * 8;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    Vector3 position = cell.ToVector3Shifted()
                        + new Vector3(Mathf.Cos(angle) * ring * 3.5f, 0f, Mathf.Sin(angle) * ring * 3.5f);
                    if (!position.ToIntVec3().InBounds(map)) continue;
                    FleckCreationData data = FleckMaker.GetDataStatic(position, map, smoke, 4f);
                    data.rotationRate = Rand.Range(-15f, 15f);
                    map.flecks.CreateFleck(data);
                }
            }
        }
    }
}
