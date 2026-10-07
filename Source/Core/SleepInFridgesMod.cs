using System.Reflection;
using HarmonyLib;
using Nivarian.Helper;
using RimWorld;
using UnityEngine;
using Verse;

// 入口 初始化 兼容补丁
namespace NivarianSleepInFridges
{
    public sealed class SleepInFridgesMod : Mod
    {
        private const string HarmonyId = "dkdcakedangao.NivarianSleepInFridgesQWQ";
        private const float SectionWidth = 160f;
        private const float SectionGap = 12f;
        private const float HeaderHeight = 36f;
        private static readonly string[] SectionLabels =
        {
            "NSIF_SettingsPageGeneral",
            "NSIF_SettingsPageMood",
            "NSIF_SettingsPageSnacking",
            "NSIF_SettingsPageDebug"
        };

        private int settingsPage;
        private Vector2 scrollPosition;
        private bool pendingCapacityRefresh;

        internal static SleepInFridgesMod Instance { get; private set; }
        public static SleepInFridgesSettings Settings { get; private set; }

        public SleepInFridgesMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<SleepInFridgesSettings>();
            LongEventHandler.ExecuteWhenFinished(ApplyBedSelectionSetting);
            Harmony harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            AdaptiveStorageBridge.Initialize(harmony);
            IcecreamTailCompatibility.Initialize(harmony);
            Log.Message("Nivarian Sleep In Fridges QWQ loaded.");
        }

