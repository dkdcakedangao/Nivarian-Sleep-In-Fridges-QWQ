using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace NivarianSleepInFridges
{
    internal struct FridgeSnackingState
    {
        internal Pawn Occupant;
        internal int NextAttemptTick;
    }

    // 偷吃会从当前冰箱选食物~
    internal static class FridgeSnackingUtility
    {
        private const int NoFoodRetryTicks = 1250;

        internal static void TrySnackOccupants(Building_FridgeBedProxy proxy)
        {
            SleepInFridgesSettings settings = SleepInFridgesMod.Settings;
            if (settings == null || !settings.SnackingEnabled || !proxy.IsActive)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            List<Thing> contents = null;
            for (int slot = 0; slot < proxy.SleepingSlotsCount; slot++)
            {
                Pawn pawn = proxy.GetCurOccupant(slot);
                if (proxy.SnackingStates != null && proxy.SnackingStates[slot].Occupant != pawn)
                {
                    proxy.SnackingStates[slot] = new FridgeSnackingState { Occupant = pawn };
                }

                if (!FridgeSleepUtility.IsEligibleSleeper(pawn)
                    || pawn.Dead
                    || pawn.jobs == null
                    || pawn.jobs.curDriver == null
                    || !pawn.jobs.curDriver.asleep
                    || pawn.CurrentBed() != proxy
                    || pawn.needs == null
                    || pawn.needs.food == null
                    || pawn.needs.food.CurLevelPercentage >= settings.SnackingThresholdPercent / 100f)
                {
                    continue;
                }

                if (proxy.SnackingStates == null)
                {
                    proxy.SnackingStates = new FridgeSnackingState[proxy.SleepingSlotsCount];
                    proxy.SnackingStates[slot].Occupant = pawn;
                }

                if (tick < proxy.SnackingStates[slot].NextAttemptTick)
                {
                    continue;
                }

                if (contents == null)
                {
                    contents = FridgeContents(proxy.ParentFridge);
                }

                Thing food = BestFood(pawn, proxy.ParentFridge, contents);
                if (food == null)
                {
                    proxy.SnackingStates[slot].NextAttemptTick = tick + NoFoodRetryTicks;
                    continue;
                }

                // 除错
                string foodLabel = food.LabelNoCount;
                float nutrition = food.Ingested(pawn, pawn.needs.food.NutritionWanted);
                if (!pawn.Dead && pawn.needs.food != null)
                {
                    pawn.needs.food.CurLevel += nutrition;
                    pawn.records.AddTo(RecordDefOf.NutritionEaten, nutrition);
                }

                if (nutrition > 0f)
                {
                    Messages.Message(new FridgeSnackingMessage(
                        "NSIF_SnackingMessage".Translate(foodLabel, pawn.LabelShort), pawn), false);
                }
            }
        }

        private static List<Thing> FridgeContents(ThingWithComps fridge)
        {
            List<Thing> contents = new List<Thing>();
            HashSet<Thing> seen = new HashSet<Thing>();
            Building_Storage storage = fridge as Building_Storage;
            SlotGroup slots = storage == null ? null : storage.GetSlotGroup();
            if (slots != null)
            {
                foreach (Thing thing in slots.HeldThings)
                {
                    if (seen.Add(thing))
                    {
                        contents.Add(thing);
                    }
                }
            }

            List<Thing> held = new List<Thing>();
            AddHolderContents(fridge as IThingHolder, fridge, contents, seen, held);
            List<ThingComp> comps = fridge.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                AddHolderContents(comps[i] as IThingHolder, fridge, contents, seen, held);
            }

            return contents;
        }

        private static void AddHolderContents(IThingHolder holder, ThingWithComps fridge,
            List<Thing> contents, HashSet<Thing> seen, List<Thing> held)
        {
            if (holder == null)
            {
                return;
            }

            ThingOwnerUtility.GetAllThingsRecursively(holder, held, false,
                delegate(IThingHolder child) { return IsStorageHolder(child, fridge); });
            for (int i = 0; i < held.Count; i++)
            {
                if (seen.Add(held[i]))
                {
                    contents.Add(held[i]);
                }
            }
        }

        private static bool IsStorageHolder(IThingHolder holder, ThingWithComps fridge)
        {

            // 限定容器范围
            for (IThingHolder current = holder; current != null; current = current.ParentHolder)
            {
                Thing thing = current as Thing;
                if (thing != null)
                {
                    return thing == fridge;
                }

                ThingComp comp = current as ThingComp;
                if (comp != null)
                {
                    return comp.parent == fridge;
                }
            }

            return false;
        }

        // 食物选择！石山的核心！
        private static Thing BestFood(Pawn pawn, ThingWithComps fridge, List<Thing> contents)
        {
            Thing best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < contents.Count; i++)
            {
                Thing food = contents[i];
                if (!CanEat(pawn, fridge, food))
                {
                    continue;
                }

                float score = FoodUtility.FoodOptimality(pawn, food, food.def, 0f);
                if (score > bestScore)
                {
                    best = food;
                    bestScore = score;
                }
            }

            return best;
        }

        private static bool CanEat(Pawn pawn, ThingWithComps fridge, Thing food)
        {
            if (food == null || food.Destroyed || food.stackCount <= 0
                || !food.IngestibleNow || !food.def.IsNutritionGivingIngestible
                || food.def.IsCorpse || food.def.IsDrug || food.def.IsRawHumanFood()
                || food.IsNotFresh() || food.IsForbidden(pawn)
                || FridgeMoodUtility.FridgeHolding(food) != fridge
                || !pawn.WillEat(food, pawn))
            {
                return false;
            }

            float nutrition = FoodUtility.NutritionForEater(pawn, food);
            if (nutrition < SleepInFridgesMod.Settings.SnackingMinimumNutrition)
            {
                return false;
            }

            int count = Mathf.Min(food.stackCount, FoodUtility.WillIngestStackCountOf(pawn, food.def, nutrition));
            if (!pawn.Map.reservationManager.CanReserve(pawn, food, 10, count))
            {
                return false;
            }

            // 原版进食思想 检查
            List<FoodUtility.ThoughtFromIngesting> thoughts = FoodUtility.ThoughtsFromIngesting(pawn, food, food.def);
            for (int i = 0; i < thoughts.Count; i++)
            {
                // 文化、特质等屏蔽的负面思想，不会使得餐食被误判为“不喜欢”~我就是喜欢吃虫胶！（hso）
                if (thoughts[i].thought.stages[0].baseMoodEffect < 0f
                    && ThoughtUtility.CanGetThought(pawn, thoughts[i].thought, checkIfNullified: true))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
