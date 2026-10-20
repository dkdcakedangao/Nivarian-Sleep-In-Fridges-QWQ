using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LumiParticle.Core;
using LumiParticle.Core.Lifetime;
using LumiParticle.Data;
using LumiParticle.Effects;
using LumiParticle.Items;
using RimUnity;
using Verse;

namespace NivarianSleepInFridges.OrbitalSupport
{
    // 仅创建时换外观、归还对象池前还原。不增加炮弹 Tick 或绘制轮询。
    internal static class FridgeBombVisuals
    {
        private sealed class Replacement
        {
            internal readonly List<Behaviour> Renderers = new List<Behaviour>();
            internal readonly List<bool> Enabled = new List<bool>();
            internal GameObject Fridge;

            internal void Hide(Behaviour renderer)
            {
                Renderers.Add(renderer);
                Enabled.Add(renderer.enabled);
                renderer.enabled = false;
            }

            internal void Restore()
            {
                List<Exception> errors = new List<Exception>();
                for (int i = 0; i < Renderers.Count; i++)
                {
                    try { if (Renderers[i] != null) Renderers[i].enabled = Enabled[i]; }
                    catch (Exception error) { errors.Add(error); }
                }
                Renderers.Clear();
                Enabled.Clear();
                GameObject fridge = Fridge;
                Fridge = null;
                if (fridge != null)
                {
                    try { fridge.SetActive(false); }
                    catch (Exception error) { errors.Add(error); }
                    try { fridge.transform.SetParent(null, false); }
                    catch (Exception error) { errors.Add(error); }
                    try { RimUnity.Object.Destroy(fridge); }
                    catch (Exception error) { errors.Add(error); }
                }
                if (errors.Count != 0) throw new AggregateException("冰箱弹外观还原失败。", errors);
            }
        }

        private static readonly Dictionary<LumiVisualItem, Replacement> Replacements =
            new Dictionary<LumiVisualItem, Replacement>();
        private static readonly AccessTools.FieldRef<LumiVisualItem, GameObject> VisualObject =
            AccessTools.FieldRefAccess<LumiVisualItem, GameObject>("_gameObject");
        private static Mesh fridgeMesh;
        private static Material fridgeMaterial;

        internal static void ValidateAccess()
        {
            if (VisualObject == null) throw new MissingFieldException("LumiVisualItem._gameObject");
        }

