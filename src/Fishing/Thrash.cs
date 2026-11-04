using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A hooked fish shows its struggle at the surface. The game already wiggles a struggling fish
    /// and now and then splashes with it, mostly unseen under the water. While it struggles, the
    /// game that owns it pushes it up until its back breaks the surface, and every game draws its
    /// splash about three times a second; while it rests it splashes no more than the game has it.
    /// The struggle is read from the fish's saved state, as the game reads it, so every player
    /// sees it. A struggling fish whose line is eased off heads away from the one fishing as it
    /// takes line out, swimming as fast as the line goes out, and back at its own speed after.
    /// </summary>
    internal static class Thrash
    {
        /// <summary>How far under the surface a struggling fish is held, its back just breaking the water, and how hard it is pushed up.</summary>
        private const float UnderSurface = 0.05f;
        private const float PushUp = 30f;

        /// <summary>How far ahead, in metres, a fish taking line out heads.</summary>
        private const float RunAhead = 4f;

        /// <summary>Seconds between a struggling fish's splashes, varied a little.</summary>
        private const float SplashEveryMin = 0.25f;
        internal const float SplashEveryMax = 0.45f;

        private static readonly AccessTools.FieldRef<Fish, float> WaterLevel = AccessTools.FieldRefAccess<Fish, float>("m_inWater");
        private static readonly AccessTools.FieldRef<Fish, Rigidbody> Body = AccessTools.FieldRefAccess<Fish, Rigidbody>("m_body");
        private static readonly AccessTools.FieldRef<Fish, Vector3> Waypoint = AccessTools.FieldRefAccess<Fish, Vector3>("m_waypoint");
        private static readonly AccessTools.FieldRef<Fish, ZNetView> View = AccessTools.FieldRefAccess<Fish, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<Fish, bool> Fast = AccessTools.FieldRefAccess<Fish, bool>("m_fast");

        private static readonly Dictionary<Fish, float> NextSplash = new Dictionary<Fish, float>();

        /// <summary>A running fish's own speed from before its run, and the speed the run gave it.</summary>
        private static readonly Dictionary<Fish, (float Own, float Run)> Runs = new Dictionary<Fish, (float, float)>();

        /// <summary>How many splashes this game drew for struggling fish, for the self-test.</summary>
        [ForTest]
        public static int SplashesForTest { get; private set; }

        /// <summary>After the fish's step: while it struggles on a line, it splashes, and its owner holds it near the surface.</summary>
        public static void Step(Fish fish)
        {
            var view = View(fish);
            if (view == null || !view.IsValid()) return;
            var zdo = view.GetZDO();

            // The game saves a struggle's length as a decimal number but its end as a whole 0, which is
            // kept apart, so the decimal stays and every game, the game's own splashes included, goes on
            // reading a struggle. The game owning the fish saves the end as a decimal too.
            if (view.IsOwner() && !fish.IsEscaping() && zdo.GetFloat(ZDOVars.s_escape) > 0f) zdo.Set(ZDOVars.s_escape, 0f);
            if (view.IsOwner()) Pace(fish);
            if (zdo.GetInt(ZDOVars.s_hooked) != 1 || zdo.GetFloat(ZDOVars.s_escape) <= 0f)
            {
                NextSplash.Remove(fish);
                return;
            }

            // A struggle splashes the moment it starts, then again and again, its back breaking the water or not.
            var now = Time.time;
            if (!NextSplash.TryGetValue(fish, out var at) || now >= at)
            {
                fish.m_jumpEffects.Create(fish.transform.position, Quaternion.identity, fish.transform);
                SplashesForTest++;
                NextSplash[fish] = now + Random.Range(SplashEveryMin, SplashEveryMax);
            }

            if (!view.IsOwner()) return;
            // The game steers the fish toward a place to swim at its own depth each step; while it
            // struggles that place is lifted to the surface, and it is pushed up on top.
            var target = WaterLevel(fish) - UnderSurface;
            var waypoint = Waypoint(fish);
            if (Lines.IsRunning(fish) && Player.m_localPlayer != null)
            {
                // With the line eased off it takes line out: it heads away from the one fishing.
                var away = fish.transform.position - Player.m_localPlayer.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f) waypoint = fish.transform.position + away.normalized * RunAhead;
            }
            if (waypoint.y < target) waypoint.y = target;
            Waypoint(fish) = waypoint;
            if (fish.transform.position.y < target) Body(fish).AddForce(Vector3.up * PushUp, ForceMode.Acceleration);
        }

        /// <summary>
        /// A fish taking line out swims as fast as the line goes out; the game doubles a fish's speed
        /// while it is fast near a player, so that is taken out. Once the run ends it gets its own
        /// speed back, unless something else set its speed meanwhile.
        /// </summary>
        private static void Pace(Fish fish)
        {
            var run = Lines.RunSpeedOf(fish);
            if (run > 0f)
            {
                var own = Runs.TryGetValue(fish, out var was) ? was.Own : fish.m_speed;
                var speed = Fast(fish) && fish.m_avoidSpeedScale > 0f ? run / fish.m_avoidSpeedScale : run;
                fish.m_speed = speed;
                Runs[fish] = (own, speed);
                return;
            }
            if (!Runs.TryGetValue(fish, out var ran)) return;
            if (Mathf.Approximately(fish.m_speed, ran.Run)) fish.m_speed = ran.Own;
            Runs.Remove(fish);
        }

        /// <summary>The speed a fish taking line out was given, 0 for one not running, for the self-test.</summary>
        [ForTest]
        public static float RunSpeedForTest(Fish fish) => fish != null && Runs.TryGetValue(fish, out var ran) ? ran.Run : 0f;

        /// <summary>Lets go of a fish no longer there.</summary>
        public static void Forget(Fish fish)
        {
            NextSplash.Remove(fish);
            Runs.Remove(fish);
        }
    }
}
