using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Fishing while sitting, as a player does it: on a chair, then sitting on the ground, the
    /// character holding a rod draws a cast, casts and reels in, and stays seated through all of
    /// it. Needs only a character beside water: the rod and a chair are brought for the run, and
    /// afterwards the character stands where it stood with its gear as before, the chair and the
    /// float taken away. Pictures of the seated draw and cast are taken for looking at.
    /// </summary>
    internal static class SelfTestSitting
    {
        private const string ChairPrefab = "piece_chair02";

        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly System.Reflection.MethodInfo StopEmote = AccessTools.Method(typeof(Player), "StopEmote");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("ARodCastsAndReelsWhileSittingAndTheCharacterStaysSeated", Sitting);
        }

        private static IEnumerable Sitting(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var water = SelfTestArrival.DeepWaterBeside(player.transform.position, 1f);
            check.That(water.HasValue, "water within 6 to 20 m of the character");
            if (!water.HasValue) yield break;
            var chairPrefab = ZNetScene.instance.GetPrefab(ChairPrefab);
            check.That(chairPrefab != null && chairPrefab.GetComponentInChildren<Chair>() != null, "the game's wooden chair is there to sit on");
            if (chairPrefab == null) yield break;

            var stoodAt = player.transform.position;
            var dry = SelfTestPlaces.DryLandBeside(stoodAt);
            check.That(dry.HasValue, "dry, level land within 1.5 to 4 m of the character for a chair");
            if (!dry.HasValue) yield break;
            var toWater = water.Value - dry.Value;
            toWater.y = 0f;
            var facing = Quaternion.LookRotation(toWater.sqrMagnitude < 0.01f ? player.transform.forward : toWater.normalized);
            var gear = SelfTestGear.RodInHand(player);
            GameObject chair = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;

                // On a chair on dry land, sat on as a player sits on one: walked up to, then used.
                chair = Object.Instantiate(chairPrefab, dry.Value, facing);
                yield return null;
                var seat = chair.GetComponentInChildren<Chair>();
                MoveTo(player, dry.Value + facing * Vector3.back * 0.6f);
                yield return null;
                var used = seat.Interact(player, false, false);
                for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime) yield return null;
                check.That(player.IsAttached() && player.IsSitting(), "the character sits on the chair in the sitting pose; used: " + used + ", attached: " + player.IsAttached() + ", sitting pose: " + player.IsSitting());
                if (player.IsAttached())
                {
                    foreach (var step in CastAndReel(check, player, water.Value, "chair", () => player.IsAttached(), () => player.IsSitting())) yield return step;
                }
                player.AttachStop();
                ZNetScene.instance.Destroy(chair);
                chair = null;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;

                // On dry ground where the chair stood, with the sit emote.
                MoveTo(player, dry.Value);
                yield return null;
                var sat = player.StartEmote("sit", oneshot: false);
                for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime) yield return null;
                check.That(SeatedFishing.SitsOnTheGround(player) && EmotePose(player), "the character sits on the ground in the sit emote's pose; emote started: " + sat + ", in the sit emote: " + SeatedFishing.SitsOnTheGround(player) + ", emote pose: " + EmotePose(player));
                if (SeatedFishing.SitsOnTheGround(player))
                {
                    foreach (var step in CastAndReel(check, player, water.Value, "ground", () => SeatedFishing.SitsOnTheGround(player), () => EmotePose(player))) yield return step;
                }
            }
            finally
            {
                SelfTestDraw.Drawing = false;
                SelfTestHold.Holding = false;
                foreach (var ff in OwnFloats()) ZNetScene.instance.Destroy(ff.gameObject);
                if (player.IsAttached()) player.AttachStop();
                if (player.InEmote()) StopEmote?.Invoke(player, null);
                if (chair != null) ZNetScene.instance.Destroy(chair);
                gear.PutBack();
            }

            // Back where it stood.
            MoveTo(player, stoodAt);
            for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
            check.That(!player.IsAttached() && !player.InEmote(), "the character stands again after the run");
            check.That(gear.AsBefore(), "the character's gear and what it knows are as before the run: " + gear.Difference());
        }

        /// <summary>Draws a cast toward the water, casts, and reels in, checking the character stays seated throughout.</summary>
        private static IEnumerable<object> CastAndReel(ScenarioCheck check, Player player, Vector3 water, string seat, System.Func<bool> seated, System.Func<bool> posed)
        {
            foreach (var ff in OwnFloats()) ZNetScene.instance.Destroy(ff.gameObject);
            var look = water + Vector3.up * 3f - player.GetEyePoint();
            player.SetLookDir(look.normalized);
            yield return null;

            // A moment with the key up, then the key held past the rod's least draw.
            for (var waited = 0f; waited < 0.3f; waited += Time.deltaTime) yield return null;
            SelfTestDraw.Drawing = true;
            var drew = false;
            var pictured = false;
            var stayed = true;
            for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime)
            {
                drew |= player.IsDrawingBow();
                stayed &= seated();
                if (waited >= 1f && !pictured) pictured = Picture(check, "sitting-" + seat + "-drawing");
                yield return null;
            }
            SelfTestDraw.Drawing = false;
            check.That(drew, "sitting on the " + seat + ", the rod draws");
            check.That(stayed, "the character stayed seated on the " + seat + " while drawing");

            FishingFloat cast = null;
            for (var waited = 0f; waited < 3f && cast == null; waited += Time.deltaTime)
            {
                cast = OwnFloats().FirstOrDefault();
                yield return null;
            }
            check.That(cast != null, "sitting on the " + seat + ", the rod casts");
            check.That(seated(), "the character is still seated on the " + seat + " after the cast");
            for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime) yield return null;
            check.That(posed(), "a moment after the cast the character is back in the seated pose on the " + seat);
            Picture(check, "sitting-" + seat + "-cast");
            yield return null;
            if (cast == null) yield break;

            // Reeling in.
            var line = LineLength(cast);
            stayed = true;
            SelfTestHold.Holding = true;
            for (var waited = 0f; waited < 1.5f && cast != null; waited += Time.deltaTime)
            {
                stayed &= seated();
                yield return null;
            }
            SelfTestHold.Holding = false;
            check.That(cast != null && LineLength(cast) < line - 0.5f, "sitting on the " + seat + ", the line reels in, from " + line.ToString("0.0", CultureInfo.InvariantCulture) + " m");
            check.That(stayed, "the character stayed seated on the " + seat + " while reeling");
            if (cast != null) ZNetScene.instance.Destroy(cast.gameObject);
            yield return null;
        }

        /// <summary>
        /// Whether the character shows an emote's pose. The game marks a chair's pose as sitting and
        /// the sit emote's as an emote, so sitting on the ground is seen here.
        /// </summary>
        private static bool EmotePose(Player player) => CurrentPose != null && (int)CurrentPose.Invoke(player, null) == EmoteTag;

        private static readonly System.Reflection.MethodInfo CurrentPose = AccessTools.Method(typeof(Character), "GetCurrentAnimHash");
        private static readonly int EmoteTag = ZSyncAnimation.GetHash("emote");

        /// <summary>Stands the character at a place, as if it had walked there.</summary>
        private static void MoveTo(Player player, Vector3 place)
        {
            player.transform.position = place;
            var body = player.GetComponent<Rigidbody>();
            if (body == null) return;
            body.position = place;
            body.linearVelocity = Vector3.zero;
        }

        private static List<FishingFloat> OwnFloats() =>
            FishingFloat.GetAllInstances().Where(f => f != null && f.GetComponent<ZNetView>() is ZNetView v && v.IsValid() && v.IsOwner()).ToList();

        private static bool Picture(ScenarioCheck check, string what)
        {
            var trouble = SelfTestPictures.Take(what);
            check.That(trouble == null, "a picture of " + what + " is taken: " + trouble);
            return true;
        }
    }
}
