using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace NivarianSleepInFridges
{
    // 可选装饰接口各自绑定一次；不使主程序集引用本体或框架。
    internal static class NivarianHostBridge
    {
        private static Func<Rect, string, TextAnchor, bool, bool> button;
        private static Action<Rect, bool> background;
        private static bool initialized;

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Type helper;
            try { helper = OrbitalSupportHost.FindLoaded("Nivarian_Race")?.GetType("Nivarian.Helper.NivarianVisualHelper"); }
            catch (Exception error)
            {
                Log.Warning("[Nivarian Sleep In Fridges] Optional UI helper unavailable: " + error.Message);
                return;
            }
            if (helper == null) return;
            button = Bind<Func<Rect, string, TextAnchor, bool, bool>>(helper, "DrawNivarianTechBtn",
                typeof(Rect), typeof(string), typeof(TextAnchor), typeof(bool));
            background = Bind<Action<Rect, bool>>(helper, "DrawNivarianTechBtnBackground", typeof(Rect), typeof(bool));
        }

        private static T Bind<T>(Type type, string name, params Type[] arguments) where T : Delegate
        {
            try
            {
                return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, arguments, null)?.CreateDelegate<T>();
            }
            catch (Exception error)
            {
                Log.Warning("[Nivarian Sleep In Fridges] Optional UI binding failed: " + name + ": " + error.Message);
                return null;
            }
        }

        internal static void UIWithColor(Color color, Action draw)
        {
            Color previous = GUI.color;
            try { GUI.color = color; draw(); }
            finally { GUI.color = previous; }
        }

        internal static void DrawNivarianTechBtnBackground(Rect rect, bool mouseover)
        {
            try
            {
                if (background != null) { background(rect, mouseover); return; }
            }
            catch (Exception error)
            {
                background = null;
                Log.Warning("[Nivarian Sleep In Fridges] Optional UI background disabled: " + error.Message);
            }
            Widgets.DrawMenuSection(rect);
        }

        internal static bool DrawNivarianTechBtn(Rect rect, string label, TextAnchor anchor, bool active)
        {
            try
            {
                if (button != null) return button(rect, label, anchor, active);
            }
            catch (Exception error)
            {
                button = null;
                Log.Warning("[Nivarian Sleep In Fridges] Optional UI button disabled: " + error.Message);
            }
            return Widgets.ButtonText(rect, label, active: active);
        }
    }
}
