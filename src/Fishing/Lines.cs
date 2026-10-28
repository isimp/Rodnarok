using System;
using System.Collections.Generic;
using System.Globalization;
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
        private static readonly AccessTools.FieldRef<Character, bool> BlockPressed = AccessTools.FieldRefAccess<Character, bool>("m_blocking");

        private sealed class Line
        {
            public Fish Fish;
            public float Strain;
            public float RunMetres;
            public float RunSeconds;
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
                if (Running.Add(fish))
                {
                    line.RunMetres = 0f;
                    line.RunSeconds = 0f;
                    TellRun(ff, owner, line);
                }
                LineLength(ff) += lineOut;
                line.RunMetres += lineOut;
                line.RunSeconds += seconds;
            }
            else if (Running.Remove(fish))
            {
                Log.Info("The fish stopped taking line: " + line.RunMetres.ToString("0.0", CultureInfo.InvariantCulture) + " m in "
                    + line.RunSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s, " + (struggling ? "the hold counts again" : "it rests")
                    + ", strain " + line.Strain.ToString("0.00", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>The line breaks as the game breaks one: its message, the fish let go, the float gone, the break's effect.</summary>
        private static void Snap(FishingFloat ff, Fish fish, ZNetView view)
        {
            SnappedForTest++;
            Tell(ff, "held through a struggle until the strain snapped it");
            Forget(ff);
            _snapping = true;
            try
            {
                Message(ff, "$msg_fishing_linebroke", true);
            }
            finally
            {
                _snapping = false;
            }
            fish.OnHooked(null);
            ff.m_lineBreakEffect.Create(ff.transform.position, Quaternion.identity);
            view.Destroy();
        }

        private static bool _snapping;

        /// <summary>A float of this game says its line broke: the game's own break is told, Rodnarök's own snap has told itself.</summary>
        public static void BrokeMessage(FishingFloat ff, string msg)
        {
            if (_snapping || msg != "$msg_fishing_linebroke") return;
            Tell(ff, "the game broke it: the float got more than " + ff.m_breakDistance.ToString("0", CultureInfo.InvariantCulture)
                + " m farther from the rod tip than the line is long, or more than " + ff.m_maxDistance.ToString("0", CultureInfo.InvariantCulture) + " m away");
        }

        /// <summary>Writes to the log why a struggling fish starts taking line: whether the block button was down, and what kept the game from counting it as a hold.</summary>
        private static void TellRun(FishingFloat ff, Character owner, Line line)
        {
            var player = owner as Player;
            var why = !BlockPressed(owner) ? "the line is eased off (block button up)"
                : !owner.IsBlocking() ? "the block button is down but the game does not count it as blocking ("
                    + (owner.InAttack() ? "attacking, " : "") + (owner.InDodge() ? "dodging, " : "") + (owner.InPlaceMode() ? "building, " : "")
                    + (owner.IsEncumbered() ? "overweight, " : "") + (owner.InMinorAction() ? "in a minor action, " : "") + (owner.IsStaggering() ? "staggered, " : "")
                    + "on " + owner.GetType().Name + ")"
                : "the block button is down but stamina is out";
            Log.Info("The fish takes line, " + why + ": line " + LineLength(ff).ToString("0.0", CultureInfo.InvariantCulture) + " m"
                + ", strain " + line.Strain.ToString("0.00", CultureInfo.InvariantCulture)
                + (player != null ? ", stamina " + player.GetStamina().ToString("0", CultureInfo.InvariantCulture) : ""));
        }

        /// <summary>Writes to the log why and how a line broke, so a break nobody saw coming can be traced.</summary>
        private static void Tell(FishingFloat ff, string why)
        {
            var owner = Owner(ff);
            var fish = ff.GetCatch();
            var tip = owner != null ? Utils.FindChild(owner.transform, "_RodTop") : null;
            var player = owner as Player;
            var seated = player != null && (player.IsAttached() || SeatedFishing.SitsOnTheGround(player));
            var text = "The line broke, " + why + ": line " + LineLength(ff).ToString("0.0", CultureInfo.InvariantCulture) + " m"
                + (tip != null ? ", float " + Vector3.Distance(tip.position, ff.transform.position).ToString("0.0", CultureInfo.InvariantCulture) + " m from the rod tip"
                    + ", the tip " + (tip.position.y - ZoneSystem.instance.m_waterLevel).ToString("0.0", CultureInfo.InvariantCulture) + " m above the sea" : ", no rod tip found")
                + (fish != null ? ", the fish " + Vector3.Distance(fish.transform.position, ff.transform.position).ToString("0.0", CultureInfo.InvariantCulture) + " m from the float"
                    + (fish.IsEscaping() ? ", struggling" : ", resting") : ", nothing hooked")
                + ", strain " + StrainOf(ff).ToString("0.00", CultureInfo.InvariantCulture)
                + (owner != null ? ", holding " + owner.IsBlocking() : "") + (player != null ? ", stamina " + player.GetStamina().ToString("0", CultureInfo.InvariantCulture) : "")
                + ", seated " + seated;
            Log.Info(text);
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
