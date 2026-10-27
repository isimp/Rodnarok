using HarmonyLib;

namespace Rodnarok
{
    /// <summary>A fish's bite with the right bait counts only with the fish at the hook; another one is held back and tried again as the fish swims closer.</summary>
    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Nibble))]
    internal static class BiteAtTheHook
    {
        private static bool Prefix(FishingFloat __instance, Fish fish, bool correctBait) =>
            !correctBait || fish == null || Bites.Counts(__instance, fish);
    }

    /// <summary>A bite the float takes pulls it under.</summary>
    [HarmonyPatch(typeof(FishingFloat), "RPC_Nibble")]
    internal static class BitePullsUnder
    {
        private static void Postfix(FishingFloat __instance, bool correctBait)
        {
            if (correctBait) Bites.Bitten(__instance);
        }
    }

    /// <summary>The hook window, longer than the game's half second.</summary>
    [HarmonyPatch(typeof(FishingFloat), "TryToHook")]
    internal static class HookWindowStretched
    {
        private static void Prefix(FishingFloat __instance) => Bites.StretchWindow(__instance);
    }

    /// <summary>Each step of the float: a bitten float is pulled under, a struggle gives back the line reeled in, and the line's strain follows the fight.</summary>
    [HarmonyPatch(typeof(FishingFloat), "FixedUpdate")]
    internal static class FloatStep
    {
        private static void Prefix(FishingFloat __instance, out float __state) => __state = Bites.LineBefore(__instance);

        private static void Postfix(FishingFloat __instance, float __state)
        {
            Bites.StallLine(__instance, __state);
            Lines.Step(__instance);
            Bites.Pull(__instance);
            Bites.WatchBait(__instance);
        }
    }

    /// <summary>A float whose bait was taken takes no bite, nor one from a fish that missed it before, at the game that owns it.</summary>
    [HarmonyPatch(typeof(FishingFloat), "RPC_Nibble")]
    internal static class BareFloatTakesNoBite
    {
        private static bool Prefix(FishingFloat __instance, ZDOID fishID)
        {
            var fish = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(fishID)?.GetComponent<Fish>() : null;
            return !Bites.Refuses(__instance, fish);
        }
    }

    /// <summary>A line the game breaks is told in the log with how it stood, as Rodnarök's own snaps are.</summary>
    [HarmonyPatch(typeof(FishingFloat), "Message")]
    internal static class BreaksAreTold
    {
        private static void Prefix(FishingFloat __instance, string msg) => Lines.BrokeMessage(__instance, msg);
    }

    /// <summary>A float leaving the world takes its line's strain and its pulls with it.</summary>
    [HarmonyPatch(typeof(FishingFloat), "OnDestroy")]
    internal static class LineForgets
    {
        private static void Postfix(FishingFloat __instance)
        {
            Lines.Forget(__instance);
            Bites.Forget(__instance);
        }
    }

    /// <summary>A struggling fish costs the stall cost to hold, the same for every fish.</summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.GetStaminaUse))]
    internal static class StallCost
    {
        private static bool Prefix(Fish __instance, ref float __result)
        {
            var figure = Bites.StruggleFigure(__instance);
            if (figure == null) return true;
            __result = figure.Value;
            return false;
        }
    }
}