        private static void Discard(LumiVisualItem item)
        {
            GameObject root = VisualObject(item);
            // Destroy 是延迟销毁。必须先断开回收引用，再执行可能抛错的框架清理。
            // 原版 Recycle 继续释放生命周期和托管引用，但不会再调用对象池 Return。
            VisualObject(item) = null;
            OrbitalSupportHost.Trace("外观还原失败，已隔离弹体，禁止归池。");
            if (root == null) return;
            List<Exception> errors = new List<Exception>();
            try { root.SetActive(false); }
            catch (Exception error) { errors.Add(error); }
            try { root.transform.SetParent(null, false); }
            catch (Exception error) { errors.Add(error); }
            try { RimUnity.Object.Destroy(root); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("已禁止归池，但异常弹体销毁未完整完成。", errors);
        }

        internal static void Clear()
        {
            List<Exception> errors = new List<Exception>();
            foreach (var item in new List<LumiVisualItem>(Replacements.Keys))
            {
                try { Restore(item); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException("冰箱弹外观会话清理失败。", errors);
        }

        internal static void ReleaseResources()
        {
            List<Exception> errors = new List<Exception>();
            try { Clear(); }
            catch (Exception error) { errors.Add(error); }
            var material = fridgeMaterial;
            var mesh = fridgeMesh;
            fridgeMaterial = null;
            fridgeMesh = null;
            try { if (material != null) RimUnity.Object.Destroy(material); }
            catch (Exception error) { errors.Add(error); }
            try { if (mesh != null) RimUnity.Object.Destroy(mesh); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("冰箱弹资源清理失败。", errors);
        }

        private static void EnsureResources()
        {
            if (fridgeMaterial == null)
            {
                UnityEngine.Texture2D texture = ContentFinder<UnityEngine.Texture2D>.Get("NSIF/Orbital/FridgeBomb");
                Shader shader = Shader.Find("Cutout");
                if (shader == null) throw new InvalidOperationException("Cutout shader is not registered.");
                fridgeMaterial = new Material(shader)
                {
                    name = "NSIF Fridge Bomb",
                    mainTexture = Texture.FromNative(texture.CoreTexture),
                    color = Color.white
                };
            }
            if (fridgeMesh == null)
            {
                // 平放在弹体根节点，不继承原炮弹零件的组装缩放。
                fridgeMesh = new Mesh
                {
                    name = "NSIF Fridge Bomb Quad",
                    vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                        new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
                    uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f),
                        new Vector2(1f, 1f), new Vector2(1f, 0f) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 }
                };
                fridgeMesh.RecalculateNormals();
                fridgeMesh.RecalculateBounds();
            }
        }

        internal static void Apply(LumiAnimatorItem item)
        {
            if (item == null || !item.HasLiveGameObject || Replacements.ContainsKey(item)) return;
            Replacement replacement = new Replacement();
            // 部分创建失败也走统一还原／隔离路径。
            Replacements.Add(item, replacement);
            try
            {
                EnsureResources();
                GameObject root = item.GameObject;
                // 只隐藏实体 Renderer，不禁用物体/Animator，不碰粒子和拖尾组件。
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true)) replacement.Hide(renderer);
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) replacement.Hide(renderer);
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) replacement.Hide(renderer);
                replacement.Fridge = new GameObject("NSIF_FridgeBombBody");
                replacement.Fridge.transform.SetParent(root.transform, false);
                replacement.Fridge.AddComponent<MeshFilter>().sharedMesh = fridgeMesh;
                MeshRenderer body = replacement.Fridge.AddComponent<MeshRenderer>();
                body.sharedMaterials = new[] { fridgeMaterial };
                body.receiveShadows = false;
                OrbitalSupportHost.Trace("冰箱弹外观已替换；活动替换数=" + Replacements.Count);
            }
            catch (Exception exception)
            {
                try { Restore(item); }
                catch (Exception cleanup) { throw new AggregateException(exception, cleanup); }
                throw;
            }
        }

        internal static void Restore(LumiVisualItem item)
        {
            if (Replacements.Count == 0) return;
            if (!Replacements.TryGetValue(item, out Replacement replacement)) return;
            try { replacement.Restore(); }
            catch (Exception error)
            {
                try { Discard(item); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
            finally { Replacements.Remove(item); }
            OrbitalSupportHost.Trace("弹体回收前已还原外观；活动替换数=" + Replacements.Count);
        }
    }

    [HarmonyPatch]
    internal static class Patch_FridgeBomb_SpawnAnimator
    {
        internal static MethodBase TargetMethod() => AccessTools.Method(typeof(LumiParticleEffects), "SpawnAnimator",
            new[] { typeof(Map), typeof(LumiAnimatorData), typeof(LumiVisualLifetime), typeof(Thing) });

        internal static void Postfix(Thing follow, LumiAnimatorData def, LumiVisualHandle<LumiAnimatorItem> __result)
        {
            if (!OrbitalSupportHost.IsAvailable || !(follow is Skyfaller_FridgeBomb bomb)
                || def?.prefabName != "prefab_orbitalBomb") return;
            try
            {
                bool fridge = bomb.IsFridgeShell;
                if (OrbitalSupportHost.DebugEnabled)
                    OrbitalSupportHost.Trace("弹体=" + bomb.GetUniqueLoadID() + "; action=" + (fridge ? "fridge" : "normal"));
                if (fridge) FridgeBombVisuals.Apply(__result?.Item);
            }
            catch (Exception error) { OrbitalSupportHost.Disable(error); }
        }
    }

    [HarmonyPatch(typeof(LumiVisualItem), nameof(LumiVisualItem.Recycle))]
    internal static class Patch_FridgeBomb_Recycle
    {
        internal static void Prefix(LumiVisualItem __instance)
        {
            try { FridgeBombVisuals.Restore(__instance); }
            catch (Exception error) { OrbitalSupportHost.Disable(error); }
        }
    }
}
