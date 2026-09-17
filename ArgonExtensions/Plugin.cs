using BepInEx;
using HarmonyLib;

namespace ArgonExtensions
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInDependency(Argon.PluginInfo.GUID, BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;
        private ExtensionMenu extensionMenu;

        private void Awake()
        {
            extensionMenu = gameObject.AddComponent<ExtensionMenu>();
            extensionMenu.Initialize(Config);
            harmony = new Harmony(PluginInfo.Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            Argon.MenuApi.UnregisterRootItem("Extensions");
            harmony?.UnpatchSelf();
        }
    }

    internal static class PluginInfo
    {
        internal const string Guid = "zone.xenon.argon.extensions";
        internal const string Name = "ArgonExtensions";
        internal const string Version = "1.0.0";
    }
}
