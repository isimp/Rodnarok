using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Rodnarok
{
    /// <summary>
    /// The schools baits draw. When a float this game cast lies in the water, the wait is rolled;
    /// when it is over a school of the bait's fish appears out in the water and makes the float
    /// its home, circling it near the top of the depth it swims at, the small ones leaping on
    /// the way in as the game lets them. Once most of it is near, one fish of it may go for the
    /// bait, and the game's own bite follows. When that one is hooked the rest scatter, and once
    /// the float is gone the school swims off and is taken away.
    ///
    /// Each of these fish carries the id of its float in its saved data. Any game that owns a
    /// marked fish whose float is gone takes it away a while later, so none gather in the world's
    /// save, which keeps every fish until it is caught. A fish without the mark is never touched.
    /// </summary>
    internal static class Schools
    {
        /// <summary>The saved key holding the id of the float a fish was drawn to.</summary>
        private static readonly KeyValuePair<int, int> FloatKey = ZDO.GetHashZDOID("Rodnarok.float");

        /// <summary>How near the float, across the ground, a fish counts as there.</summary>
        private const float Near = 6f;

        /// <summary>How far a school swims off when it leaves.</summary>
        private const float GoTo = 30f;

        /// <summary>How widely a school circles its float, and how often each fish picks a new place to swim.</summary>
        private const float CircleRange = 4f;
        private const float RepickMin = 1.5f;
        private const float RepickMax = 3f;

        private static readonly AccessTools.FieldRef<Fish, Vector3> SpawnPoint = AccessTools.FieldRefAccess<Fish, Vector3>("m_spawnPoint");
        private static readonly AccessTools.FieldRef<Fish, float> SwimTimer = AccessTools.FieldRefAccess<Fish, float>("m_swimTimer");
        private static readonly AccessTools.FieldRef<Fish, bool> Jumping = AccessTools.FieldRefAccess<Fish, bool>("m_isJumping");
        private static readonly Action<Fish> Jump = AccessTools.MethodDelegate<Action<Fish>>(AccessTools.Method(typeof(Fish), "Jump"));

        private sealed class Cast
        {
            public FishingFloat Float;
            public ZDOID Id;
            public Vector3 LastPlace;
            public float DueAt;
            public float CameAt;
            public bool Came;
            public Vector3 Start;
            public float OwnSpeed;
            public bool In;
            public readonly List<Fish> Fish = new List<Fish>();
            public int Biter = -1;
            public float BiterFreeAt = float.MaxValue;
            public bool Hooked;
        }

        private static readonly Dictionary<FishingFloat, Cast> Casts = new Dictionary<FishingFloat, Cast>();
        private static readonly Dictionary<Fish, Cast> CastOf = new Dictionary<Fish, Cast>();
        private static readonly HashSet<Fish> Ours = new HashSet<Fish>();
        private static readonly Dictionary<Fish, float> NextLeap = new Dictionary<Fish, float>();
        private static readonly Dictionary<Fish, float> GoneSince = new Dictionary<Fish, float>();
        private static readonly HashSet<Fish> WetOnce = new HashSet<Fish>();
        private static readonly Dictionary<Fish, float> MadeAt = new Dictionary<Fish, float>();
        private static float _nextSweep;

        /// <summary>How far ahead the water must stay deep for a fish to leap, in metres.</summary>
        private const float LeapReach = 10f;

        /// <summary>Seconds a new fish may take to first touch the water before it counts as stuck on land.</summary>
        private const float FirstWetGrace = 3f;

        /// <summary>How many of the schools' fish were found on land and taken away, for the self-test.</summary>
        [ForTest]
        public static int LandedForTest { get; private set; }

        /// <summary>What the last fish found on land was doing, for the self-test.</summary>
        [ForTest]
        public static string LastLandingForTest { get; private set; } = "";

        /// <summary>When each fish last leapt.</summary>
        private static readonly Dictionary<Fish, float> LeaptAt = new Dictionary<Fish, float>();

        private static FishingRules Rules => PlayRules.Now;

        /// <summary>Whether this game steers a fish as one of a school.</summary>
        public static bool IsOurs(Fish fish) => fish != null && Ours.Contains(fish);

        /// <summary>The float a fish was drawn to, from its saved mark; none for a fish of the world's own.</summary>
        public static ZDOID FloatOf(Fish fish)
        {
            var zdo = fish != null ? fish.GetComponent<ZNetView>()?.GetZDO() : null;
            return zdo == null ? ZDOID.None : zdo.GetZDOID(FloatKey);
        }

        /// <summary>The float one of the schools' fish may go for now: its own, while it is the one let to and the float waits in the water; none otherwise.</summary>
        public static FishingFloat FloatFor(Fish fish)
        {
            if (!CastOf.TryGetValue(fish, out var cast) || cast.Hooked || cast.Float == null) return null;
            if (cast.Biter < 0 || cast.Biter >= cast.Fish.Count || cast.Fish[cast.Biter] != fish || Time.time < cast.BiterFreeAt) return null;
            return cast.Float.IsInWater() && cast.Float.GetCatch() == null ? cast.Float : null;
        }

        /// <summary>A fish was hooked or let go by a float; a school whose fish is hooked scatters.</summary>
        public static void Hooked(Fish fish, FishingFloat ff)
        {
            if (ff == null || !CastOf.TryGetValue(fish, out var cast) || cast.Hooked) return;
            cast.Hooked = true;
            var hooked = cast.Fish.IndexOf(fish);
            foreach (var i in School.Scatter(cast.Fish.Count, hooked))
            {
                var other = cast.Fish[i];
                if (other != null) Flee(other, ff.transform.position);
            }
        }

        /// <summary>Called every frame in a world.</summary>
        public static void Tick()
        {
            if (Player.m_localPlayer == null || ZNetScene.instance == null)
            {
                Forget();
                return;
            }
            WatchFloats();
            foreach (var cast in Casts.Values.ToList()) Advance(cast);
            KeepOffLand();
            if (Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + 2f;
                Sweep();
            }
            TakeAwayTheLeft();
        }

        /// <summary>Starts a cast for each float this game cast that lies in the water, and ends the cast of each float gone.</summary>
        private static void WatchFloats()
        {
            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null || Casts.ContainsKey(ff) || !ff.IsInWater()) continue;
                var view = ff.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                var rules = Rules;
                Casts[ff] = new Cast
                {
                    Float = ff,
                    Id = view.GetZDO().m_uid,
                    LastPlace = ff.transform.position,
                    DueAt = Time.time + Arrival.Seconds(rules.WaitMin, rules.WaitMax, Random.value),
                };
            }

            foreach (var pair in Casts.ToList())
            {
                if (pair.Key != null)
                {
                    pair.Value.LastPlace = pair.Key.transform.position;
                    continue;
                }
                Casts.Remove(pair.Key);
                foreach (var fish in pair.Value.Fish) Leave(fish, pair.Value.LastPlace);
            }
        }

        private static void Advance(Cast cast)
        {
            if (!cast.Came)
            {
                if (Time.time >= cast.DueAt) Come(cast);
                return;
            }

            cast.Fish.RemoveAll(f => f == null);
            if (!cast.In) DriftIn(cast);
            foreach (var fish in cast.Fish) MaybeLeap(fish, cast.Float.transform.position);
            if (cast.Hooked || cast.Fish.Count == 0 || cast.BiterFreeAt < float.MaxValue) return;
            var near = cast.Fish.Count(f => Across(f.transform.position, cast.Float.transform.position) < Near);
            if (School.ReadyToBite(Time.time - cast.CameAt, near, cast.Fish.Count))
            {
                cast.Biter = School.Biter(cast.Fish.Count, Random.value);
                cast.BiterFreeAt = Time.time + Random.Range(2f, 4f);
            }
        }

        /// <summary>
        /// Moves the school's home from where it appeared toward the float, its fish wandering widely
        /// around it at one slow pace, until it reaches the float and each fish swims at its own speed.
        /// The game takes a new fish's home from its saved data once, on its first frame, so a fish
        /// whose home jumped from the school's then picks a new place to swim at once.
        /// </summary>
        private static void DriftIn(Cast cast)
        {
            var share = Drift.Share(Time.time - cast.CameAt);
            var home = Vector3.Lerp(cast.Start, cast.Float.transform.position, share);
            foreach (var fish in cast.Fish)
            {
                if (Fled.Contains(fish)) continue;
                if (Across(SpawnPoint(fish), home) > 1f) SwimTimer(fish) = 0f;
                SpawnPoint(fish) = home;
                fish.m_swimRange = Drift.SwimRange(share, CircleRange);
                fish.m_speed = Drift.Speed(cast.OwnSpeed, share);
            }
            cast.In = share >= 1f;
        }

        /// <summary>The school appears loose out in the water, one kind of the bait's fish, as many as the world spawns together.</summary>
        private static void Come(Cast cast)
        {
            cast.Came = true;
            cast.CameAt = Time.time;
            var book = FishBook.Current;
            var kind = book?.Baits.Choose(cast.Float.GetBait(), Random.value);
            var prefab = book?.Prefab(kind);
            if (prefab == null) return;
            var template = prefab.GetComponent<Fish>();
            var home = cast.Float.transform.position;
            var surface = home.y;
            var start = StartFor(home, surface, template.m_minDepth);
            if (!start.HasValue)
            {
                Log.Info("No school for " + kind + ": the water by the float is shallower than it swims.");
                return;
            }

            cast.Start = start.Value;
            cast.OwnSpeed = template.m_speed;
            var size = School.Size(book.Group(kind), Rules, Random.value);
            var depth = template.m_minDepth + 0.25f;
            for (var i = 0; i < size; i++)
            {
                // Each fish where the water is deep enough for it, or else at the school's centre, which is.
                var offset = Random.insideUnitCircle * Drift.Spread;
                var facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var place = new Vector3(start.Value.x + offset.x, surface - depth, start.Value.z + offset.y);
                if (!DeepEnough(place, surface, template.m_minDepth)) place = new Vector3(start.Value.x, surface - depth, start.Value.z);
                var go = UnityEngine.Object.Instantiate(prefab, place, facing);
                // The rod's catch hands the fish over by itself; lying anywhere else it is never picked up in passing.
                var item = go.GetComponent<ItemDrop>();
                if (item != null) item.m_autoPickup = false;
                var zdo = go.GetComponent<ZNetView>().GetZDO();
                zdo.Set(FloatKey, cast.Id);
                // Fish.Start takes its home from here, so the school's home is the float.
                zdo.Set(ZDOVars.s_spawnPoint, home);
                var quality = book.RollQuality(kind, place);
                if (quality > 1) item?.SetQuality(quality);

                var fish = go.GetComponent<Fish>();
                SpawnPoint(fish) = start.Value;
                fish.m_swimRange = Drift.SwimRange(0f, CircleRange);
                fish.m_speed = Drift.Speed(cast.OwnSpeed, 0f);
                fish.m_wpDurationMin = RepickMin;
                fish.m_wpDurationMax = RepickMax;
                fish.m_maxDepth = Mathf.Min(fish.m_maxDepth, fish.m_minDepth + 0.5f);
                cast.Fish.Add(fish);
                CastOf[fish] = cast;
                Ours.Add(fish);
                MadeAt[fish] = Time.time;
                NextLeap[fish] = Time.time + Random.Range(0.5f, 3f);
            }
        }

        /// <summary>
        /// Takes away at once any of the schools' fish this game owns that lies on land: out of the
        /// water and not on a line, with the ground right beneath it, or never in the water at all a
        /// while after it was made. A leaping fish is out of the water too, but above water, and the
        /// game ends a leap on touching anything, the float or another fish included, so what lies
        /// beneath tells land from a leap: dry ground, however high a fish flops above it. The game
        /// lets a fish out of the water be picked up, and the schools' fish are drawn by the bait,
        /// not won from the water.
        /// </summary>
        private static void KeepOffLand()
        {
            if (Ours.Count == 0) return;
            foreach (var fish in Ours.ToList())
            {
                if (fish == null) continue;
                if (!fish.IsOutOfWater())
                {
                    WetOnce.Add(fish);
                    continue;
                }
                if (fish.IsHooked()) continue;
                var stuck = !WetOnce.Contains(fish) && MadeAt.TryGetValue(fish, out var made) && Time.time - made >= FirstWetGrace;
                if (!stuck && !OverDryGround(fish)) continue;
                var view = fish.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                LandedForTest++;
                LastLandingForTest = Doing(fish);
                Log.Warning("A " + Utils.GetPrefabName(fish.gameObject) + " of a school lay on land and was taken away; " + LastLandingForTest + ".");
                ZNetScene.instance.Destroy(fish.gameObject);
                Drop(fish);
            }
        }

        /// <summary>
        /// A place out in the water for a school to come from: as far out as can be found, with the
        /// water deep enough for the fish there, halfway and at the float, so it can swim in.
        /// </summary>
        private static Vector3? StartFor(Vector3 home, float surface, float minDepth)
        {
            if (!DeepEnough(home, surface, minDepth)) return null;
            var turn = Random.Range(0f, 360f);
            for (var distance = Drift.From; distance >= 6f; distance -= 4f)
            {
                for (var step = 0; step < 12; step++)
                {
                    var angle = (turn + step * 30f) * Mathf.Deg2Rad;
                    var p = home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (DeepEnough(p, surface, minDepth) && DeepEnough((p + home) / 2f, surface, minDepth)) return p;
                }
            }
            return null;
        }

        private static bool DeepEnough(Vector3 p, float surface, float minDepth) =>
            ZoneSystem.instance.GetSolidHeight(p, out var ground) && surface - ground >= minDepth + 0.5f;

        /// <summary>A small fish leaps a couple of times on its way in, as the game lets fish of its quality leap.</summary>
        private static void MaybeLeap(Fish fish, Vector3 home)
        {
            if (!NextLeap.TryGetValue(fish, out var at) || Time.time < at) return;
            if (Across(fish.transform.position, home) < Near)
            {
                NextLeap.Remove(fish);
                return;
            }
            NextLeap[fish] = Time.time + Random.Range(3f, 6f);
            var view = fish.GetComponent<ZNetView>();
            var item = fish.GetComponent<ItemDrop>();
            if (view == null || !view.IsOwner() || fish.IsOutOfWater() || Jumping(fish)) return;
            if (item != null && item.m_itemData.m_quality > fish.m_jumpMaxLevel) return;
            if (!DeepAhead(fish, home.y)) return;
            Jump(fish);
            LeaptAt[fish] = Time.time;
        }

        /// <summary>Whether the water stays deep enough for the fish all the way a leap could carry it.</summary>
        private static bool DeepAhead(Fish fish, float surface)
        {
            var ahead = Flat(fish.transform.forward);
            for (var distance = 2f; distance <= LeapReach; distance += 2f)
            {
                if (!DeepEnough(fish.transform.position + ahead * distance, surface, fish.m_minDepth)) return false;
            }
            return true;
        }

        /// <summary>A fish whose float is gone swims off, and is taken away once it has been gone a while.</summary>
        private static void Leave(Fish fish, Vector3 from)
        {
            if (fish == null) return;
            CastOf.Remove(fish);
            NextLeap.Remove(fish);
            Flee(fish, from, Rules.DiveDepth);
            if (!GoneSince.ContainsKey(fish)) GoneSince[fish] = Time.time;
        }

        /// <summary>
        /// The fish makes a place well away from <paramref name="from"/> its home and swims there at
        /// the speed the game gives a fleeing fish. Given a depth to dive to, it heads for water that
        /// deep where there is some, and swims no shallower there, out of sight under the surface.
        /// </summary>
        private static void Flee(Fish fish, Vector3 from, float dive = 0f)
        {
            var view = fish.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var away = Flat(fish.transform.position - from);
            var deep = Vector3.zero;
            var diving = dive > 0f && OpenWaterAway(fish, away, dive + 0.5f, out deep);
            var place = diving ? deep : OpenWaterAway(fish, away);
            if (diving)
            {
                fish.m_minDepth = Mathf.Max(fish.m_minDepth, dive);
                fish.m_maxDepth = Mathf.Max(fish.m_maxDepth, dive + 20f);
            }
            SpawnPoint(fish) = place;
            view.GetZDO().Set(ZDOVars.s_spawnPoint, place);
            fish.m_swimRange = CircleRange * 2f;
            if (fish.m_speed > 0f && Ours.Contains(fish) && !Fled.Contains(fish))
            {
                // A school left on its way in still swims at the cruise; it flees at its own speed's flight.
                if (CastOf.TryGetValue(fish, out var cast) && cast.OwnSpeed > 0f) fish.m_speed = cast.OwnSpeed;
                fish.m_speed *= fish.m_avoidSpeedScale;
                Fled.Add(fish);
            }
            SwimTimer(fish) = 0f;
        }

        private static readonly HashSet<Fish> Fled = new HashSet<Fish>();

        /// <summary>
        /// A place out in open water for a fleeing fish: of the directions around it, the one most
        /// away from what it flees whose water is deep enough for it there and halfway, so it never
        /// swims for the shore; straight away when no direction has such water.
        /// </summary>
        private static Vector3 OpenWaterAway(Fish fish, Vector3 away) =>
            OpenWaterAway(fish, away, fish.m_minDepth, out var place) ? place : fish.transform.position + away * GoTo;

        /// <summary>The place most away from what the fish flees whose water is at least this deep there and halfway; false where no direction has such water.</summary>
        private static bool OpenWaterAway(Fish fish, Vector3 away, float depth, out Vector3 place)
        {
            var from = fish.transform.position;
            var surface = ZoneSystem.instance.m_waterLevel;
            place = default;
            var bestDot = float.MinValue;
            for (var step = 0; step < 16; step++)
            {
                var angle = step * Mathf.PI / 8f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var p = from + dir * GoTo;
                if (!DeepEnough(p, surface, depth) || !DeepEnough(from + dir * (GoTo / 2f), surface, depth)) continue;
                var dot = Vector3.Dot(dir, away);
                if (dot <= bestDot) continue;
                bestDot = dot;
                place = p;
            }
            return bestDot > float.MinValue;
        }

        /// <summary>Finds marked fish this game owns that it does not steer yet, such as those of a float cast before a restart, and lets those whose float is gone leave.</summary>
        private static void Sweep()
        {
            foreach (var fish in Fish.Instances.OfType<Fish>())
            {
                if (fish == null || CastOf.ContainsKey(fish) || GoneSince.ContainsKey(fish)) continue;
                var id = FloatOf(fish);
                if (id.IsNone()) continue;
                var view = fish.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                Ours.Add(fish);
                var item = fish.GetComponent<ItemDrop>();
                if (item != null) item.m_autoPickup = false;
                var ff = ZNetScene.instance.FindInstance(id);
                if (ff == null || ff.GetComponent<FishingFloat>() == null) Leave(fish, fish.transform.position - fish.transform.forward);
            }
        }

        /// <summary>Takes away the leaving fish this game owns once they are out of sight: far from every player or deep under the surface.</summary>
        private static void TakeAwayTheLeft()
        {
            if (GoneSince.Count == 0) return;
            var rules = Rules;
            foreach (var pair in GoneSince.ToList())
            {
                var fish = pair.Key;
                if (fish == null)
                {
                    Drop(fish);
                    continue;
                }
                var p = fish.transform.position;
                var under = ZoneSystem.instance.m_waterLevel - p.y;
                if (!School.Remove(ours: true, floatAlive: false, Time.time - pair.Value, NearestPlayer(p), under, rules)) continue;
                var view = fish.GetComponent<ZNetView>();
                if (view != null && view.IsValid() && view.IsOwner()) ZNetScene.instance.Destroy(fish.gameObject);
                Drop(fish);
            }
        }

        /// <summary>How far the nearest player in the loaded world stands from a place.</summary>
        private static float NearestPlayer(Vector3 place)
        {
            var nearest = float.MaxValue;
            foreach (var player in Player.GetAllPlayers())
            {
                if (player != null) nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, place));
            }
            return nearest;
        }

        private static void Drop(Fish fish)
        {
            GoneSince.Remove(fish);
            Ours.Remove(fish);
            CastOf.Remove(fish);
            NextLeap.Remove(fish);
            Fled.Remove(fish);
            WetOnce.Remove(fish);
            MadeAt.Remove(fish);
            LeaptAt.Remove(fish);
        }

        /// <summary>How far under the water the ground beneath a fish may lie and still count as the shore, in metres.</summary>
        private const float ShoreDepth = 0.3f;

        /// <summary>Whether the ground beneath a fish is dry land or the shore's edge, which is so for a fish on land however it flops, and never for one leaping over water.</summary>
        private static bool OverDryGround(Fish fish)
        {
            var p = fish.transform.position;
            return ZoneSystem.instance.GetSolidHeight(p, out var ground) && ground >= ZoneSystem.instance.m_waterLevel - ShoreDepth;
        }

        /// <summary>What a fish was doing, as far as the schools know: for telling how one came to land.</summary>
        private static string Doing(Fish fish)
        {
            var now = Time.time;
            var parts = new List<string>();
            parts.Add(GoneSince.ContainsKey(fish) ? "leaving" : Fled.Contains(fish) ? "fleeing" : CastOf.ContainsKey(fish) ? "circling its float" : "found by the sweep");
            if (CastOf.TryGetValue(fish, out var cast) && cast.Float != null)
                parts.Add(Across(fish.transform.position, cast.Float.transform.position).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m from its float");
            if (LeaptAt.TryGetValue(fish, out var leapt)) parts.Add("leapt " + (now - leapt).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s before");
            if (MadeAt.TryGetValue(fish, out var made)) parts.Add("made " + (now - made).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s before");
            parts.Add(WetOnce.Contains(fish) ? "had been in the water" : "never in the water");
            return string.Join(", ", parts);
        }

        /// <summary>Lets go of everything on leaving a world; the marks in the save stay, for the sweep of whoever comes next.</summary>
        private static void Forget()
        {
            if (Casts.Count == 0 && Ours.Count == 0) return;
            Casts.Clear();
            CastOf.Clear();
            Ours.Clear();
            NextLeap.Clear();
            GoneSince.Clear();
            Fled.Clear();
            WetOnce.Clear();
            MadeAt.Clear();
            LeaptAt.Clear();
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
