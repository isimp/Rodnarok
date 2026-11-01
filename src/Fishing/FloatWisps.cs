using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A player wearing a Wisplight has its wisp guard their float, as <see cref="FloatWisp"/> has
    /// it. The game moves the wisp in the wearer's own game, every physics step, toward a place over
    /// the wearer; while the wearer's float is out Rodnarök moves it instead, with the Wisplight's own
    /// acceleration, top speed and friction, toward a place by the float. The wisp is a networked
    /// object whose place every game is sent, so every player sees it search, dive in at a bite and
    /// follow the fight; the flare's brightness is the wearer's alone, since only their game learns
    /// of a bite. Once the float is gone the wisp is drawn back to the wearer and handed to the
    /// game's own following within its leash, so it never snaps across. The wisp's mist clearing
    /// stays with the wearer: while it guards a float, every game keeps a copy of the clearing over
    /// the wearer and narrows the wisp's own to a pocket round the float.
    /// </summary>
    internal static class FloatWisps
    {
        /// <summary>On the wisp's networked data: the player whose float it guards, none while it follows as the game has it.</summary>
        private const string GuardsFor = "RodnarokWispGuards";

        private static readonly AccessTools.FieldRef<SE_Demister, GameObject> BallOf = AccessTools.FieldRefAccess<SE_Demister, GameObject>("m_ballInstance");
        private static readonly AccessTools.FieldRef<SE_Demister, Vector3> BallVelocity = AccessTools.FieldRefAccess<SE_Demister, Vector3>("m_ballVel");
        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<LightFlicker, float> BaseIntensity = AccessTools.FieldRefAccess<LightFlicker, float>("m_baseIntensity");

        /// <summary>A wisp this game moves for its wearer's float.</summary>
        private sealed class Guard
        {
            public WispMotion Motion;
            public WispTask Task;
            public float Bright = 1f;
            public LightFlicker Flicker;
            public float OwnIntensity;
        }

        /// <summary>A wisp guarding a float, as this game keeps its clearings.</summary>
        private sealed class Clearing
        {
            public GameObject OverWearer;
            public float OwnRange;
        }

        private static readonly Dictionary<SE_Demister, Guard> Guards = new Dictionary<SE_Demister, Guard>();
        private static readonly Dictionary<Demister, Clearing> Clearings = new Dictionary<Demister, Clearing>();
        private static readonly HashSet<Demister> Copies = new HashSet<Demister>();
        private static readonly Dictionary<Demister, GameObject> Guarding = new Dictionary<Demister, GameObject>();
        private static readonly List<Demister> Ended = new List<Demister>();

        /// <summary>What this game's wisp does now, for the self-test; Home with none guarding.</summary>
        [ForTest]
        public static WispTask TaskForTest { get; private set; }

        /// <summary>How bright this game's wisp shines against its own light, for the self-test.</summary>
        [ForTest]
        public static float BrightForTest { get; private set; } = 1f;

        /// <summary>The copy of a guarding wisp's clearing kept over its wearer, null for none, for the self-test.</summary>
        [ForTest]
        public static Demister ClearingOverWearerForTest(Demister wisps) =>
            wisps != null && Clearings.TryGetValue(wisps, out var clearing) && clearing.OverWearer != null ? clearing.OverWearer.GetComponent<Demister>() : null;

        /// <summary>
        /// Right after the game has moved a Wisplight's wisp for its wearer: in the wearer's own game,
        /// while their float is out or the wisp is still coming back from one, the wisp is moved by
        /// Rodnarök instead.
        /// </summary>
        public static void Steer(SE_Demister wisplight, float seconds)
        {
            if (!(wisplight.m_character is Player player) || player != Player.m_localPlayer) return;
            var ball = BallOf(wisplight);
            var view = ball != null ? ball.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid())
            {
                Guards.Remove(wisplight);
                return;
            }
            var ff = Settings.FloatWisp ? CrosshairRing.OwnFloat() : null;
            Guards.TryGetValue(wisplight, out var guard);
            if (ff == null)
            {
                if (guard == null) return;
                Return(wisplight, player, ball, view, guard, seconds);
                return;
            }
            if (guard == null)
            {
                var at = ball.transform.position;
                var velocity = BallVelocity(wisplight);
                guard = new Guard { Motion = new WispMotion(at.x, at.y, at.z) { VX = velocity.x, VY = velocity.y, VZ = velocity.z } };
                guard.Flicker = ball.GetComponentInChildren<LightFlicker>();
                if (guard.Flicker != null) guard.OwnIntensity = BaseIntensity(guard.Flicker);
                Guards[wisplight] = guard;
                view.GetZDO().Set(GuardsFor, player.GetZDOID());
            }
            var window = PlayRules.Now.HookWindow;
            var catchOn = ff.GetCatch();
            var sinceBite = Time.time - NibbleTime(ff);
            var biting = catchOn == null && Nibbler(ff) != null && NibbleTime(ff) > 0f && sinceBite < window;
            guard.Task = FloatWisp.Task(floatOut: true, biting, hooked: catchOn != null);
            var offset = FloatWisp.Offset(guard.Task, Time.time);
            var to = ff.transform.position + new Vector3(offset.X, offset.Y, offset.Z);
            Move(wisplight, ball, guard, to, seconds);
            Shine(guard, FloatWisp.Brightness(guard.Task, sinceBite, window));
        }

        /// <summary>The float gone: drawn back toward the wearer, and handed to the game's own following within its leash.</summary>
        private static void Return(SE_Demister wisplight, Player player, GameObject ball, ZNetView view, Guard guard, float seconds)
        {
            guard.Task = WispTask.Return;
            var home = player.transform.position + player.transform.TransformVector(wisplight.m_offset);
            Move(wisplight, ball, guard, home, seconds);
            Shine(guard, 1f);
            if (Vector3.Distance(ball.transform.position, home) > FloatWisp.HandBack) return;
            BallVelocity(wisplight) = new Vector3(guard.Motion.VX, guard.Motion.VY, guard.Motion.VZ);
            Guards.Remove(wisplight);
            view.GetZDO().Set(GuardsFor, ZDOID.None);
            TaskForTest = WispTask.Home;
        }

        private static void Move(SE_Demister wisplight, GameObject ball, Guard guard, Vector3 to, float seconds)
        {
            guard.Motion.Step(to.x, to.y, to.z, wisplight.m_ballAcceleration, wisplight.m_ballMaxSpeed, wisplight.m_ballFriction, FloatWisp.Pull(guard.Task), seconds);
            ball.transform.position = new Vector3(guard.Motion.X, guard.Motion.Y, guard.Motion.Z);
            TaskForTest = guard.Task;
        }

        /// <summary>The wisp's light at a share of its own, the game's flicker going on over it.</summary>
        private static void Shine(Guard guard, float bright)
        {
            guard.Bright = bright;
            BrightForTest = bright;
            if (guard.Flicker != null) BaseIntensity(guard.Flicker) = guard.OwnIntensity * bright;
        }

        /// <summary>
        /// Each frame, in every game: a wisp marked as guarding a float keeps a copy of its clearing
        /// over its wearer, and its own clearing narrowed to the pocket round the float; once the
        /// mark is gone, or the wisp or the wearer, the clearing is as the game has it again.
        /// </summary>
        public static void Tick()
        {
            // Found first and changed after: a copy of a clearing joins the game's list as it is made.
            Guarding.Clear();
            foreach (var demister in Demister.GetDemisters())
            {
                if (demister == null || Copies.Contains(demister) || demister.m_forceField == null) continue;
                var view = demister.GetComponentInParent<ZNetView>();
                var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
                var wearerId = zdo != null ? zdo.GetZDOID(GuardsFor) : ZDOID.None;
                var wearer = wearerId.IsNone() ? null : ZNetScene.instance.FindInstance(wearerId);
                if (wearer != null) Guarding[demister] = wearer;
            }
            foreach (var pair in Guarding)
            {
                var demister = pair.Key;
                var wearer = pair.Value;
                if (!Clearings.TryGetValue(demister, out var clearing))
                {
                    var copy = Object.Instantiate(demister.gameObject);
                    copy.name = "RodnarokWisplightClearing";
                    var kept = copy.GetComponent<Demister>();
                    if (kept != null) Copies.Add(kept);
                    clearing = new Clearing { OverWearer = copy, OwnRange = demister.m_forceField.endRange };
                    Clearings[demister] = clearing;
                    demister.m_forceField.endRange = FloatWisp.PocketRange;
                }
                if (clearing.OverWearer != null) clearing.OverWearer.transform.position = wearer.transform.position + Vector3.up * OverWearer;
            }
            Ended.Clear();
            foreach (var pair in Clearings)
                if (!Guarding.ContainsKey(pair.Key)) Ended.Add(pair.Key);
            foreach (var demister in Ended) End(demister);
        }

        /// <summary>How high over the wearer the clearing kept for them sits, in metres: where the Wisplight's wisp hovers over its wearer.</summary>
        private const float OverWearer = 3.5f;

        private static void End(Demister demister)
        {
            if (!Clearings.TryGetValue(demister, out var clearing)) return;
            Clearings.Remove(demister);
            if (demister != null && demister.m_forceField != null) demister.m_forceField.endRange = clearing.OwnRange;
            if (clearing.OverWearer != null)
            {
                Copies.Remove(clearing.OverWearer.GetComponent<Demister>());
                Object.Destroy(clearing.OverWearer);
            }
        }

        /// <summary>Lets every clearing be as the game has it, on leaving a world.</summary>
        public static void Forget()
        {
            Guarding.Clear();
            Ended.Clear();
            Ended.AddRange(Clearings.Keys);
            foreach (var demister in Ended) End(demister);
            Copies.Clear();
            Guards.Clear();
            TaskForTest = WispTask.Home;
            BrightForTest = 1f;
        }
    }

    /// <summary>Right after the game moves a Wisplight's wisp, Rodnarök moves it while it guards a float.</summary>
    [HarmonyPatch(typeof(SE_Demister), nameof(SE_Demister.UpdateStatusEffect))]
    internal static class WispSteer
    {
        private static void Postfix(SE_Demister __instance, float dt) => FloatWisps.Steer(__instance, dt);
    }
}
