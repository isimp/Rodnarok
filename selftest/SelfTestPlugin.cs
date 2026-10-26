using BepInEx;

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
    public class SelfTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Rodnarok.SelfTest";

        private void Awake() => SelfTestHost.Register(new SelfTestRun());
    }
}
