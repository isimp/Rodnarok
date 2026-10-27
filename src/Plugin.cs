using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rodnarok
{
    /// <summary>
    /// What BepInEx loads: it gives the log and the settings to the layers below, patches the
    /// game's fish, registers the command and hands each frame to the schools and to the
    /// self-test where one is installed. On a dedicated server (no graphics) it binds the
    /// settings alone, which ServerSync then sends every player.
    /// </summary>
    [BepInPlugin(About.Guid, About.Name, About.Version)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "Unity ends a plugin with OnDestroy, which unpatches; a MonoBehaviour is never disposed.")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        private void Awake()
        {
            Log.Source = Logger;
            Settings.Bind(Config);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            _harmony = new Harmony(About.Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Commands.Register();
        }

        private void Update()
        {
            Schools.Tick();
            BaitWatch.Tick();
            if (ZNetScene.instance == null)
            {
                CalmWaters.Forget();
                Lines.Clear();
            }
            else
            {
                CalmWaters.Tick();
            }
            SelfTestHost.Tick();
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
