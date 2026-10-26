using BepInEx;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rodnarok
{
    /// <summary>
    /// What BepInEx loads: it gives the log and the settings to the layers below, registers the
    /// command and hands each frame to the self-test where one is installed. On a dedicated server
    /// (no graphics) it binds the settings alone, which ServerSync then sends every player.
    /// </summary>
    [BepInPlugin(About.Guid, About.Name, About.Version)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Log.Source = Logger;
            Settings.Bind(Config);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Commands.Register();
        }

        private void Update() => SelfTestHost.Tick();
    }
}
