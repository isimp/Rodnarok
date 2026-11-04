using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A hooked fish shows its next struggle coming (<see cref="StruggleWarning"/>), on every game. The
    /// game owning the fish shares when its next struggle is due as server time on the fish's networked
    /// data (<see cref="StruggleAtKey"/>), read from the game's own <c>m_nextEscape</c> as a struggle
    /// ends; the game owning the float shares the strain on its line (<see cref="StrainKey"/>). Every
    /// game tilts the float's look (never the float itself) as it grows restless and draws its rod line
    /// taut and quivering over what the game drew.
    /// </summary>
    internal static class StruggleTells
    {
        /// <summary>The server time, in ticks, the hooked fish's next struggle is due; 0 while none is known.</summary>
        public static readonly int StruggleAtKey = "Rodnarok.struggleAt".GetStableHashCode();

        /// <summary>The strain on a float's line, in steps of <see cref="StrainStep"/>, shared by the game owning the float.</summary>
        public static readonly int StrainKey = "Rodnarok.strain".GetStableHashCode();

        /// <summary>The steps the shared strain moves in, so it is written only when it changes noticeably.</summary>
        private const float StrainStep = 0.05f;

        /// <summary>How fast a tilted float's look eases back upright, in degrees a second.</summary>
        private const float EaseBack = 60f;

        private static readonly AccessTools.FieldRef<Fish, float> NextEscape = AccessTools.FieldRefAccess<Fish, float>("m_nextEscape");

        private sealed class Tell
        {
            public Fish Fish;
            public List<(Transform T, Vector3 Pos, Quaternion Rot)> Look;
            public float Phase;
            public float Tilt;
            public Vector3 Axis = Vector3.right;
            public float Until = float.PositiveInfinity;
            public float Restless;
            public bool Struggling;
            public bool Taut;
            public float TautSeconds;
            public float Quiver;
            public float Strain;
        }

        private static readonly Dictionary<FishingFloat, Tell> Tells = new Dictionary<FishingFloat, Tell>();
        private static readonly Dictionary<LineConnect, Tell> LinesDrawn = new Dictionary<LineConnect, Tell>();
        private static readonly Dictionary<Fish, float> Shared = new Dictionary<Fish, float>();
        private static readonly List<FishingFloat> Gone = new List<FishingFloat>();

        /// <summary>
        /// After the fish's step, on the game owning it: shares when its next struggle is due as soon as
        /// the game has decided it, and takes it back while it struggles or is off the line.
        /// </summary>
        public static void Share(Fish fish)
        {
            var view = fish.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner() || ZNet.instance == null) return;
            var zdo = view.GetZDO();
            var next = NextEscape(fish);
            // At the hook the game's time is from before, so the struggle starts at once: nothing to warn of.
            if (!fish.IsHooked() || fish.IsEscaping() || next <= Time.time)
            {
                Shared.Remove(fish);
                if (zdo.GetLong(StruggleAtKey, 0L) != 0L) zdo.Set(StruggleAtKey, 0L);
                return;
            }
            if (Shared.TryGetValue(fish, out var was) && was == next) return;
            Shared[fish] = next;
            zdo.Set(StruggleAtKey, ZNet.instance.GetTime().Ticks + (long)((next - Time.time) * TimeSpan.TicksPerSecond));
        }

        /// <summary>Lets go of a fish no longer there.</summary>
        public static void Forget(Fish fish) => Shared.Remove(fish);

        /// <summary>Each frame: every hooked float's warning, from the fish's and the float's shared data.</summary>
        public static void Tick()
        {
            var rules = PlayRules.Now;
            Gone.Clear();
            foreach (var pair in Tells)
                if (pair.Key == null || pair.Key.GetCatch() == null || !rules.StruggleWarning) Gone.Add(pair.Key);
            foreach (var ff in Gone) End(ff);
            if (!rules.StruggleWarning || ZNet.instance == null) return;

            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null) continue;
                var fish = ff.GetCatch();
                if (fish == null) continue;
                var fishView = fish.GetComponent<ZNetView>();
                var floatView = ff.GetComponent<ZNetView>();
                if (fishView == null || !fishView.IsValid() || floatView == null || !floatView.IsValid()) continue;
                if (!Tells.TryGetValue(ff, out var tell) || tell.Fish != fish)
                {
                    if (tell != null) Upright(tell);
                    tell = new Tell { Fish = fish };
                    Tells[ff] = tell;
                    if (ff.m_rodLine != null) LinesDrawn[ff.m_rodLine] = tell;
                }
                Step(ff, tell, fishView.GetZDO(), floatView, rules);
            }
        }

        private static void Step(FishingFloat ff, Tell tell, ZDO fishZdo, ZNetView floatView, FishingRules rules)
        {
            var fish = tell.Fish;
            tell.Struggling = fishZdo.GetInt(ZDOVars.s_hooked) == 1 && fishZdo.GetFloat(ZDOVars.s_escape) > 0f;
            var at = fishZdo.GetLong(StruggleAtKey, 0L);
            tell.Until = at == 0L || tell.Struggling ? float.PositiveInfinity : (float)((at - ZNet.instance.GetTime().Ticks) / (double)TimeSpan.TicksPerSecond);

            // The strain: the float's own game knows it and shares it, every other game reads it.
            var floatZdo = floatView.GetZDO();
            if (floatView.IsOwner())
            {
                tell.Strain = Lines.StrainOf(ff);
                var stepped = Mathf.Round(tell.Strain / StrainStep) * StrainStep;
                if (!Mathf.Approximately(floatZdo.GetFloat(StrainKey, 0f), stepped)) floatZdo.Set(StrainKey, stepped);
            }
            else
            {
                tell.Strain = floatZdo.GetFloat(StrainKey, 0f);
            }

            var speed = FishBook.Current?.Speed(Utils.GetPrefabName(fish.gameObject)) ?? 0f;
            tell.TautSeconds = StruggleWarning.TautSeconds(speed, fishZdo.GetInt(ZDOVars.s_quality, 1), rules);
            tell.Taut = StruggleWarning.Taut(tell.Struggling, tell.Until, tell.TautSeconds);
            tell.Quiver = StruggleWarning.Quiver(tell.Struggling, tell.Taut, tell.Strain);
            tell.Restless = tell.Struggling ? 0f : StruggleWarning.Restlessness(tell.Until);

            // The float's look trembles toward the fish as it grows restless, as at a bite.
            var target = 0f;
            if (tell.Restless > 0f)
            {
                tell.Phase += Time.deltaTime / FloatTremble.Seconds(tell.Restless);
                target = FloatTremble.Degrees(tell.Restless, tell.Phase);
                var offset = fish.transform.position - ff.transform.position;
                var (x, z) = FloatTremble.Axis(offset.x, offset.z);
                tell.Axis = new Vector3(x, 0f, z);
            }
            tell.Tilt = target > tell.Tilt ? target : Mathf.MoveTowards(tell.Tilt, target, EaseBack * Time.deltaTime);
            if (tell.Tilt <= 0f && tell.Look == null) return;
            if (tell.Look == null) tell.Look = ArrivalSigns.LookOf(ff.gameObject);
            var root = ff.transform;
            var axis = root.InverseTransformDirection(tell.Axis);
            var turn = tell.Tilt > 0f && axis.sqrMagnitude > 0.0001f ? Quaternion.AngleAxis(tell.Tilt, axis) : Quaternion.identity;
            foreach (var (t, pos, rot) in tell.Look)
            {
                if (t == null) continue;
                t.localPosition = turn * pos;
                t.localRotation = turn * rot;
            }
        }

        /// <summary>
        /// After the game drew a rod line: a line taut before or through a struggle is drawn straight from
        /// the rod to the float, swinging aside by its quiver, both ends where the game put them.
        /// </summary>
        public static void Draw(LineConnect line)
        {
            if (!LinesDrawn.TryGetValue(line, out var tell) || !tell.Taut) return;
            var renderer = line.GetComponent<LineRenderer>();
            if (renderer == null || !renderer.enabled || renderer.positionCount < 3) return;
            var count = renderer.positionCount;
            var from = renderer.GetPosition(0);
            var to = renderer.GetPosition(count - 1);
            var way = to - from;
            var up = line.transform.InverseTransformDirection(Vector3.up);
            var side = Vector3.Cross(way, up);
            side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.zero;
            // Measured along the line in world metres, turned into the line's own space.
            var scale = line.transform.lossyScale.x;
            var aside = scale > 0.0001f ? side / scale : side;
            for (var i = 1; i < count - 1; i++)
            {
                var along = (float)i / (count - 1);
                var swing = StruggleWarning.QuiverOffset(tell.Quiver, along, Time.time);
                // Half the swing goes up and down, so the quiver reads from the side and from behind alike.
                var lift = up * (StruggleWarning.QuiverOffset(tell.Quiver, along, Time.time + 0.37f / StruggleWarning.QuiverHz) * 0.5f);
                renderer.SetPosition(i, Vector3.Lerp(from, to, along) + aside * swing + (scale > 0.0001f ? lift / scale : lift));
            }
        }

        private static void Upright(Tell tell)
        {
            if (tell.Look == null) return;
            foreach (var (t, pos, rot) in tell.Look)
            {
                if (t == null) continue;
                t.localPosition = pos;
                t.localRotation = rot;
            }
            tell.Look = null;
            tell.Tilt = 0f;
        }

        private static void End(FishingFloat ff)
        {
            if (Tells.TryGetValue(ff, out var tell)) Upright(tell);
            Tells.Remove(ff);
            if (ff != null && ff.m_rodLine != null) LinesDrawn.Remove(ff.m_rodLine);
            Clean();
        }

        /// <summary>Drops lines of floats that are gone.</summary>
        private static void Clean()
        {
            var dropped = new List<LineConnect>();
            foreach (var pair in LinesDrawn)
                if (pair.Key == null) dropped.Add(pair.Key);
            foreach (var line in dropped) LinesDrawn.Remove(line);
        }

        /// <summary>Lets go of every float, on leaving a world.</summary>
        public static void ForgetAll()
        {
            foreach (var tell in Tells.Values) Upright(tell);
            Tells.Clear();
            LinesDrawn.Clear();
            Shared.Clear();
        }

        /// <summary>What a hooked float shows of the next struggle now, for the self-test; null for a float with no warning.</summary>
        [ForTest]
        public static (float Until, float Restless, float Tilt, int Parts, bool Taut, float TautSeconds, float Quiver, float Strain, bool Struggling)? StateForTest(FishingFloat ff) =>
            ff != null && Tells.TryGetValue(ff, out var t)
                ? (t.Until, t.Restless, t.Tilt, t.Look?.Count ?? 0, t.Taut, t.TautSeconds, t.Quiver, t.Strain, t.Struggling)
                : ((float, float, float, int, bool, float, float, float, bool)?)null;

        /// <summary>The game's own time a fish's next struggle is due, for the self-test to hold the shared time against.</summary>
        [ForTest]
        public static float NextStruggleForTest(Fish fish) => fish != null ? NextEscape(fish) : 0f;

        /// <summary>
        /// How a float's rod line is drawn now, for the self-test: its points, and the farthest any of
        /// them lies from the straight way between its ends, in the line's own space.
        /// </summary>
        [ForTest]
        public static (int Points, float Bend) LineForTest(FishingFloat ff)
        {
            var renderer = ff != null && ff.m_rodLine != null ? ff.m_rodLine.GetComponent<LineRenderer>() : null;
            if (renderer == null || renderer.positionCount < 2) return (0, 0f);
            var count = renderer.positionCount;
            var from = renderer.GetPosition(0);
            var to = renderer.GetPosition(count - 1);
            var bend = 0f;
            for (var i = 1; i < count - 1; i++)
            {
                var p = renderer.GetPosition(i);
                var onLine = from + Vector3.Project(p - from, to - from);
                bend = Mathf.Max(bend, Vector3.Distance(p, onLine));
            }
            return (count, bend);
        }
    }
}
