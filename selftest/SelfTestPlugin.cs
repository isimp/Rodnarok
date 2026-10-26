using BepInEx;
using HarmonyLib;

namespace Rodnarok
{
    /// <summary>
    /// The self-test as a plugin of its own beside Rodnarok: it registers with Rodnarok as it loads
    /// (<see cref="SelfTestHost"/>). Installing it is what turns the self-test on, so a release,
    /// which packages Rodnarok.dll alone, has nothing of it.
    /// </summary>
    [BepInPlugin(Guid, "Rodnarök self-test", About.Version)]
    [BepInDependency(About.Guid)]
    [BepInProcess("valheim.exe")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "Unity ends a plugin with OnDestroy, which unpatches; a MonoBehaviour is never disposed.")]
    public class SelfTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Rodnarok.SelfTest";

        private Harmony _harmony;

        private void Awake()
        {
            // Only what stands in for the player's hands (SelfTestHold); it does nothing unless a scenario asks.
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(SelfTestPlugin).Assembly);
            SelfTestHost.Register(new SelfTestRun());
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
