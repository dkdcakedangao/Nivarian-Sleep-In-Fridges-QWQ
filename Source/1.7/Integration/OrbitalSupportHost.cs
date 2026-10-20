using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace NivarianSleepInFridges
{
    // 接缝只使用本模组与游戏类型，主 DLL 不认识 Lumi / RimUnity。
    public interface IOrbitalSupportBackend : IDisposable
    {
        void ClearSession();
    }

    public static class OrbitalSupportHost
    {
        private enum StartupState { Uninitialized, Available, Disabled }
        private static StartupState state;
        private static IOrbitalSupportBackend backend;
        private static string failureReason;

        public static bool IsAvailable => state == StartupState.Available;
        public static bool DebugEnabled => FridgeDebugLog.Enabled;

        internal static Assembly FindLoaded(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == name) return assembly;
            return null;
        }

        internal static void Initialize(ModContentPack content)
        {
            if (state != StartupState.Uninitialized) return;
            state = StartupState.Disabled;
            try
            {
                foreach (string name in new[] { "Nivarian_Race", "LumiParticle", "RimUnity.Core", "RimUnity" })
                    if (FindLoaded(name) == null) throw new InvalidOperationException("本体尚未加载 " + name);

                string path = Path.Combine(content.RootDir, "1.7", "OrbitalSupport", "NivarianSleepInFridges.OrbitalSupport.dll");
                Assembly adapter = Assembly.LoadFrom(path);
                // 提前验证类型可加载；失败程序集不得登记进原版的全类型索引。
                adapter.GetTypes();
                Type entry = adapter.GetType("NivarianSleepInFridges.OrbitalSupport.OrbitalSupportBackend", true);
                MethodInfo create = entry.GetMethod("Create", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                if (create == null) throw new MissingMethodException(entry.FullName, "Create");
                backend = (IOrbitalSupportBackend)create.Invoke(null, null);
                if (backend == null) throw new InvalidOperationException("战备适配入口未返回后端。");
                // Scribe 通过运行模组程序集列表找 Class，而非 AppDomain。
                // 成功接入后登记一次，确保飞行中炮弹能在下次读档找到派生类型。
                if (!content.assemblies.loadedAssemblies.Contains(adapter))
                {
                    content.assemblies.loadedAssemblies.Add(adapter);
                    GenTypes.ClearCache();
                }
                state = StartupState.Available;
            }
            catch (Exception error) { Disable(error); }
        }

        public static void Trace(string message)
        {
            FridgeDebugLog.Message("[OrbitalSupport] " + message);
        }

        public static void Disable(Exception error)
        {
            if (failureReason != null) return;
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            state = StartupState.Disabled;
            failureReason = error.ToString();
            IOrbitalSupportBackend previous = backend;
            backend = null;
            if (previous != null)
            {
                try { previous.Dispose(); }
                catch (Exception cleanup) { failureReason += "\n清理：" + cleanup; }
            }
            Log.Warning("[Nivarian Sleep In Fridges] 冰箱火力网本次启动已停用，不会自动重试；冰箱床等主功能不受影响。\n" + failureReason);
        }

        internal static void ClearSession()
        {
            if (!IsAvailable) return;
            try
            {
                backend.ClearSession();
                Trace("游戏会话清理完成。");
            }
            catch (Exception error) { Disable(error); }
        }

    }

    [HarmonyPatch(typeof(Game), nameof(Game.Dispose))]
    internal static class Patch_OrbitalSupport_GameDispose
    {
        private static void Prefix() { OrbitalSupportHost.ClearSession(); }
    }
}