        public override string SettingsCategory()
        {
            return LanguageDatabase.activeLanguage != null
                && LanguageDatabase.activeLanguage.folderName.StartsWith("ChineseSimplified")
                ? "冰龙可以睡！冰！箱！"
                : "Nivarian Sleep In Fridges QWQ";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (pendingCapacityRefresh)
            {
                pendingCapacityRefresh = false;
                FridgeSleepUtility.RefreshAllMaps();
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect sectionRect = new Rect(inRect.x, inRect.y, SectionWidth, inRect.height);
            Rect titleRect = new Rect(
                inRect.x + SectionWidth + SectionGap,
                inRect.y,
                inRect.width - SectionWidth - SectionGap,
                HeaderHeight);
            Rect contentViewRect = new Rect(
                inRect.x + SectionWidth + SectionGap,
                inRect.y + HeaderHeight,
                inRect.width - SectionWidth - SectionGap,
                inRect.height - HeaderHeight);

            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, SectionLabels[settingsPage].Translate());
            Text.Font = GameFont.Small;

            DrawSectionList(sectionRect);

            float contentHeight = Mathf.Max(contentViewRect.height, SettingsContentHeight());
            Rect contentRect = new Rect(0f, 0f, contentViewRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(contentViewRect, ref scrollPosition, contentRect, true);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(contentRect);

            bool oldValue = Settings.Enabled;
            bool oldAllowUfGarbageBinValue = Settings.AllowUfGarbageBin;
            bool oldIcyCoreRecoveryValue = Settings.IcyCoreRecoveryEnabled;
            bool oldIcecreamTailRecoveryBoostValue = Settings.IcecreamTailRecoveryBoostEnabled;
            float oldIcecreamTailRecoveryMultiplier = Settings.IcecreamTailRecoveryMultiplier;
            bool oldCapacityReductionValue = Settings.CapacityReductionEnabled;
            int oldSingleBedMaximumReduction = Settings.SingleBedMaximumReduction;
            int oldDoubleBedMaximumReduction = Settings.DoubleBedMaximumReduction;
            bool oldSleepMoodValue = Settings.SleepMoodEnabled;
            bool oldInteractionMoodValue = Settings.InteractionMoodEnabled;
            bool oldSnackingValue = Settings.SnackingEnabled;
            int oldSnackingThreshold = Settings.SnackingThresholdPercent;
            float oldSnackingMinimumNutrition = Settings.SnackingMinimumNutrition;
            bool oldDebugLoggingValue = Settings.DebugLoggingEnabled;
            bool oldVerboseCacheLoggingValue = Settings.VerboseCacheLoggingEnabled;
            bool oldDebugNoticeValue = Settings.DebugNoticeEnabled;
            bool oldAllowSelectingFridgeNestValue = Settings.AllowSelectingFridgeNest;
            bool reset = false;
            if (settingsPage == 0)
            {
                listing.CheckboxLabeled(
                    "NSIF_MasterSwitch".Translate(),
                    ref Settings.Enabled,
                    "NSIF_MasterSwitchDesc".Translate());
                listing.CheckboxLabeled(
                    "NSIF_AllowUfGarbageBin".Translate(),
                    ref Settings.AllowUfGarbageBin,
                    "NSIF_AllowUfGarbageBinDesc".Translate());
                listing.CheckboxLabeled(
                    "NSIF_IcyCoreRecovery".Translate(),
                    ref Settings.IcyCoreRecoveryEnabled,
                    "NSIF_IcyCoreRecoveryDesc".Translate());
                listing.CheckboxLabeled(
                    "NSIF_IcecreamTailRecoveryBoost".Translate(),
                    ref Settings.IcecreamTailRecoveryBoostEnabled,
                    "NSIF_IcecreamTailRecoveryBoostDesc".Translate());
                if (Settings.IcecreamTailRecoveryBoostEnabled)
                {
                    listing.Label("NSIF_IcecreamTailRecoveryMultiplier".Translate(
                        Settings.IcecreamTailRecoveryMultiplier.ToString("0.0")));
                    float multiplier = listing.Slider(Settings.IcecreamTailRecoveryMultiplier, 1f, 10f);
                    Settings.IcecreamTailRecoveryMultiplier = Mathf.Round(multiplier * 10f) / 10f;
                }

                listing.CheckboxLabeled(
                    "NSIF_CapacityReduction".Translate(),
                    ref Settings.CapacityReductionEnabled,
                    "NSIF_CapacityReductionDesc".Translate());
                if (Settings.CapacityReductionEnabled)
                {
                    listing.Label("NSIF_SingleBedMaximumReduction".Translate(Settings.SingleBedMaximumReduction));
                    Rect singleBedSliderRect = listing.GetRect(24f);
                    Settings.SingleBedMaximumReduction = Mathf.RoundToInt(Widgets.HorizontalSlider(
                        singleBedSliderRect,
                        Settings.SingleBedMaximumReduction,
                        0f,
                        100f));
                    TooltipHandler.TipRegion(
                        singleBedSliderRect,
                        "NSIF_SingleBedMaximumReductionDesc".Translate());

                    listing.Label("NSIF_DoubleBedMaximumReduction".Translate(Settings.DoubleBedMaximumReduction));
                    Rect doubleBedSliderRect = listing.GetRect(24f);
                    Settings.DoubleBedMaximumReduction = Mathf.RoundToInt(Widgets.HorizontalSlider(
                        doubleBedSliderRect,
                        Settings.DoubleBedMaximumReduction,
                        0f,
                        100f));
                    TooltipHandler.TipRegion(
                        doubleBedSliderRect,
                        "NSIF_DoubleBedMaximumReductionDesc".Translate());
                }

                listing.Gap(8f);
                reset = Widgets.ButtonText(listing.GetRect(32f), "NSIF_ResetDefaults".Translate());
            }
            else if (settingsPage == 1)
            {
                listing.CheckboxLabeled(
                    "NSIF_SleepMood".Translate(),
                    ref Settings.SleepMoodEnabled,
                    "NSIF_SleepMoodDesc".Translate());
                listing.CheckboxLabeled(
                    "NSIF_InteractionMood".Translate(),
                    ref Settings.InteractionMoodEnabled,
                    "NSIF_InteractionMoodDesc".Translate());
            }
            else if (settingsPage == 2)
            {
                listing.CheckboxLabeled(
                    "NSIF_Snacking".Translate(),
                    ref Settings.SnackingEnabled,
                    "NSIF_SnackingDesc".Translate());
                if (Settings.SnackingEnabled)
                {
                    listing.Label("NSIF_SnackingThreshold".Translate(Settings.SnackingThresholdPercent));
                    Rect sliderRect = listing.GetRect(24f);
                    Settings.SnackingThresholdPercent = Mathf.RoundToInt(Widgets.HorizontalSlider(
                        sliderRect, Settings.SnackingThresholdPercent, 0f, 100f) / 5f) * 5;
                    TooltipHandler.TipRegion(sliderRect, "NSIF_SnackingThresholdDesc".Translate());

                    listing.Label("NSIF_SnackingMinimumNutrition".Translate(
                        Settings.SnackingMinimumNutrition.ToString("0.00")));
                    Rect nutritionSliderRect = listing.GetRect(24f);
                    Settings.SnackingMinimumNutrition = Mathf.RoundToInt(Widgets.HorizontalSlider(
                        nutritionSliderRect, Settings.SnackingMinimumNutrition, 0f, 1f) * 20f) / 20f;
                    TooltipHandler.TipRegion(nutritionSliderRect, "NSIF_SnackingMinimumNutritionDesc".Translate());
                }
            }
            else
            {
                listing.CheckboxLabeled(
                    "NSIF_DebugLogging".Translate(),
                    ref Settings.DebugLoggingEnabled,
                    "NSIF_DebugLoggingDesc".Translate());
                listing.CheckboxLabeled(
                    "NSIF_DebugNotice".Translate(),
                    ref Settings.DebugNoticeEnabled,
                    "NSIF_DebugNoticeDesc".Translate());
                listing.CheckboxLabeled(
                    "NSIF_AllowSelectingFridgeNest".Translate(),
                    ref Settings.AllowSelectingFridgeNest,
                    "NSIF_AllowSelectingFridgeNestDesc".Translate());
                if (Settings.DebugLoggingEnabled)
                {
                    listing.CheckboxLabeled(
                        "NSIF_VerboseCacheLogging".Translate(),
                        ref Settings.VerboseCacheLoggingEnabled,
                        "NSIF_VerboseCacheLoggingDesc".Translate());
                }

                listing.Gap(12f);
                Rect repairButtonRect = listing.GetRect(32f);
                bool canRepair = Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null;
                if (Widgets.ButtonText(
                    repairButtonRect,
                    "NSIF_QuickRepair".Translate(),
                    true,
                    true,
                    canRepair))
                {
                    FridgeRepairReport report = FridgeRepairUtility.RepairCurrentMap();
                    Messages.Message(
                        "NSIF_QuickRepairResult".Translate(
                            report.ScannedStorages,
                            report.NewlyProcessedDefs,
                            report.FridgeStorages,
                            report.RepairedInstances,
                            report.Failures),
                        MessageTypeDefOf.TaskCompletion,
                        false);
                }

                TooltipHandler.TipRegion(
                    repairButtonRect,
                    canRepair
                        ? "NSIF_QuickRepairDesc".Translate()
                        : "NSIF_QuickRepairUnavailable".Translate());
            }

            listing.End();
            Widgets.EndScrollView();

            if (reset)
            {
                Settings.ResetToDefaults();
            }

            if (reset || oldAllowSelectingFridgeNestValue != Settings.AllowSelectingFridgeNest)
            {
                ApplyBedSelectionSetting();
            }

            bool capacitySliderChanged = oldSingleBedMaximumReduction != Settings.SingleBedMaximumReduction
                || oldDoubleBedMaximumReduction != Settings.DoubleBedMaximumReduction;
            bool immediateCapacityRefresh = reset
                || oldValue != Settings.Enabled
                || oldAllowUfGarbageBinValue != Settings.AllowUfGarbageBin
                || oldCapacityReductionValue != Settings.CapacityReductionEnabled;
            bool otherSettingChanged = oldIcyCoreRecoveryValue != Settings.IcyCoreRecoveryEnabled
                || oldIcecreamTailRecoveryBoostValue != Settings.IcecreamTailRecoveryBoostEnabled
                || !Mathf.Approximately(oldIcecreamTailRecoveryMultiplier, Settings.IcecreamTailRecoveryMultiplier)
                || oldSleepMoodValue != Settings.SleepMoodEnabled
                || oldInteractionMoodValue != Settings.InteractionMoodEnabled
                || oldSnackingValue != Settings.SnackingEnabled
                || oldSnackingThreshold != Settings.SnackingThresholdPercent
                || !Mathf.Approximately(oldSnackingMinimumNutrition, Settings.SnackingMinimumNutrition)
                || oldDebugLoggingValue != Settings.DebugLoggingEnabled
                || oldVerboseCacheLoggingValue != Settings.VerboseCacheLoggingEnabled
                || oldDebugNoticeValue != Settings.DebugNoticeEnabled
                || oldAllowSelectingFridgeNestValue != Settings.AllowSelectingFridgeNest;

            if (immediateCapacityRefresh)
            {
                pendingCapacityRefresh = false;
                WriteSettings();
                FridgeSleepUtility.RefreshAllMaps();
            }
            else
            {
                if (capacitySliderChanged)
                {
                    pendingCapacityRefresh = true;
                }

                if (otherSettingChanged)
                {
                    WriteSettings();
                }

                if (pendingCapacityRefresh && GUIUtility.hotControl == 0)
                {
                    WriteSettings();
                }
            }
        }

        private static void ApplyBedSelectionSetting()
        {
            // 可选择小窝建筑的相关除错~孩子们！debug来咯~
            NSIF_DefOf.NSIF_FridgeBedProxySingleCell.selectable = Settings.AllowSelectingFridgeNest;
            NSIF_DefOf.NSIF_FridgeBedProxySingle.selectable = Settings.AllowSelectingFridgeNest;
            NSIF_DefOf.NSIF_FridgeBedProxyDouble.selectable = Settings.AllowSelectingFridgeNest;

            if (!Settings.AllowSelectingFridgeNest && Current.ProgramState == ProgramState.Playing)
            {
                var selected = Find.Selector.SelectedObjects;
                for (int i = selected.Count - 1; i >= 0; i--)
                {
                    if (selected[i] is Building_FridgeBedProxy)
                    {
                        Find.Selector.Deselect(selected[i]);
                    }
                }
            }
        }

        private void DrawSectionList(Rect sectionRect)
        {
            Listing_Standard sectionListing = new Listing_Standard();
            sectionListing.Begin(sectionRect);
            for (int page = 0; page < SectionLabels.Length; page++)
            {
                Rect rect = sectionListing.GetRect(30f, 1f);
                string label = SectionLabels[page].Translate();
                if (settingsPage == page)
                {
                    NivarianVisualHelper.UIWithColor(
                        Color.yellow,
                        delegate { NivarianVisualHelper.DrawNivarianTechBtnBackground(rect, false); });
                    Widgets.ButtonText(rect, label, false, true, true, TextAnchor.MiddleCenter);
                }
                else if (NivarianVisualHelper.DrawNivarianTechBtn(rect, label, TextAnchor.MiddleCenter, true))
                {
                    settingsPage = page;
                    scrollPosition = Vector2.zero;
                }

                sectionListing.Gap(4f);
            }

            sectionListing.End();
        }

        private float SettingsContentHeight()
        {
            if (settingsPage == 0)
            {
                return 410f;
            }

            return settingsPage == 1 ? 100f : settingsPage == 2 ? 180f : 240f;
        }
    }

