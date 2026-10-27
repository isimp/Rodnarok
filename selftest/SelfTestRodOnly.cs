using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The fish a bait draws are caught only with the rod: a float with Fishing Bait lands in deep
    /// water beside the character, the school comes, and a wooden arrow's projectile hits one of
    /// its fish through the game's own hit. The fish must stay a swimming fish, nothing must reach
    /// the inventory, the school must be spooked, no fish of it may take the bait, and it must
    /// swim off. With Spearfishing installed its handling of the hit runs too and must find
    /// nothing. Needs a character beside water at least 3 m deep; the rod, the float, the arrow
    /// and the school are taken away again.
    /// </summary>
    internal static class SelfTestRodOnly
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly System.Reflection.MethodInfo OnHit = AccessTools.Method(typeof(Projectile), "OnHit");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AFishABaitDrewIsCaughtOnlyWithTheRodAndAShotSpooksItsSchool", ShotSpooks);
        }

        private static IEnumerable ShotSpooks(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            var arrowPrefab = ArrowProjectile();
            check.That(floatPrefab != null && baitPrefab != null && arrowPrefab != null, "the float, the bait and the wooden arrow's projectile are there");
            check.That(OnHit != null, "the game's projectile hit is there");
            if (floatPrefab == null || baitPrefab == null || arrowPrefab == null || OnHit == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            GameObject arrow = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;

                // The school of a scenario before may still be leaving, and a new one waits for it.
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, baitLost: false);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;

                List<Fish> school = null;
                for (var waited = 0f; waited < 15f; waited += Time.deltaTime)
                {
                    school = SchoolOf(floatId);
                    if (school.Count > 0) break;
                    yield return null;
                }
                check.That(school != null && school.Count > 0, "a school came within 15 s of the float landing");
                if (school == null || school.Count == 0) yield break;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                school = SchoolOf(floatId);
                if (school.Count == 0) yield break;

                // A wooden arrow hits the fish of the school nearest the character, through the game's own hit.
                var target = school.OrderBy(f => Vector3.Distance(f.transform.position, player.transform.position)).First();
                var collider = target.GetComponentInChildren<Collider>();
                check.That(collider != null, "the fish has a collider to be hit");
                if (collider == null) yield break;
                var name = target.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
                var carried = Carried(player, name);
                var turnedAway = RodOnly.TurnedAwayForTest;
                var spooked = Schools.SpookedForTest;
                var before = Average(school, ff);
                arrow = Object.Instantiate(arrowPrefab, target.transform.position + Vector3.up, Quaternion.LookRotation(Vector3.down));
                var projectile = arrow.GetComponent<Projectile>();
                projectile.Setup(player, Vector3.down * 10f, -1f, new HitData(), null, null);
                OnHit.Invoke(projectile, new object[] { collider, target.transform.position, true, Vector3.up });
                yield return null;

                check.That(RodOnly.TurnedAwayForTest == turnedAway + 1, "the arrow's hit found nothing where the bait's fish was");
                check.That(target != null && target.GetComponent<Fish>() != null, "the fish hit is still a swimming fish, not an item to take");
                check.That(Carried(player, name) == carried, "nothing reached the inventory, " + carried.ToString(CultureInfo.InvariantCulture) + " carried before and " + Carried(player, name).ToString(CultureInfo.InvariantCulture) + " after");
                check.That(Schools.SpookedForTest == spooked + 1, "the school was spooked");

                // Spooked, it takes no bait and swims off.
                var bit = false;
                for (var waited = 0f; waited < 10f; waited += Time.deltaTime)
                {
                    if (Nibbler(ff) != null && school.Contains(Nibbler(ff))) bit = true;
                    yield return null;
                }
                check.That(!bit, "no fish of the spooked school went for the bait");
                var left = school.Where(f => f != null).ToList();
                var after = left.Count == 0 ? float.MaxValue : Average(left, ff);
                check.That(after > before + 3f, "the spooked school swam off, from " + before.ToString("0.0", CultureInfo.InvariantCulture) + " m to " + (left.Count == 0 ? "gone" : after.ToString("0.0", CultureInfo.InvariantCulture) + " m") + " from the float");
            }
            finally
            {
                PlayRules.ForTest = null;
                if (arrow != null) ZNetScene.instance.Destroy(arrow);
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        /// <summary>The projectile a wooden arrow flies as, from the arrow's own attack, else the game's bow projectile.</summary>
        private static GameObject ArrowProjectile()
        {
            var shared = ObjectDB.instance.GetItemPrefab("ArrowWood")?.GetComponent<ItemDrop>()?.m_itemData.m_shared;
            var fromArrow = shared?.m_attack?.m_attackProjectile;
            return fromArrow != null && fromArrow.GetComponent<Projectile>() != null ? fromArrow : ZNetScene.instance.GetPrefab("bow_projectile");
        }

        private static float Average(List<Fish> fish, FishingFloat ff) =>
            fish.Where(f => f != null).Select(f => Across(f.transform.position, ff.transform.position)).DefaultIfEmpty(0f).Average();

        private static int Carried(Player player, string sharedName) =>
            player.GetInventory().GetAllItems().Where(i => i.m_shared.m_name == sharedName).Sum(i => i.m_stack);

        private static List<Fish> SchoolOf(ZDOID floatId) =>
            Fish.Instances.OfType<Fish>().Where(f => f != null && Schools.FloatOf(f) == floatId).ToList();

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
