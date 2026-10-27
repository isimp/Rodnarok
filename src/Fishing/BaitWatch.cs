using System.Linq;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Writes to the log each change of the local player's bait while a float of theirs is out or
    /// was a moment ago, with what was going on: whether a float is out, what it holds, how far the
    /// fish on it is, whether the player attacks or draws. The game takes a bait at the cast and
    /// gives one back only for a line reeled in empty; the log shows when a bait comes or goes.
    /// </summary>
    internal static class BaitWatch
    {
        private static int _last = -1;
        private static float _floatSeenAt = -100f;

        /// <summary>How long after the last float was seen a bait's change is still told, in seconds.</summary>
        private const float AfterFloat = 5f;

        /// <summary>Called every frame in a world.</summary>
        public static void Tick()
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                _last = -1;
                return;
            }
            var count = player.GetInventory().GetAllItems().Where(i => i.m_shared.m_ammoType == "$item_fishingbait").Sum(i => i.m_stack);
            var ff = FishingFloat.GetAllInstances().FirstOrDefault(f => f != null && f.GetComponent<ZNetView>() is ZNetView v && v.IsValid() && v.IsOwner());
            if (ff != null) _floatSeenAt = Time.time;
            if (_last >= 0 && count != _last && Time.time - _floatSeenAt <= AfterFloat)
            {
                var fish = ff != null ? ff.GetCatch() : null;
                var c = System.Globalization.CultureInfo.InvariantCulture;
                Log.Info("Bait " + _last.ToString(c) + " -> " + count.ToString(c) + ": "
                    + (ff == null ? "no float out (last seen " + (Time.time - _floatSeenAt).ToString("0.0", c) + " s ago)" : "float out " + ff.GetBait())
                    + (fish == null ? "" : ", a fish on it " + Vector3.Distance(fish.transform.position, player.transform.position).ToString("0.0", c) + " m off, out of the water " + fish.IsOutOfWater())
                    + ", attacking " + player.InAttack() + ", drawing " + player.IsDrawingBow());
            }
            _last = count;
        }
    }
}
