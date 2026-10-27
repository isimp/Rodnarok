using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Line tension on the game's own float, in the game that cast it. After each step of the
    /// float the line's strain follows <see cref="Tension"/>: held through a struggle it builds,
    /// and a line strained to snapping breaks as the game breaks one, with its message and effect.
    /// While a struggling fish has the line eased off it takes line out and swims away from the
    /// rod. A new catch starts on an unstrained line.
    /// </summary>
    internal static class Lines
    {
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly Func<FishingFloat, Character> Owner = AccessTools.MethodDelegate<Func<FishingFloat, Character>>(AccessTools.Method(typeof(FishingFloat), "GetOwner"));
        private static readonly Action<FishingFloat, string, bool> Message = AccessTools.MethodDelegate<Action<FishingFloat, string, bool>>(AccessTools.Method(typeof(FishingFloat), "Message"));

        private sealed class Line
        {
            public Fish Fish;
            public float Strain;
        }

        private static readonly Dictionary<FishingFloat, Line> Strains = new Dictionary<FishingFloat, Line>();
        private static readonly HashSet<Fish> Running = new HashSet<Fish>();

        /// <summary>How many lines snapped from strain in this game, for the self-test.</summary>
        [ForTest]
        public static int SnappedForTest { get; private set; }

        /// <summary>How strained the line of this float is, from 0 to 1 snapped; 0 with nothing on it.</summary>
        public static float StrainOf(FishingFloat ff) => ff != null && Strains.TryGetValue(ff, out var line) ? line.Strain : 0f;

        /// <summary>Whether a hooked fish is taking line out, the line eased off while it struggles.</summary>
        public static bool IsRunning(Fish fish) => fish != null && Running.Contains(fish);

        /// <summary>After the float's step in the game that cast it: the strain follows the fight, and an eased-off struggle takes line out.</summary>
        public static void Step(FishingFloat ff)
        {
            var view = ff != null ? ff.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                Forget(ff);
                return;
            }
            var fish = ff.GetCatch();
            var owner = Owner(ff);
            if (fish == null || owner == null)
            {
                Forget(ff);
                return;
            }
            if (!Strains.TryGetValue(ff, out var line) || line.Fish != fish)
            {
                if (line != null && line.Fish != null) Running.Remove(line.Fish);
                line = new Line { Fish = fish };
                Strains[ff] = line;
            }

            var rules = PlayRules.Now;
            var struggling = fish.IsEscaping();
            var holding = owner.IsBlocking() && owner.HaveStamina();
            var seconds = Time.fixedDeltaTime;
            line.Strain = Tension.Step(line.Strain, struggling, holding, seconds, rules);
            if (Tension.Snaps(line.Strain))
            {
                Snap(ff, fish, view);
                return;
            }
            var lineOut = Tension.LineOut(struggling, holding, seconds, rules);
            if (lineOut > 0f)
            {
                LineLength(ff) += lineOut;
                Running.Add(fish);
            }
            else
            {
                Running.Remove(fish);
            }
        }

        /// <summary>The line breaks as the game breaks one: its message, the fish let go, the float gone, the break's effect.</summary>
        private static void Snap(FishingFloat ff, Fish fish, ZNetView view)
        {
            SnappedForTest++;
            Forget(ff);
            Message(ff, "$msg_fishing_linebroke", true);
            fish.OnHooked(null);
            ff.m_lineBreakEffect.Create(ff.transform.position, Quaternion.identity);
            view.Destroy();
        }

        /// <summary>Lets go of a float's line.</summary>
        public static void Forget(FishingFloat ff)
        {
            if (ff == null || !Strains.TryGetValue(ff, out var line)) return;
            if (line.Fish != null) Running.Remove(line.Fish);
            Strains.Remove(ff);
        }

        /// <summary>Lets go of every line on leaving a world.</summary>
        public static void Clear()
        {
            Strains.Clear();
            Running.Clear();
        }
    }
}
