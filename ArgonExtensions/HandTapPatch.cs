using HarmonyLib;

namespace ArgonExtensions
{
    [HarmonyPatch(typeof(VRRig), nameof(VRRig.PlayHandTapLocal))]
    internal static class HandTapPatch
    {
        private static bool Prefix(VRRig __instance)
        {
            return !ExtensionMenu.IsHandTapMuted(__instance.Creator);
        }
    }
}