    // mod选项
    // 定义默认值
    // 话说回来，这种复制自己之前的代码的感觉，好爽！
    public sealed class SleepInFridgesSettings : ModSettings
    {
        public bool Enabled = true;
        public bool AllowUfGarbageBin = true;
        public bool IcyCoreRecoveryEnabled = true;
        public bool IcecreamTailRecoveryBoostEnabled = true;
        public float IcecreamTailRecoveryMultiplier = 2f;
        public bool CapacityReductionEnabled = true;
        public int SingleBedMaximumReduction = 10;
        public int DoubleBedMaximumReduction = 20;
        public bool SleepMoodEnabled = true;
        public bool InteractionMoodEnabled = true;
        public bool SnackingEnabled = true;
        public int SnackingThresholdPercent = 30;
        public float SnackingMinimumNutrition = 0.3f;
        public bool DebugLoggingEnabled;
        public bool VerboseCacheLoggingEnabled;
        public bool DebugNoticeEnabled = true;
        public bool AllowSelectingFridgeNest;

        public void ResetToDefaults()
        {
            Enabled = true;
            AllowUfGarbageBin = true;
            IcyCoreRecoveryEnabled = true;
            IcecreamTailRecoveryBoostEnabled = true;
            IcecreamTailRecoveryMultiplier = 2f;
            CapacityReductionEnabled = true;
            SingleBedMaximumReduction = 10;
            DoubleBedMaximumReduction = 20;
            SleepMoodEnabled = true;
            InteractionMoodEnabled = true;
            SnackingEnabled = true;
            SnackingThresholdPercent = 30;
            SnackingMinimumNutrition = 0.3f;
            DebugLoggingEnabled = false;
            VerboseCacheLoggingEnabled = false;
            DebugNoticeEnabled = true;
            AllowSelectingFridgeNest = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref AllowUfGarbageBin, "allowUfGarbageBin", true);
            Scribe_Values.Look(ref IcyCoreRecoveryEnabled, "icyCoreRecoveryEnabled", true);
            Scribe_Values.Look(ref IcecreamTailRecoveryBoostEnabled, "icecreamTailRecoveryBoostEnabled", true);
            Scribe_Values.Look(ref IcecreamTailRecoveryMultiplier, "icecreamTailRecoveryMultiplier", 2f);
            Scribe_Values.Look(ref CapacityReductionEnabled, "capacityReductionEnabled", true);
            Scribe_Values.Look(ref SingleBedMaximumReduction, "singleBedMaximumReduction", 10);
            Scribe_Values.Look(ref DoubleBedMaximumReduction, "doubleBedMaximumReduction", 20);
            Scribe_Values.Look(ref SleepMoodEnabled, "sleepMoodEnabled", true);
            Scribe_Values.Look(ref InteractionMoodEnabled, "interactionMoodEnabled", true);
            Scribe_Values.Look(ref SnackingEnabled, "snackingEnabled", true);
            Scribe_Values.Look(ref SnackingThresholdPercent, "snackingThresholdPercent", 30);
            SnackingThresholdPercent = Mathf.RoundToInt(Mathf.Clamp(SnackingThresholdPercent, 0, 100) / 5f) * 5;
            Scribe_Values.Look(ref SnackingMinimumNutrition, "snackingMinimumNutrition", 0.3f);
            SnackingMinimumNutrition = Mathf.RoundToInt(Mathf.Clamp(SnackingMinimumNutrition, 0f, 1f) * 20f) / 20f;
            Scribe_Values.Look(ref DebugLoggingEnabled, "debugLoggingEnabled", false);
            Scribe_Values.Look(ref VerboseCacheLoggingEnabled, "verboseCacheLoggingEnabled", false);
            Scribe_Values.Look(ref DebugNoticeEnabled, "debugNoticeEnabled", true);
            Scribe_Values.Look(ref AllowSelectingFridgeNest, "allowSelectingFridgeNest", false);
            base.ExposeData();
        }
    }
}
