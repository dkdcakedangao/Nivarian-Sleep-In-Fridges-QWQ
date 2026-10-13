using Verse;

// 运行时的调度，就是原版的race分桶，250tick的计时器
namespace NivarianSleepInFridges
{
    // 仅运行时计时
    public sealed class Thing_FridgeGiftTimer : Thing
    {
        private FridgeGiftComponent owner;

        internal void Initialize(FridgeGiftComponent component)
        {
            owner = component;
        }

        internal void Detach()
        {
            owner = null;
        }

        public override void TickRare()
        {
            if (owner != null) owner.ProcessTimerTick(this);
        }
    }
}
