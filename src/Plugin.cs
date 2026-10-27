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
        private bool _afterAnimation;

        private void Awake()
        {
            Log.Source = Logger;
            Settings.Bind(Config);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            _harmony = new Harmony(About.Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Commands.Register();
            _afterAnimation = AfterAnimation.Hook(AfterTheGamesAnimation);
            if (!_afterAnimation) Log.Warning("Unity's loop has no place after its animation; seated bodies and fishing motions are set in the late scripts instead, and a line or cape may flicker.");
        }

        private void Update()
        {
            Schools.Tick();
            SeatedRodPose.Tick();
            if (ZNetScene.instance == null)
            {
                CalmWaters.Forget();
                CalmStands.Forget();
                Lines.Clear();
                SeatedRodPose.Forget();
                SeatedBodies.Forget();
            }
            else
            {
                CalmStands.Tick();
                CalmWaters.Tick();
            }
            SelfTestHost.Tick();
        }

        private void LateUpdate()
        {
            if (!_afterAnimation) AfterTheGamesAnimation();
        }

        /// <summary>Right after the game's animation: seated bodies keep their seat, then seated arms show the fishing motions.</summary>
        private static void AfterTheGamesAnimation()
        {
            SeatedBodies.Hold();
            SeatedRodPose.Turn();
        }

        private void OnDestroy()
        {
            if (_afterAnimation) AfterAnimation.Unhook();
            _harmony?.UnpatchSelf();
        }
    }
}
