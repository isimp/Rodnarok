using HarmonyLib;

namespace Rodnarok
{
    /// <summary>The serpent post joins the game's prefabs as the world's scene wakes.</summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class PostInScene
    {
        private static void Postfix() => SerpentPost.Ensure();
    }

    /// <summary>The serpent post joins the hammer as the items wake.</summary>
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class PostOnHammer
    {
        private static void Postfix() => SerpentPost.Ensure();
    }

    /// <summary>A water tile that has read its corners may be calmed.</summary>
    [HarmonyPatch(typeof(WaterVolume), "Start")]
    internal static class TileStarts
    {
        private static void Postfix(WaterVolume __instance) => CalmWaters.TileStarted(__instance);
    }
}
