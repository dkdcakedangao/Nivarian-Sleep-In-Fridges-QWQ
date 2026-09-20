using System;
using System.Collections.Generic;
using AlienRace;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

// 睡觉的时候，绘制位置相关的
// 1x1冰箱总是只露出尾巴
// 其他冰箱都是默认在物品所在图层
// 联合部分容器的特别图层处理（待优化）
namespace NivarianSleepInFridges
{
    [HarmonyPatch(typeof(PawnRenderTree), "Draw")]
    internal static class Patch_PawnRenderTree_Draw
    {
        private const string NivarianTailPath = "Nivarian/Race/Tails/NivarianTails";
        private const float BelowFridgeOffset = 0.001f;

        private static void Prefix(
            PawnDrawParms parms,
            List<PawnGraphicDrawRequest> ___drawRequests,
            ref List<PawnGraphicDrawRequest> __state)
        {
            Building_FridgeBedProxy proxy;
            if (parms.Portrait || parms.Cache || !TryGetCustomDrawBed(parms.pawn, out proxy))
            {
                return;
            }

            __state = SimplePool<List<PawnGraphicDrawRequest>>.Get();
            __state.Clear();
            __state.AddRange(___drawRequests);
            ___drawRequests.Clear();

            float drawAltitude = proxy.ParentFridge.DrawPos.y - BelowFridgeOffset;
            for (int i = 0; i < __state.Count; i++)
            {
                PawnGraphicDrawRequest request = __state[i];
                if (proxy.HidesSleeper && !IsNivarianTail(request.node))
                {
                    continue;
                }

                Matrix4x4 matrix = request.preDrawnComputedMatrix;
                matrix.m13 = drawAltitude;
                request.preDrawnComputedMatrix = matrix;
                ___drawRequests.Add(request);
            }
        }

        private static Exception Finalizer(
            Exception __exception,
            List<PawnGraphicDrawRequest> ___drawRequests,
            List<PawnGraphicDrawRequest> __state)
        {
            if (__state == null)
            {
                return __exception;
            }

            ___drawRequests.Clear();
            ___drawRequests.AddRange(__state);
            __state.Clear();
            SimplePool<List<PawnGraphicDrawRequest>>.Return(__state);
            return __exception;
        }

        internal static bool TryGetCustomDrawBed(Pawn pawn, out Building_FridgeBedProxy proxy)
        {
            proxy = null;
            if (pawn == null)
            {
                return false;
            }

            PawnPosture posture = pawn.GetPosture();
            if (posture != PawnPosture.LayingInBed && posture != PawnPosture.LayingInBedFaceUp)
            {
                return false;
            }

            proxy = pawn.CurrentBed() as Building_FridgeBedProxy;
            return proxy != null
                && proxy.ParentFridge != null
                && proxy.ParentFridge.Spawned
                && (proxy.HidesSleeper
                    || proxy.ParentFridge.def.defName == FridgeSleepUtility.UfGarbageBinDefName);
        }

        private static bool IsNivarianTail(PawnRenderNode node)
        {
            AlienPawnRenderNode_BodyAddon bodyAddonNode = node as AlienPawnRenderNode_BodyAddon;
            return bodyAddonNode != null
                && bodyAddonNode.props != null
                && bodyAddonNode.props.addon != null
                && string.Equals(bodyAddonNode.props.addon.path, NivarianTailPath, StringComparison.Ordinal);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_PawnRenderer_ParallelGetPreRenderResults
    {
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            Building_FridgeBedProxy proxy;
            if (Patch_PawnRenderTree_Draw.TryGetCustomDrawBed(___pawn, out proxy))
            {
                disableCache = true;
            }
        }
    }
}
