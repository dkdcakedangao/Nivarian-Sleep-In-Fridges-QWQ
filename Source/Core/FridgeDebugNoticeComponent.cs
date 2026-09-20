using RimWorld;
using Verse;

namespace NivarianSleepInFridges
{
    public sealed class FridgeDebugNoticeComponent : GameComponent
    {
        public FridgeDebugNoticeComponent(Game game)
        {
        }

        public override void StartedNewGame()
        {
            ShowNoticeOnce();
        }

        public override void LoadedGame()
        {
            ShowNoticeOnce();
        }

        private static void ShowNoticeOnce()
        {
            SleepInFridgesSettings settings = SleepInFridgesMod.Settings;
            if (settings == null || !settings.DebugNoticeEnabled || Find.LetterStack == null)
            {
                return;
            }

            settings.DebugNoticeEnabled = false;
            if (SleepInFridgesMod.Instance != null)
            {
                SleepInFridgesMod.Instance.WriteSettings();
            }

            Find.LetterStack.ReceiveLetter(
                "NSIF_DebugNoticeLetterLabel".Translate(),
                "NSIF_DebugNoticeLetterText".Translate(),
                LetterDefOf.NeutralEvent);
        }
    }
}
