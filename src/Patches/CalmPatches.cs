using HarmonyLib;

namespace Rodnarok
{
    /// <summary>A stand waking answers the call to switch its calm and is looked over from then on.</summary>
    [HarmonyPatch(typeof(ItemStand), "Awake")]
    internal static class StandWakes
    {
        private static void Postfix(ItemStand __instance) => CalmStands.Woke(__instance);
    }

    /// <summary>A tap of the use key on a stand holding the Serpent trophy switches its calm.</summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact))]
    internal static class StandSwitchesCalm
    {
        private static bool Prefix(ItemStand __instance, Humanoid user, bool hold, bool alt, ref bool __result) =>
            CalmStands.Interact(__instance, user, hold, alt, ref __result);
    }

    /// <summary>A stand holding the Serpent trophy tells whether it calms the waters and how to switch it.</summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
    internal static class StandTellsCalm
    {
        private static void Postfix(ItemStand __instance, ref string __result) => __result = CalmStands.Hover(__instance, __result);
    }

    /// <summary>The trophy taken off a stand switches its calm off.</summary>
    [HarmonyPatch(typeof(ItemStand), "DropItem")]
    internal static class StandDropsTrophy
    {
        private static void Postfix(ItemStand __instance) => CalmStands.Taken(__instance);
    }

    /// <summary>The trophy taken away from a stand switches its calm off.</summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.RPC_DestroyAttachment))]
    internal static class StandLosesTrophy
    {
        private static void Postfix(ItemStand __instance) => CalmStands.Taken(__instance);
    }

    /// <summary>A water tile that has read its corners may be calmed.</summary>
    [HarmonyPatch(typeof(WaterVolume), "Start")]
    internal static class TileStarts
    {
        private static void Postfix(WaterVolume __instance) => CalmWaters.TileStarted(__instance);
    }
}
