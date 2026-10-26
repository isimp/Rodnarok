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

        /// <summary>How far out a school appears, and how far it swims off when it leaves.</summary>
        private const float ComeFrom = 14f;
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
        private static float _nextSweep;

        /// <summary>Rules the self-test sets for its run in place of the settings.</summary>
        [ForTest]
        public static FishingRules RulesForTest { get; set; }

        private static FishingRules Rules => RulesForTest ?? Settings.Rules;

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
            foreach (var fish in cast.Fish) MaybeLeap(fish, cast.Float.transform.position);
            if (cast.Hooked || cast.Fish.Count == 0 || cast.BiterFreeAt < float.MaxValue) return;
            var near = cast.Fish.Count(f => Across(f.transform.position, cast.Float.transform.position) < Near);
            if (near * 2 >= cast.Fish.Count || Time.time - cast.CameAt > 20f)
            {
                cast.Biter = School.Biter(cast.Fish.Count, Random.value);
                cast.BiterFreeAt = Time.time + Random.Range(2f, 4f);
            }
        }

        /// <summary>The school appears out in the water, one kind of the bait's fish, as many as the world spawns together.</summary>
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

            var size = School.Size(book.Group(kind), Rules, Random.value);
            var depth = template.m_minDepth + 0.25f;
            var facing = Quaternion.LookRotation(Flat(home - start.Value));
            for (var i = 0; i < size; i++)
            {
                var offset = Random.insideUnitCircle * 2f;
                var place = new Vector3(start.Value.x + offset.x, surface - depth, start.Value.z + offset.y);
                var go = UnityEngine.Object.Instantiate(prefab, place, facing);
                var zdo = go.GetComponent<ZNetView>().GetZDO();
                zdo.Set(FloatKey, cast.Id);
                // Fish.Start takes its home from here, so the school's home is the float.
                zdo.Set(ZDOVars.s_spawnPoint, home);
                var quality = book.RollQuality(kind, place);
                if (quality > 1) go.GetComponent<ItemDrop>()?.SetQuality(quality);

                var fish = go.GetComponent<Fish>();
                fish.m_swimRange = CircleRange;
                fish.m_wpDurationMin = RepickMin;
                fish.m_wpDurationMax = RepickMax;
                fish.m_maxDepth = Mathf.Min(fish.m_maxDepth, fish.m_minDepth + 0.5f);
                cast.Fish.Add(fish);
                CastOf[fish] = cast;
                Ours.Add(fish);
                NextLeap[fish] = Time.time + Random.Range(0.5f, 3f);
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
            for (var distance = ComeFrom; distance >= 6f; distance -= 4f)
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
            Jump(fish);
        }

        /// <summary>A fish whose float is gone swims off, and is taken away once it has been gone a while.</summary>
        private static void Leave(Fish fish, Vector3 from)
        {
            if (fish == null) return;
            CastOf.Remove(fish);
            NextLeap.Remove(fish);
            Flee(fish, from);
            if (!GoneSince.ContainsKey(fish)) GoneSince[fish] = Time.time;
        }

        /// <summary>The fish makes a place well away from <paramref name="from"/> its home and swims there at the speed the game gives a fleeing fish.</summary>
        private static void Flee(Fish fish, Vector3 from)
        {
            var view = fish.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var away = Flat(fish.transform.position - from);
            var place = fish.transform.position + away * GoTo;
            SpawnPoint(fish) = place;
            view.GetZDO().Set(ZDOVars.s_spawnPoint, place);
            fish.m_swimRange = CircleRange * 2f;
            if (fish.m_speed > 0f && Ours.Contains(fish) && !Fled.Contains(fish))
            {
                fish.m_speed *= fish.m_avoidSpeedScale;
                Fled.Add(fish);
            }
            SwimTimer(fish) = 0f;
        }

        private static readonly HashSet<Fish> Fled = new HashSet<Fish>();

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
                var ff = ZNetScene.instance.FindInstance(id);
                if (ff == null || ff.GetComponent<FishingFloat>() == null) Leave(fish, fish.transform.position - fish.transform.forward);
            }
        }

        /// <summary>Takes away the fish that have been gone long enough, where this game owns them.</summary>
        private static void TakeAwayTheLeft()
        {
            if (GoneSince.Count == 0) return;
            var leave = Rules.LeaveSeconds;
            foreach (var pair in GoneSince.ToList())
            {
                var fish = pair.Key;
                if (fish == null)
                {
                    Drop(fish);
                    continue;
                }
                if (!School.Remove(ours: true, floatAlive: false, Time.time - pair.Value, leave)) continue;
                var view = fish.GetComponent<ZNetView>();
                if (view != null && view.IsValid() && view.IsOwner()) ZNetScene.instance.Destroy(fish.gameObject);
                Drop(fish);
            }
        }

        private static void Drop(Fish fish)
        {
            GoneSince.Remove(fish);
            Ours.Remove(fish);
            CastOf.Remove(fish);
            NextLeap.Remove(fish);
            Fled.Remove(fish);
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
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
