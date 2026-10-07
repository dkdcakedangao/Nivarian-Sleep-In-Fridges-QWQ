// 游戏依赖替身，仅由 Run-SnackingTests.ps1 编译，不进入发布 DLL。
namespace UnityEngine
{
    public static class Mathf
    {
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static int RoundToInt(float value) { return (int)Math.Round(value); }
        public static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(value, max)); }
        public static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(value, max)); }
    }
}
namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string member) { } }
}
namespace Verse
{
    public class DefOf : Attribute { }
    public static class DefOfHelper { public static void EnsureInitializedInCtor(Type type) { } }
    public class ThingDef
    {
        public string defName = "Food";
        public bool IsNutritionGivingIngestible = true, IsCorpse, IsDrug, Raw;
        public int MaxIngest = 1;
    }
    public class Thing
    {
        public ThingDef def = new ThingDef();
        public bool Destroyed, Spawned = true, IngestibleNow = true, Forbidden, Rotten, Allowed = true;
        public int stackCount = 1, IngestCalls;
        public float Nutrition = 0.9f, Score;
        public string LabelNoCount = "meal";
        public IThingHolder ParentHolder { get; set; }
        public List<RimWorld.FoodUtility.ThoughtFromIngesting> Thoughts = new List<RimWorld.FoodUtility.ThoughtFromIngesting>();
        public RimWorld.Map Map = new RimWorld.Map();
        public virtual void ExposeData() { }
        public virtual IEnumerable<Gizmo> GetGizmos() { yield break; }
        public virtual void DrawGUIOverlay() { }
        public virtual void TickRare() { }
        public void Destroy(DestroyMode mode) { Destroyed = true; }
        public float Ingested(Pawn pawn, float wanted)
        {
            IngestCalls++;
            int count = Math.Min(stackCount, Math.Max(1, (int)Math.Ceiling(wanted / Nutrition)));
            if (def.MaxIngest > 0) count = Math.Min(count, def.MaxIngest);
            stackCount -= count;
            if (stackCount == 0) Destroyed = true;
            pawn.FoodEffects += count;
            return Nutrition * count;
        }
    }
    public class ThingWithComps : Thing
    {
        public List<ThingComp> AllComps = new List<ThingComp>();
        public T GetComp<T>() where T : ThingComp
        {
            foreach (ThingComp comp in AllComps) if (comp is T) return (T)comp;
            return null;
        }
    }
    public class ThingComp { public ThingWithComps parent; }
    public class Pawn : ThingWithComps, IThingHolder
    {
        public bool IsColonist = true, Dead;
        public PawnJobs jobs = new PawnJobs();
        public PawnNeeds needs = new PawnNeeds();
        public PawnRecords records = new PawnRecords();
        public RimWorld.Building_Bed Bed;
        public int FoodEffects;
        public string LabelShort = "Niva";
        public ThingOwner inventory = new ThingOwner();
        public Pawn() { def.defName = "NivarianRace_Pawn"; }
        public ThingOwner GetDirectlyHeldThings() { return inventory; }
        public void GetChildHolders(List<IThingHolder> holders) { }
    }
    public class PawnJobs { public PawnDriver curDriver = new PawnDriver(); }
    public class PawnDriver { public bool asleep = true; }
    public class PawnNeeds { public RimWorld.Need_Food food = new RimWorld.Need_Food(); }
    public class PawnRecords { public float Nutrition; public void AddTo(object def, float value) { Nutrition += value; } }
    public enum DestroyMode { Vanish }
    public class Gizmo { }
    public struct AcceptanceReport
    {
        public static implicit operator AcceptanceReport(string value) { return new AcceptanceReport(); }
    }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_References { public static void Look<T>(ref T value, string key) { } }
    public static class Scribe_Values
    {
        public static Dictionary<string, object> Values = new Dictionary<string, object>();
        public static void Look<T>(ref T value, string key, T defaultValue)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Values[key] = value;
            else if (Scribe.mode == LoadSaveMode.LoadingVars) value = Values.ContainsKey(key) ? (T)Values[key] : defaultValue;
        }
    }
    public class ModSettings { public virtual void ExposeData() { } }
    public static class LongEventHandler { public static void ExecuteWhenFinished(Action action) { } }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame; }
    public interface IThingHolder
    {
        IThingHolder ParentHolder { get; }
        ThingOwner GetDirectlyHeldThings();
        void GetChildHolders(List<IThingHolder> holders);
    }
    public class ThingOwner : List<Thing> { }
    public static class ThingOwnerUtility
    {
        public static void GetAllThingsRecursively(IThingHolder holder, List<Thing> result, bool real, Predicate<IThingHolder> pass)
        {
            result.Clear(); Gather(holder, result, pass);
        }
        private static void Gather(IThingHolder holder, List<Thing> result, Predicate<IThingHolder> pass)
        {
            if (!pass(holder)) return;
            result.AddRange(holder.GetDirectlyHeldThings());
            var children = new List<IThingHolder>(); holder.GetChildHolders(children);
            foreach (var child in children) Gather(child, result, pass);
        }
    }
    public class LookTargets { public Pawn Pawn; public LookTargets(Pawn pawn) { Pawn = pawn; } }
    public class Message
    {
        private float startingTime;
        public static float Now = 100f;
        public string text;
        public LookTargets lookTargets;
        public Message(string text, object def, LookTargets targets) { this.text = text; lookTargets = targets; ResetTimer(); }
        public void ResetTimer()
        {
            startingTime = Now;
            // 模拟 Harmony 事件；单独验证实际 Postfix 的类型隔离及计时逻辑。
            NivarianSleepInFridges.Patch_Message_ResetTimer_Snacking.Postfix(this, ref startingTime);
        }
        public float TimeLeft { get { return 13f - (Now - startingTime); } }
    }
    public static class Messages
    {
        public static List<Message> Live = new List<Message>();
        public static bool Historical;
        public static void Message(Message message, bool historical)
        {
            Historical = historical;
            foreach (Message old in Live)
                if (old.text == message.text && old.lookTargets.Pawn == message.lookTargets.Pawn) { old.ResetTimer(); return; }
            Live.Add(message);
        }
    }
    public static class TestExtensions
    {
        public static string Translate(this string text, params object[] args) { return text + ":" + string.Join(",", args); }
        public static RimWorld.Building_Bed CurrentBed(this Pawn pawn) { return pawn.Bed; }
        public static bool IsRawHumanFood(this ThingDef def) { return def.Raw; }
        public static bool IsNotFresh(this Thing thing) { return thing.Rotten; }
        public static bool IsForbidden(this Thing thing, Pawn pawn) { return thing.Forbidden; }
        public static bool WillEat(this Pawn pawn, Thing thing, Pawn getter) { return thing.Allowed; }
    }
}
namespace RimWorld
{
    public class Building_Bed : ThingWithComps
    {
        public Pawn[] Occupants = new Pawn[1];
        public int BaseTicks;
        public bool Medical;
        public int SleepingSlotsCount { get { return Occupants.Length; } }
        public Pawn GetCurOccupant(int slot) { return Occupants[slot]; }
        public override void TickRare() { BaseTicks++; }
    }
    public class Building_Storage : ThingWithComps
    {
        public SlotGroup Slots = new SlotGroup();
        public SlotGroup GetSlotGroup() { return Slots; }
    }
    public class SlotGroup
    {
        public List<Thing> Items = new List<Thing>();
        public int Reads;
        public IEnumerable<Thing> HeldThings { get { Reads++; return Items; } }
    }
    public class Need_Food
    {
        public float CurLevel = 0.2f;
        public float CurLevelPercentage { get { return CurLevel; } }
        public float NutritionWanted { get { return 1f - CurLevel; } }
    }
    public class Map { public ReservationManager reservationManager = new ReservationManager(); }
    public class ReservationManager
    {
        public Thing Blocked;
        public int CheckedCount;
        public bool CanReserve(Pawn pawn, Thing food, int max, int count) { CheckedCount = count; return food != Blocked; }
    }
    public class ThoughtDef
    {
        public List<ThoughtStage> stages = new List<ThoughtStage> { new ThoughtStage() };
        public bool CanReceive = true, Nullified;
    }
    public class ThoughtStage { public float baseMoodEffect; }
    public static class ThoughtUtility
    {
        public static int Calls;
        public static bool CanGetThought(Pawn pawn, ThoughtDef thought, bool checkIfNullified = false)
        {
            Calls++;
            return thought.CanReceive && (!checkIfNullified || !thought.Nullified);
        }
    }
    public static class RecordDefOf { public static object NutritionEaten = new object(); }
    public static class MessageTypeDefOf { public static object NeutralEvent = new object(); }
    public static class FoodUtility
    {
        public struct ThoughtFromIngesting { public ThoughtDef thought; }
        public static float NutritionForEater(Pawn pawn, Thing food) { return food.Nutrition; }
        public static int WillIngestStackCountOf(Pawn pawn, ThingDef def, float nutrition)
        {
            int count = Math.Max(1, (int)Math.Ceiling(pawn.needs.food.NutritionWanted / nutrition));
            return def.MaxIngest > 0 ? Math.Min(count, def.MaxIngest) : count;
        }
        public static List<ThoughtFromIngesting> ThoughtsFromIngesting(Pawn pawn, Thing food, ThingDef def) { return food.Thoughts; }
        public static float FoodOptimality(Pawn pawn, Thing food, ThingDef def, float distance) { return food.Score; }
    }
    public class CompAssignableToPawn_Bed : ThingComp
    {
        public class Properties { public int maxAssignedPawnsCount; }
        public Properties Props = new Properties();
        public virtual IEnumerable<Pawn> AssigningCandidates { get { yield break; } }
        public virtual void PostSpawnSetup(bool loading) { }
        public virtual AcceptanceReport CanAssignTo(Pawn pawn) { return new AcceptanceReport(); }
        public virtual void TryAssignPawn(Pawn pawn) { }
        public virtual void ForceAddPawn(Pawn pawn) { }
        protected virtual bool ShouldShowAssignmentGizmo() { return true; }
    }
}
namespace NivarianSleepInFridges
{
    internal static class SleepInFridgesMod { internal static SleepInFridgesSettings Settings = new SleepInFridgesSettings(); }
    internal static class FridgeSleepUtility
    {
        internal static bool Enabled { get { return SleepInFridgesMod.Settings.Enabled; } }
        internal static bool IsEligibleSleeper(Pawn pawn) { return pawn != null && pawn.IsColonist && pawn.def.defName == "NivarianRace_Pawn"; }
    }
    public class CompFridgeSleep : ThingComp
    {
        public bool Active = true;
        public bool IsExpectedProxy(Building_FridgeBedProxy proxy) { return true; }
    }
    internal static class FridgeMoodUtility
    {
        internal static int Calls;
        internal static ThingWithComps FridgeHolding(Thing food)
        {
            if (food.ParentHolder != null)
            {
                for (IThingHolder holder = food.ParentHolder; holder != null; holder = holder.ParentHolder)
                {
                    ThingWithComps thing = holder as ThingWithComps;
                    if (thing != null && thing.GetComp<CompFridgeSleep>() != null) return thing;
                    ThingComp comp = holder as ThingComp;
                    if (comp != null && comp.parent.GetComp<CompFridgeSleep>() != null) return comp.parent;
                }
            }
            return null;
        }
        internal static void RefreshSpaceThoughts(Building_FridgeBedProxy proxy) { Calls++; }
    }
    internal static class FridgeIcyCoreUtility { internal static int Calls; internal static void RecoverOccupants(Building_FridgeBedProxy proxy) { Calls++; } }
    internal class TestFridge : Building_Storage, IThingHolder
    {
        internal ThingOwner Held = new ThingOwner();
        internal List<IThingHolder> Children = new List<IThingHolder>();
        public ThingOwner GetDirectlyHeldThings() { return Held; }
        public void GetChildHolders(List<IThingHolder> children) { children.AddRange(Children); }
    }
    internal class TestStorageComp : ThingComp, IThingHolder
    {
        internal ThingOwner Held = new ThingOwner();
        public IThingHolder ParentHolder { get { return parent as IThingHolder; } }
        public ThingOwner GetDirectlyHeldThings() { return Held; }
        public void GetChildHolders(List<IThingHolder> children) { }
    }
    public static class SnackingTests
    {
        private static int checks;
        private static TestFridge fridge;
        private static Building_FridgeBedProxy bed;
        private static Pawn pawn;
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
            checks++;
        }
        private static void Setup(int slots = 1)
        {
            SleepInFridgesMod.Settings = new SleepInFridgesSettings();
            Find.TickManager.TicksGame = 0;
            Messages.Live.Clear(); FridgeMoodUtility.Calls = FridgeIcyCoreUtility.Calls = 0;
            ThoughtUtility.Calls = 0;
            fridge = new TestFridge(); fridge.AllComps.Add(new CompFridgeSleep());
            bed = new Building_FridgeBedProxy { ParentFridge = fridge, Occupants = new Pawn[slots] };
            pawn = new Pawn { Bed = bed }; bed.Occupants[0] = pawn;
        }
        private static Thing Meal(float score = 0f, int count = 1)
        {
            var food = new Thing { ParentHolder = fridge, Score = score, stackCount = count };
            fridge.Slots.Items.Add(food); return food;
        }
        private static void Filter(Action<Thing> edit, string name)
        {
            Setup(); Thing food = Meal(); edit(food); bed.TickRare();
            Check(food.IngestCalls == 0 && pawn.FoodEffects == 0, name);
        }
        private static void IngredientMoodCase(string name, float mood, bool nullified, bool canReceive, bool accepted)
        {
            // 模拟原版思想列表与有效性 API 的返回值，不模拟真实文化/DLC 实现。
            Setup(); Thing food = Meal(); food.LabelNoCount = name; food.Nutrition = 0.3f;
            food.Thoughts.Add(new FoodUtility.ThoughtFromIngesting
            {
                thought = new ThoughtDef
                {
                    stages = new List<ThoughtStage> { new ThoughtStage { baseMoodEffect = mood } },
                    Nullified = nullified,
                    CanReceive = canReceive
                }
            });
            bed.TickRare();
            Check(food.IngestCalls == (accepted ? 1 : 0), name);
            Check(ThoughtUtility.Calls == (mood < 0f ? 1 : 0), name + " checks only negative thoughts");
        }
        public static void Run()
        {
            Setup(2); bed.Occupants[0] = null; bed.TickRare();
            Check(bed.BaseTicks == 1 && FridgeMoodUtility.Calls == 0 && FridgeIcyCoreUtility.Calls == 0 && fridge.Slots.Reads == 0, "empty bed early return");
            Setup(); pawn.jobs.curDriver.asleep = false; Meal(); bed.TickRare();
            Check(fridge.Slots.Reads == 0 && FridgeMoodUtility.Calls == 1, "awake medical rest does not search food");
            Setup(); pawn.needs.food.CurLevel = 0.3f; Meal(); bed.TickRare();
            Check(fridge.Slots.Reads == 0, "threshold strictly below");
            Setup(); Thing meal = Meal(); object job = pawn.jobs.curDriver; bed.TickRare();
            Check(meal.Destroyed && Math.Abs(pawn.needs.food.CurLevel - 1.1f) < 0.001f && pawn.FoodEffects == 1, "ingestion and nutrition");
            Check(pawn.records.Nutrition == 0.9f && pawn.jobs.curDriver == job && pawn.jobs.curDriver.asleep, "record and unchanged sleep job");
            Check(Messages.Live.Count == 1 && !Messages.Historical && Messages.Live[0].lookTargets.Pawn == pawn, "nonhistorical targeted message");
            Check(Messages.Live[0].TimeLeft == 5f, "first message lasts five seconds");
            Message.Now += 2f; Messages.Live[0].ResetTimer();
            Check(Messages.Live[0].TimeLeft == 5f, "repeat timer reset lasts five seconds");
            var ordinary = new Message("ordinary", null, new LookTargets(pawn));
            Check(ordinary.TimeLeft == 13f, "ordinary messages unchanged");
            Filter(t => t.def.Raw = true, "raw food"); Filter(t => t.def.IsCorpse = true, "corpse");
            Filter(t => t.def.IsDrug = true, "drug"); Filter(t => t.Rotten = true, "rotten");
            Filter(t => t.Nutrition = 0.29f, "low nutrition"); Filter(t => t.Forbidden = true, "forbidden");
            Filter(t => t.Allowed = false, "food policy"); Filter(t => t.IngestibleNow = false, "not ingestible");
            Filter(t => t.Thoughts.Add(new FoodUtility.ThoughtFromIngesting { thought = new ThoughtDef { stages = new List<ThoughtStage> { new ThoughtStage { baseMoodEffect = -1f } } } }), "negative ingredient mood");
            IngredientMoodCase("human ingredient nullified negative", -5f, true, true, true);
            IngredientMoodCase("human ingredient forbidden thought", -5f, false, false, true);
            IngredientMoodCase("human ingredient accepted culture", 0f, false, true, true);
            IngredientMoodCase("human ingredient preferred culture", 2f, false, true, true);
            IngredientMoodCase("human ingredient disliked culture", -5f, false, true, false);
            IngredientMoodCase("insect ingredient nullified negative", -3f, true, true, true);
            IngredientMoodCase("insect ingredient loved culture", 6f, false, true, true);
            IngredientMoodCase("insect ingredient disliked culture", -3f, false, true, false);
            IngredientMoodCase("insect jelly ingredient nullified negative", -3f, true, true, true);
            IngredientMoodCase("insect jelly ingredient accepted", 0f, false, true, true);
            IngredientMoodCase("insect jelly ingredient disliked", -3f, false, true, false);
            Filter(t => pawn.Map.reservationManager.Blocked = t, "reservation");
            Setup(); meal = Meal(); meal.Nutrition = 0.3f; bed.TickRare(); Check(meal.IngestCalls == 1, "0.30 nutrition boundary accepted");
            Setup(); meal = Meal(); meal.Nutrition = 0.31f; bed.TickRare(); Check(meal.IngestCalls == 1, "nutrition above boundary accepted");
            for (int step = 0; step <= 20; step++)
            {
                Setup(); SleepInFridgesMod.Settings.SnackingMinimumNutrition = step / 20f;
                meal = Meal(); meal.Nutrition = step == 0 ? 0.05f : step / 20f;
                bed.TickRare(); Check(meal.IngestCalls == 1, "nutrition slider inclusive boundary " + step);
            }
            Setup(); Thing low = Meal(1f); Thing high = Meal(2f); bed.TickRare();
            Check(low.IngestCalls == 0 && high.IngestCalls == 1, "original food score used");
            Setup(); Thing first = Meal(1f); Thing second = Meal(1f); bed.TickRare();
            Check(first.IngestCalls == 1 && second.IngestCalls == 0, "equal scores retain first");
            Setup(); bed.TickRare(); int reads = fridge.Slots.Reads;
            for (int tick = 250; tick < 1250; tick += 250) { Find.TickManager.TicksGame = tick; bed.TickRare(); }
            Check(fridge.Slots.Reads == reads, "no-food retry skips four rare ticks");
            Find.TickManager.TicksGame = 1250; bed.TickRare(); Check(fridge.Slots.Reads == reads + 1, "retry at 1250 ticks");
            pawn = new Pawn { Bed = bed }; bed.Occupants[0] = pawn; Meal(); bed.TickRare();
            Check(pawn.FoodEffects == 1, "new occupant resets retry");
            Setup(2); Pawn other = new Pawn { Bed = bed }; bed.Occupants[1] = other; meal = Meal(0f, 2); bed.TickRare();
            Check(pawn.FoodEffects == 1 && other.FoodEffects == 1 && meal.Destroyed && fridge.Slots.Reads == 1, "double bed sequential shared snapshot");
            Setup(2); other = new Pawn { Bed = bed }; bed.Occupants[1] = other; meal = Meal(); bed.TickRare();
            Check(pawn.FoodEffects == 1 && other.FoodEffects == 0 && meal.IngestCalls == 1, "double bed cannot consume destroyed item twice");
            Setup(); meal = Meal(); fridge.Held.Add(meal); bed.TickRare(); Check(meal.IngestCalls == 1, "slot and holder deduplicated");
            Setup(); meal = new Thing { ParentHolder = fridge }; fridge.Held.Add(meal); bed.TickRare(); Check(meal.IngestCalls == 1, "direct container");
            Setup(); var comp = new TestStorageComp { parent = fridge }; fridge.AllComps.Add(comp);
            meal = new Thing { ParentHolder = comp }; comp.Held.Add(meal); bed.TickRare(); Check(meal.IngestCalls == 1, "component-held container");
            Setup(); var guest = new Pawn { ParentHolder = fridge }; fridge.Held.Add(guest); fridge.Children.Add(guest);
            meal = new Thing { ParentHolder = guest }; guest.inventory.Add(meal); bed.TickRare(); Check(meal.IngestCalls == 0, "no pawn inventory traversal");
            Setup(); meal = Meal(); SleepInFridgesMod.Settings.SnackingEnabled = false; bed.TickRare(); Check(fridge.Slots.Reads == 0, "snacking off");
            Setup(); Meal(); SleepInFridgesMod.Settings.Enabled = false; bed.TickRare(); Check(fridge.Slots.Reads == 0, "master off");
            Setup(); Meal(); fridge.GetComp<CompFridgeSleep>().Active = false; bed.TickRare(); Check(fridge.Slots.Reads == 0, "per-fridge off");
            Setup(); Meal(); pawn.IsColonist = false; bed.TickRare(); Check(fridge.Slots.Reads == 0, "noncolonist rejected");
            Setup(); Meal(); pawn.def.defName = "Human"; bed.TickRare(); Check(fridge.Slots.Reads == 0, "non-Nivarian rejected");
            Setup(); Meal(); SleepInFridgesMod.Settings.SnackingThresholdPercent = 0; bed.TickRare(); Check(fridge.Slots.Reads == 0, "zero threshold");
            var settings = new SleepInFridgesSettings(); Check(settings.SnackingEnabled && settings.SnackingThresholdPercent == 30 && settings.SnackingMinimumNutrition == 0.3f, "defaults");
            settings.SnackingEnabled = false; settings.SnackingThresholdPercent = 65; settings.SnackingMinimumNutrition = 0.65f;
            Scribe.mode = LoadSaveMode.Saving; settings.ExposeData();
            settings = new SleepInFridgesSettings(); Scribe.mode = LoadSaveMode.LoadingVars; settings.ExposeData();
            Check(!settings.SnackingEnabled && settings.SnackingThresholdPercent == 65 && settings.SnackingMinimumNutrition == 0.65f, "save and load");
            Scribe_Values.Values["snackingThresholdPercent"] = 103; settings.ExposeData(); Check(settings.SnackingThresholdPercent == 100, "loaded upper clamp");
            Scribe_Values.Values["snackingThresholdPercent"] = -3; settings.ExposeData(); Check(settings.SnackingThresholdPercent == 0, "loaded lower clamp");
            Scribe_Values.Values["snackingThresholdPercent"] = 32; settings.ExposeData(); Check(settings.SnackingThresholdPercent == 30, "loaded five-percent snap");
            Scribe_Values.Values["snackingMinimumNutrition"] = 1.2f; settings.ExposeData(); Check(settings.SnackingMinimumNutrition == 1f, "loaded nutrition upper clamp");
            Scribe_Values.Values["snackingMinimumNutrition"] = -0.1f; settings.ExposeData(); Check(settings.SnackingMinimumNutrition == 0f, "loaded nutrition lower clamp");
            Scribe_Values.Values["snackingMinimumNutrition"] = 0.32f; settings.ExposeData(); Check(settings.SnackingMinimumNutrition == 0.3f, "loaded nutrition snap");
            for (int step = 0; step <= 20; step++)
            {
                float expected = step / 20f;
                Scribe_Values.Values["snackingMinimumNutrition"] = expected; settings.ExposeData();
                Check(settings.SnackingMinimumNutrition == expected, "loaded nutrition step unchanged " + step);
            }
            Scribe_Values.Values.Clear(); settings.ExposeData(); Check(settings.SnackingEnabled && settings.SnackingThresholdPercent == 30 && settings.SnackingMinimumNutrition == 0.3f, "missing settings defaults");
            settings.SnackingEnabled = false; settings.SnackingThresholdPercent = 75; settings.SnackingMinimumNutrition = 0.75f; settings.ResetToDefaults();
            Check(settings.SnackingEnabled && settings.SnackingThresholdPercent == 30 && settings.SnackingMinimumNutrition == 0.3f, "reset defaults");
            Console.WriteLine("PASS: " + checks + " isolated checks (test doubles, not in-game tests).");
        }
    }
}
