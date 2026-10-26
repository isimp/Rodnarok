using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A hooked fish shows its struggle at the surface. The game already wiggles a struggling fish
    /// and now and then splashes with it, mostly unseen under the water. While it struggles, the
    /// game that owns it pushes it up to just under the surface, and every game draws its splash
    /// about three times a second; while it rests it sinks back and splashes no more than the game
    /// has it. The struggle is read from the fish's saved state, as the game reads it, so every
    /// player sees it.
    /// </summary>
    internal static class Thrash
    {
        /// <summary>How far under the surface a struggling fish is held, and how hard it is pushed up.</summary>
        private const float UnderSurface = 0.3f;
        private const float PushUp = 15f;

        /// <summary>Seconds between a struggling fish's splashes, varied a little.</summary>
        private const float SplashEveryMin = 0.25f;
        private const float SplashEveryMax = 0.45f;

        private static readonly AccessTools.FieldRef<Fish, float> WaterLevel = AccessTools.FieldRefAccess<Fish, float>("m_inWater");
        private static readonly AccessTools.FieldRef<Fish, Rigidbody> Body = AccessTools.FieldRefAccess<Fish, Rigidbody>("m_body");
        private static readonly AccessTools.FieldRef<Fish, ZNetView> View = AccessTools.FieldRefAccess<Fish, ZNetView>("m_nview");

        private static readonly Dictionary<Fish, float> NextSplash = new Dictionary<Fish, float>();

        /// <summary>How many splashes this game drew for struggling fish, for the self-test.</summary>
        [ForTest]
        public static int SplashesForTest { get; private set; }

        /// <summary>After the fish's step: while it struggles on a line, it splashes, and its owner holds it near the surface.</summary>
        public static void Step(Fish fish)
        {
            var view = View(fish);
            if (view == null || !view.IsValid()) return;
            var zdo = view.GetZDO();
            if (zdo.GetInt(ZDOVars.s_hooked) != 1 || zdo.GetFloat(ZDOVars.s_escape) <= 0f || fish.IsOutOfWater())
            {
                NextSplash.Remove(fish);
                return;
            }

            var now = Time.time;
            if (!NextSplash.TryGetValue(fish, out var at) || now >= at)
            {
                if (NextSplash.ContainsKey(fish))
                {
                    fish.m_jumpEffects.Create(fish.transform.position, Quaternion.identity, fish.transform);
                    SplashesForTest++;
                }
                NextSplash[fish] = now + Random.Range(SplashEveryMin, SplashEveryMax);
            }

            if (!view.IsOwner()) return;
            var target = WaterLevel(fish) - UnderSurface;
            if (fish.transform.position.y < target) Body(fish).AddForce(Vector3.up * PushUp, ForceMode.Acceleration);
        }

        /// <summary>Lets go of a fish no longer there.</summary>
        public static void Forget(Fish fish) => NextSplash.Remove(fish);
    }
}
