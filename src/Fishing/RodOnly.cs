using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The fish a bait draws can only be caught with the rod, so a bait is never a way to summon
    /// fish for other means. A projectile hitting one, a spear, harpoon, arrow or bolt as mods such
    /// as Spearfishing let catch fish, finds nothing there: for that hit the game's own lookup of
    /// what a projectile hit answers nothing, for the game and for every mod asking through it, and
    /// the school is spooked and leaves. A fish of the world's own is left as the game and those
    /// mods have it.
    /// </summary>
    internal static class RodOnly
    {
        private static GameObject _hidden;

        /// <summary>How many hits on a bait's fish found nothing, for the self-test.</summary>
        [ForTest]
        public static int TurnedAwayForTest { get; private set; }

        /// <summary>A projectile's hit begins: a fish a bait drew is hidden from it, and its school spooked.</summary>
        public static void Hit(Projectile projectile, Collider collider)
        {
            _hidden = null;
            if (collider == null) return;
            var found = Projectile.FindHitObject(collider);
            var fish = found != null ? found.GetComponent<Fish>() : null;
            if (fish == null || School.Catchable(drawnByBait: !Schools.FloatOf(fish).IsNone(), byRod: false)) return;
            _hidden = found;
            TurnedAwayForTest++;
            Schools.Spook(fish, projectile != null ? projectile.transform.position : fish.transform.position);
        }

        /// <summary>What a projectile's hit finds while it is being handled: nothing where a bait's fish is hidden.</summary>
        public static GameObject Seen(GameObject found) => found != null && found == _hidden ? null : found;

        /// <summary>The hit is handled.</summary>
        public static void Done() => _hidden = null;
    }
}
