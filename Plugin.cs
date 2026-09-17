using BepInEx;
using GorillaLocomotion;
using System;
using UnityEngine;

namespace Argon
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            gameObject.AddComponent<ArgonMenu>().Initialize(Config);
        }

        private void OnDestroy() => MenuApi.Reset();
    }

    public class PluginInfo
    {
        public const string GUID = "zone.xenon.argon";
        public const string Name = "Argon";
        public const string Version = "1.0.0";
    }
}
