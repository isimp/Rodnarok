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
    /// when it is over, a fish of the world's own that takes the bait and can swim to the float is
    /// called to it from where it swims, and nothing is spawned. Without one, a school of the
    /// bait's fish appears out of sight beyond the float the moment it lands, 40 m from every
    /// player or 4 m under the surface, where the water gives it a way out again, and once the
    /// caster's last school is gone; where it does not, that is said once. The wait is its swim in:
    /// it makes the float its home, a deep one rising as it comes, circling it near the top of the
    /// depth it swims at, the small ones leaping on the way in as the game lets them. Once most of it is near, one
    /// fish of it may go for the bait, and the game's own bite follows. When that one is hooked the
    /// rest scatter, and once the float is gone the school swims off and is taken away.
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
        private static readonly AccessTools.FieldRef<Fish, Vector3> FishWaypoint = AccessTools.FieldRefAccess<Fish, Vector3>("m_waypoint");
        private static readonly AccessTools.FieldRef<Fish, FishingFloat> WaypointFloat = AccessTools.FieldRefAccess<Fish, FishingFloat>("m_waypointFF");
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
            public float Wait;
            public float SwimSeconds;
            public float StartUnder;
            public float SwimUnder;
            public readonly List<Fish> Fish = new List<Fish>();
            public int Biter = -1;
            public float BiterFreeAt = float.MaxValue;
            public bool Hooked;
            public bool Spooked;
            public Fish Called;
            public bool Hinted;

            // How wary the school is (Wary): startled and off the float, since when all is quiet,
            // how often startled, when it was first ready to bite and how shy it is then, and where
            // and when the float was last seen, for its speed.
            public bool Away;
            public float QuietSince;
            public int Fright;
            public bool Flinched;
            public float FastSince = -1f;
            public float ReadyAt = -1f;
            public float Shyness;
            public Vector3 FloatSeen;
            public float FloatSeenAt = -1f;
            public float FloatSpeed;

            /// <summary>When the picked fish bumps the float before it goes for the hook, in order (Bumps).</summary>
            public readonly List<float> BumpsAt = new List<float>();
        }

        private static readonly Dictionary<FishingFloat, Cast> Casts = new Dictionary<FishingFloat, Cast>();
        private static readonly Dictionary<Fish, Cast> CastOf = new Dictionary<Fish, Cast>();
        private static readonly HashSet<Fish> Ours = new HashSet<Fish>();
        private static readonly Dictionary<Fish, float> NextLeap = new Dictionary<Fish, float>();
        private static readonly Dictionary<Fish, float> GoneSince = new Dictionary<Fish, float>();
        private static readonly HashSet<Fish> WetOnce = new HashSet<Fish>();
        private static readonly Dictionary<Fish, float> MadeAt = new Dictionary<Fish, float>();
        private static readonly Dictionary<Fish, Cast> CalledBy = new Dictionary<Fish, Cast>();
        private static float _nextSweep;

        /// <summary>
        /// Where each of the schools' fish was over the last seconds, for telling how one came to land:
        /// its time, nearest player, water beneath, how far its place to swim and its home lay, and
        /// whether it was going for a float.
        /// </summary>
        private struct Sample
        {
            public float At;
            public float Player;
            public float Water;
            public float Place;
            public float Home;
            public bool ForFloat;
        }

        private static readonly Dictionary<Fish, List<Sample>> Trail = new Dictionary<Fish, List<Sample>>();
        private static float _nextSample;

        /// <summary>How many one-second samples of a fish are kept.</summary>
        private const int TrailLength = 8;

        /// <summary>What a float with too little water for a school says, once.</summary>
        private const string TooLittleWaterText = "Too little water here for a school. Fish of the world's own still bite.";

        /// <summary>How many fish of the world's own were called to a float, for the self-test.</summary>
        [ForTest]
        public static int CalledForTest { get; private set; }

        /// <summary>How many casts had too little water for a school, for the self-test.</summary>
        [ForTest]
        public static int TooLittleWaterForTest { get; private set; }

        /// <summary>How many fish this game steers away as leaving, for the self-test.</summary>
        [ForTest]
        public static int LeavingForTest => GoneSince.Count;

        /// <summary>How long the school of a float this game cast takes to swim in, for the self-test; below zero for none.</summary>
        [ForTest]
        public static float SwimSecondsForTest(FishingFloat ff) => ff != null && Casts.TryGetValue(ff, out var cast) && cast.Fish.Count > 0 ? cast.SwimSeconds : -1f;

        /// <summary>How long after the float lands what comes to it is decided, in seconds: the float settles on the water first.</summary>
        private const float Settle = 0.5f;

        /// <summary>The farthest out beyond the float a school may start, in metres.</summary>
        private const float FarthestStart = 60f;

        /// <summary>The fish of the world's own called to a float this game cast, for the self-test; null for none.</summary>
        [ForTest]
        public static Fish CalledFishForTest(FishingFloat ff) => ff != null && Casts.TryGetValue(ff, out var cast) ? cast.Called : null;

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

        /// <summary>Whether a fish of the world's own is called to a float this game cast.</summary>
        public static bool IsCalled(Fish fish) => fish != null && CalledBy.ContainsKey(fish);

        /// <summary>The float a fish was drawn to, from its saved mark; none for a fish of the world's own.</summary>
        public static ZDOID FloatOf(Fish fish)
        {
            var zdo = fish != null ? fish.GetComponent<ZNetView>()?.GetZDO() : null;
            return zdo == null ? ZDOID.None : zdo.GetZDOID(FloatKey);
        }

        /// <summary>The float one of the schools' fish may go for now: its own, while it is the one let to and the float waits in the water; none otherwise.</summary>
        public static FishingFloat FloatFor(Fish fish)
        {
            if (CalledBy.TryGetValue(fish, out var calling))
                return calling.Float != null && calling.Float.IsInWater() && calling.Float.GetCatch() == null ? calling.Float : null;
            if (!CastOf.TryGetValue(fish, out var cast) || cast.Hooked || cast.Spooked || cast.Float == null) return null;
            if (cast.Biter < 0 || cast.Biter >= cast.Fish.Count || cast.Fish[cast.Biter] != fish || Time.time < cast.BiterFreeAt) return null;
            return cast.Float.IsInWater() && cast.Float.GetCatch() == null ? cast.Float : null;
        }

        /// <summary>A fish was hooked or let go by a float; a school whose fish is hooked scatters.</summary>
        public static void Hooked(Fish fish, FishingFloat ff)
        {
            // A called fish, once on the line or let go, is the game's again.
            CalledBy.Remove(fish);
            if (ff == null || !CastOf.TryGetValue(fish, out var cast) || cast.Hooked) return;
            cast.Hooked = true;
            var hooked = cast.Fish.IndexOf(fish);
            foreach (var i in School.Scatter(cast.Fish.Count, hooked))
            {
                var other = cast.Fish[i];
                if (other != null) Flee(other, ff.transform.position);
            }
        }

        /// <summary>How many times a school this game steers was startled, for the self-test.</summary>
        [ForTest]
        public static int StartledForTest { get; private set; }

        /// <summary>How many schools this game steers gave up and swam off without biting, for the self-test.</summary>
        [ForTest]
        public static int GaveUpForTest { get; private set; }

        /// <summary>Whether the school of a float this game cast is startled and off the float now, for the self-test.</summary>
        [ForTest]
        public static bool AwayForTest(FishingFloat ff) => ff != null && Casts.TryGetValue(ff, out var cast) && cast.Away;

        /// <summary>How many schools this game spooked, for the self-test.</summary>
        [ForTest]
        public static int SpookedForTest { get; private set; }

        /// <summary>
        /// Something other than the rod hit a fish of a school this game steers: the school is
        /// spooked, takes no bait, and every fish of it not on a line leaves, diving out of sight.
        /// </summary>
        public static void Spook(Fish hit, Vector3 from)
        {
            if (hit == null || !CastOf.TryGetValue(hit, out var cast) || cast.Spooked) return;
            cast.Spooked = true;
            SpookedForTest++;
            foreach (var fish in cast.Fish.ToList())
            {
                if (fish != null && !fish.IsHooked()) Leave(fish, from);
            }
            cast.Fish.RemoveAll(f => f == null || !f.IsHooked());
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
            if (Time.time >= _nextSample)
            {
                _nextSample = Time.time + 1f;
                TakeSamples();
            }
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
                    DueAt = Time.time + Settle,
                    Wait = Arrival.Seconds(rules.WaitMin, rules.WaitMax, Random.value),
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
                if (pair.Value.Called is object) CalledBy.Remove(pair.Value.Called);
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
            if (cast.Called is object)
            {
                // A called fish gone from the world before it bit: what comes is decided again.
                if (cast.Called == null && !cast.Hooked)
                {
                    CalledBy.Remove(cast.Called);
                    cast.Called = null;
                    cast.Came = false;
                    cast.DueAt = Time.time + 1f;
                }
                return;
            }

            cast.Fish.RemoveAll(f => f == null);
            if (Startled(cast)) return;
            if (!cast.In) DriftIn(cast);
            // A school rising from deep leaps only once it is near its own depth.
            var rising = cast.StartUnder > cast.SwimUnder + 0.5f && Drift.Share(Time.time - cast.CameAt, cast.SwimSeconds) < 0.6f;
            if (!rising)
            {
                foreach (var fish in cast.Fish) MaybeLeap(fish, cast.Float.transform.position);
            }
            if (cast.BumpsAt.Count > 0 && !cast.Hooked && Time.time >= cast.BumpsAt[0])
            {
                cast.BumpsAt.RemoveAt(0);
                Bites.Bump(cast.Float);
            }
            if (cast.Hooked || cast.Fish.Count == 0 || cast.BiterFreeAt < float.MaxValue) return;
            var near = cast.Fish.Count(f => Across(f.transform.position, cast.Float.transform.position) < Near);
            if (School.ReadyToBite(Time.time - cast.CameAt, cast.SwimSeconds, near, cast.Fish.Count, cast.Spooked))
            {
                if (Rules.Wary && Hesitates(cast)) return;
                cast.Biter = School.Biter(cast.Fish.Count, Random.value);
                cast.BiterFreeAt = Time.time + Random.Range(2f, 4f);
                cast.BumpsAt.Clear();
                foreach (var after in Bumps.Times(Rules.Bumps, cast.BiterFreeAt - Time.time, Random.value, Random.value, Random.value)) cast.BumpsAt.Add(Time.time + after);
            }
        }

        /// <summary>
        /// A wary school (<see cref="Wary"/>) listens before it bites: a player heard at the float or
        /// at the school startles it, and it darts a few metres off the noise; the float moved quickly
        /// makes it flinch a little sidestep off the float. Either way it comes back once all is quiet
        /// again, the flinch sooner; frights add up and many of them drive it off, as does the float
        /// dragged through it a while. True while the school is off the float or gone.
        /// </summary>
        private static bool Startled(Cast cast)
        {
            if (!Rules.Wary || cast.Hooked || cast.Spooked || cast.Fish.Count == 0) return false;
            var at = cast.Float.transform.position;
            var now = Time.time;
            if (cast.FloatSeenAt < 0f || now - cast.FloatSeenAt >= 0.5f)
            {
                cast.FloatSpeed = cast.FloatSeenAt < 0f ? 0f : Across(at, cast.FloatSeen) / (now - cast.FloatSeenAt);
                cast.FloatSeen = at;
                cast.FloatSeenAt = now;
            }
            var heard = HeardAt(at) ?? HeardAt(Centre(cast.Fish));
            var near = cast.Fish.Any(f => Across(f.transform.position, at) < Near);
            var fast = Wary.FloatStartles(cast.FloatSpeed, near);
            if (!fast) cast.FastSince = -1f;
            else if (cast.FastSince < 0f) cast.FastSince = now;
            if (fast && Wary.Dragged(now - cast.FastSince))
            {
                StartledForTest++;
                DriveOff(cast, at);
                return true;
            }
            if (heard != null || fast)
            {
                cast.QuietSince = now;
                // A flinching school heard to as well darts; a darting one is not made to flinch.
                if (!cast.Away || (heard != null && cast.Flinched)) Fright(cast, heard != null ? heard.transform.position : at, heard != null);
            }
            else if (cast.Away && Wary.ComesBack(now - cast.QuietSince, cast.Flinched))
            {
                ComeBack(cast);
            }
            return cast.Away || cast.Spooked;
        }

        /// <summary>The player heard by a school at a place: the game's noise ranges, halved under water.</summary>
        private static Player HeardAt(Vector3 place)
        {
            foreach (var player in Player.GetAllPlayers())
            {
                if (player != null && Wary.Heard(Vector3.Distance(player.transform.position, place), player.GetNoiseRange())) return player;
            }
            return null;
        }

        /// <summary>
        /// A fright: a startle by noise darts the school a few metres off the noise, a flinch at the
        /// float sidesteps it off the float; enough frights drive it off. No fish of it goes for the
        /// bait meanwhile.
        /// </summary>
        private static void Fright(Cast cast, Vector3 from, bool heard)
        {
            cast.Fright += Wary.Fright(heard);
            StartledForTest++;
            cast.BumpsAt.Clear();
            cast.Biter = -1;
            cast.BiterFreeAt = float.MaxValue;
            cast.ReadyAt = -1f;
            if (Wary.Leaves(cast.Fright))
            {
                DriveOff(cast, from);
                return;
            }
            cast.Away = true;
            cast.Flinched = !heard;
            foreach (var fish in cast.Fish)
            {
                if (fish != null) Sidestep(cast, fish, from, heard ? Wary.DartDistance : Wary.FlinchDistance);
            }
        }

        /// <summary>The school leaves for good, diving out of sight as a school whose float is gone.</summary>
        private static void DriveOff(Cast cast, Vector3 from)
        {
            cast.Spooked = true;
            foreach (var fish in cast.Fish.ToList())
            {
                if (fish != null && !fish.IsHooked()) Leave(fish, from);
            }
            cast.Fish.RemoveAll(f => f == null || !f.IsHooked());
        }

        /// <summary>A fish swims quickly a few metres away from what frightened it, where the water lets it, and waits there.</summary>
        private static void Sidestep(Cast cast, Fish fish, Vector3 from, float distance)
        {
            var view = fish.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            var place = fish.transform.position + Flat(fish.transform.position - from) * distance;
            if (!DeepEnough(place, ZoneSystem.instance.m_waterLevel, fish.m_minDepth)) place = fish.transform.position;
            SpawnPoint(fish) = place;
            view.GetZDO().Set(ZDOVars.s_spawnPoint, place);
            fish.m_swimRange = 1.5f;
            if (cast.OwnSpeed > 0f) fish.m_speed = cast.OwnSpeed * fish.m_avoidSpeedScale;
            SwimTimer(fish) = 0f;
        }

        /// <summary>All quiet again: the school swims back in from where it waited, as it swam in first.</summary>
        private static void ComeBack(Cast cast)
        {
            cast.Away = false;
            cast.Flinched = false;
            var from = Centre(cast.Fish);
            cast.Start = from;
            cast.StartUnder = cast.SwimUnder;
            cast.CameAt = Time.time;
            cast.SwimSeconds = Drift.Swim(Across(from, cast.Float.transform.position), 0f, cast.OwnSpeed);
            cast.In = false;
        }

        /// <summary>
        /// A school ready to bite close to the one fishing or in shallow water is shy: once, as it is
        /// first ready, it may give up and swim off; otherwise it hesitates the longer the shyer it is.
        /// True while it hesitates or once it gave up.
        /// </summary>
        private static bool Hesitates(Cast cast)
        {
            if (cast.ReadyAt < 0f)
            {
                cast.ReadyAt = Time.time;
                var at = cast.Float.transform.position;
                var angler = Player.m_localPlayer;
                cast.Shyness = Wary.Shyness(angler != null ? Across(angler.transform.position, at) : Wary.FarOut, WaterDepth(at));
                if (Wary.GivesUp(cast.Shyness, Random.value))
                {
                    GaveUpForTest++;
                    cast.Spooked = true;
                    foreach (var fish in cast.Fish.ToList()) Leave(fish, at);
                    cast.Fish.Clear();
                    return true;
                }
            }
            return Time.time - cast.ReadyAt < Wary.Hesitation(cast.Shyness);
        }

        /// <summary>Where a school's fish are, on average.</summary>
        private static Vector3 Centre(List<Fish> fish)
        {
            var here = fish.Where(f => f != null).ToList();
            if (here.Count == 0) return Vector3.zero;
            var sum = Vector3.zero;
            foreach (var f in here) sum += f.transform.position;
            return sum / here.Count;
        }

        /// <summary>
        /// Moves the school's home from where it appeared toward the float over its swim, its fish
        /// wandering widely around it at one slow pace and a school that started deep rising to its
        /// own depth, until it reaches the float and each fish swims at its own speed. The game takes
        /// a new fish's home from its saved data once, on its first frame, so a fish whose home jumped
        /// from the school's then picks a new place to swim at once.
        /// </summary>
        private static void DriftIn(Cast cast)
        {
            var share = Drift.Share(Time.time - cast.CameAt, cast.SwimSeconds);
            var home = Vector3.Lerp(cast.Start, cast.Float.transform.position, share);
            var under = Drift.Depth(share, cast.StartUnder, cast.SwimUnder);
            foreach (var fish in cast.Fish)
            {
                if (Fled.Contains(fish)) continue;
                if (Across(SpawnPoint(fish), home) > 1f) SwimTimer(fish) = 0f;
                SpawnPoint(fish) = home;
                fish.m_swimRange = Drift.SwimRange(share, CircleRange);
                fish.m_speed = Drift.Speed(cast.OwnSpeed, share);
                if (cast.StartUnder > cast.SwimUnder)
                {
                    fish.m_minDepth = under;
                    fish.m_maxDepth = under + 0.5f;
                }
            }
            cast.In = share >= 1f;
        }

        /// <summary>
        /// What comes once the wait is over: a fish of the world's own called, a school appearing loose
        /// out in the water, one kind of the bait's fish as many as the world spawns together, a wait
        /// for the caster's last school to be gone, or word that the water is too little for a school.
        /// </summary>
        private static void Come(Cast cast)
        {
            var book = FishBook.Current;
            var bait = cast.Float.GetBait();
            if (book == null)
            {
                cast.Came = true;
                return;
            }
            var home = cast.Float.transform.position;
            var surface = home.y;

            // The fish is rolled among those of the bait's fish the water here fits, so a bait two fish
            // take never turns a cast away for the one that does not fit while the other would.
            var starts = new Dictionary<string, StartPlace?>();
            bool Fits(string candidate)
            {
                var fish = book.Prefab(candidate)?.GetComponent<Fish>();
                if (fish == null) return false;
                var place = StartFor(home, surface, fish.m_minDepth);
                var fits = place.HasValue && HasWayOut(home, surface, fish.m_minDepth);
                starts[candidate] = fits ? place : null;
                return fits;
            }
            var roll = Random.value;
            var kind = book.Baits.Choose(bait, roll, Fits) ?? book.Baits.Choose(bait, roll);
            var prefab = book.Prefab(kind);
            if (prefab == null)
            {
                cast.Came = true;
                return;
            }
            var template = prefab.GetComponent<Fish>();
            var worldFish = Callable(cast.Float, bait);
            var start = starts.TryGetValue(kind, out var found) ? found : null;
            Hint(cast, book, bait, starts, worldFish);
            var water = Arrival.WaterAllowsSchool(start.HasValue, start.HasValue);
            switch (Arrival.Choose(worldFish != null, water, GoneSince.Count == 0, Rules))
            {
                case ArrivalChoice.Call:
                    Call(cast, worldFish);
                    return;
                case ArrivalChoice.Wait:
                    cast.DueAt = Time.time + 1f;
                    return;
                case ArrivalChoice.TooLittleWater:
                    cast.Came = true;
                    TooLittleWaterForTest++;
                    Log.Info("No school for " + kind + ": the water by the float gives it no place to come from or no way out.");
                    if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, TooLittleWaterText);
                    return;
            }

            cast.Came = true;
            cast.CameAt = Time.time;

            var at = start.Value.At;
            var depth = start.Value.Under;
            cast.Start = at;
            cast.OwnSpeed = template.m_speed;
            cast.StartUnder = depth;
            cast.SwimUnder = template.m_minDepth;
            cast.SwimSeconds = Drift.Swim(Across(at, home), cast.Wait, template.m_speed);
            var size = School.Size(book.Group(kind), Rules, Random.value);
            for (var i = 0; i < size; i++)
            {
                // Each fish where the water is deep enough for it, or else at the school's centre, which is.
                var offset = Random.insideUnitCircle * Drift.Spread;
                var facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var place = new Vector3(at.x + offset.x, surface - depth, at.z + offset.y);
                if (!DeepEnough(place, surface, depth)) place = new Vector3(at.x, surface - depth, at.z);
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
                SpawnPoint(fish) = at;
                fish.m_swimRange = Drift.SwimRange(0f, CircleRange);
                fish.m_speed = Drift.Speed(cast.OwnSpeed, 0f);
                fish.m_wpDurationMin = RepickMin;
                fish.m_wpDurationMax = RepickMax;
                fish.m_maxDepth = Mathf.Min(fish.m_maxDepth, fish.m_minDepth + 0.5f);
                if (depth > fish.m_minDepth)
                {
                    // Started deep: it swims as deep as it started, and rises over the swim in.
                    fish.m_minDepth = depth;
                    fish.m_maxDepth = depth + 0.5f;
                }
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
        /// With cast hints on, the cast says once which of the bait's fish the water here fits, which
        /// would need bigger or deeper water, or which fish of the world's own is called.
        /// </summary>
        private static void Hint(Cast cast, FishBook book, string bait, Dictionary<string, StartPlace?> starts, Fish called)
        {
            if (cast.Hinted || !Settings.CastHints || Player.m_localPlayer == null) return;
            cast.Hinted = true;
            var here = new List<string>();
            var better = new List<string>();
            foreach (var kind in book.Baits.FishFor(bait))
            {
                var name = ShownName(book.Prefab(kind)?.GetComponent<Fish>());
                if (name == null) continue;
                if (starts.TryGetValue(kind, out var start) && start.HasValue) here.Add(name);
                else better.Add(name);
            }
            var calledName = called != null && Rules.CallWorldFish ? ShownName(called) : null;
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, CastHint.Text(here, better, calledName));
        }

        /// <summary>A fish's name as the player reads it, in the game's language.</summary>
        private static string ShownName(Fish fish) =>
            fish == null ? null : Localization.instance != null ? Localization.instance.Localize(fish.m_name) : fish.m_name;

        /// <summary>A fish of the world's own is called to the float: it goes for it at its next choice, hearing nobody meanwhile.</summary>
        private static void Call(Cast cast, Fish fish)
        {
            cast.Came = true;
            cast.CameAt = Time.time;
            cast.In = true;
            cast.Called = fish;
            CalledBy[fish] = cast;
            SwimTimer(fish) = 0f;
            CalledForTest++;
        }

        /// <summary>
        /// The nearest fish of the world's own this game steers that takes this bait, within the
        /// float's reach, and can swim to the float through water deep enough for it all the way;
        /// null for none. A fish another game steers is left to it.
        /// </summary>
        private static Fish Callable(FishingFloat ff, string bait)
        {
            var at = ff.transform.position;
            var surface = ZoneSystem.instance.m_waterLevel;
            Fish best = null;
            var bestDistance = float.MaxValue;
            foreach (var fish in Fish.Instances.OfType<Fish>())
            {
                if (fish == null || IsOurs(fish) || IsCalled(fish) || !FloatOf(fish).IsNone() || fish.IsHooked() || fish.IsOutOfWater()) continue;
                var view = fish.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                if (!fish.m_baits.Any(b => b.m_bait != null && b.m_bait.name == bait)) continue;
                var distance = Vector3.Distance(fish.transform.position, at);
                if (distance > ff.m_range || distance >= bestDistance) continue;
                var across = Across(fish.transform.position, at);
                if (across > 0.5f && !DeepAlong(fish.transform.position, Flat(at - fish.transform.position), across, surface, fish.m_minDepth)) continue;
                best = fish;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// Whether a school leaving this float has a way out of sight through water deep enough for
        /// it: away as far as a leaving fish must be from every player, or to water deep enough to
        /// dive under, the whole way.
        /// </summary>
        private static bool HasWayOut(Vector3 home, float surface, float minDepth)
        {
            var rules = Rules;
            for (var step = 0; step < 16; step++)
            {
                var angle = step * Mathf.PI / 8f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (DeepAlong(home, dir, rules.OutOfSight, surface, minDepth)) return true;
                for (var reach = GoTo; reach >= GoShortest; reach -= 10f)
                {
                    if (DeepAlong(home, dir, reach, surface, minDepth) && DeepEnough(home + dir * reach, surface, rules.DiveDepth + 0.5f)) return true;
                }
            }
            return false;
        }

        /// <summary>Where a school appears, and how far under the surface.</summary>
        private struct StartPlace
        {
            public Vector3 At;
            public float Under;
        }

        /// <summary>
        /// A place out of sight for a school to come from, beyond the float as seen from the one
        /// fishing, through water deep enough for its fish all the way in: the nearest place where
        /// the whole school is 40 m from every player, at its own depth; else 6 to 14 m out where the
        /// water lets it swim 4 m under the surface. None where neither is.
        /// </summary>
        private static StartPlace? StartFor(Vector3 home, float surface, float minDepth)
        {
            if (!DeepEnough(home, surface, minDepth) || Player.m_localPlayer == null) return null;
            var rules = Rules;
            var away = Flat(home - Player.m_localPlayer.transform.position);
            var own = minDepth + 0.25f;
            foreach (var turn in BeyondTurns)
            {
                var dir = Quaternion.Euler(0f, turn, 0f) * away;
                for (var distance = 2f; distance <= FarthestStart; distance += 2f)
                {
                    var p = home + dir * distance;
                    if (!DeepEnough(p, surface, minDepth)) break;
                    if (Arrival.StartOutOfSight(NearestPlayer(p) - Drift.Spread, own, rules)) return new StartPlace { At = p, Under = own };
                }
            }
            var deep = rules.DiveDepth;
            foreach (var turn in BeyondTurns)
            {
                var dir = Quaternion.Euler(0f, turn, 0f) * away;
                for (var distance = 6f; distance <= 14f; distance += 2f)
                {
                    if (!DeepAlong(home, dir, distance, surface, minDepth)) break;
                    var p = home + dir * distance;
                    if (DeepEnough(p, surface, deep) && Arrival.StartOutOfSight(NearestPlayer(p), deep, rules)) return new StartPlace { At = p, Under = deep };
                }
            }
            return null;
        }

        /// <summary>The turns from straight beyond the float a start is looked for in, nearest first, in degrees.</summary>
        private static readonly float[] BeyondTurns = { 0f, 20f, -20f, 40f, -40f, 60f, -60f };

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
            // The whole way must be deep, or a fish heading for water beyond a spit of land crosses it;
            // where no long way is, a shorter one does.
            for (var reach = GoTo; reach >= GoShortest; reach -= 10f)
            {
                var bestDot = float.MinValue;
                for (var step = 0; step < 16; step++)
                {
                    var angle = step * Mathf.PI / 8f;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    if (!DeepAlong(from, dir, reach, surface, depth)) continue;
                    var dot = Vector3.Dot(dir, away);
                    if (dot <= bestDot) continue;
                    bestDot = dot;
                    place = from + dir * reach;
                }
                if (bestDot > float.MinValue) return true;
            }
            return false;
        }

        /// <summary>The shortest way a fleeing fish is sent, in metres.</summary>
        private const float GoShortest = 10f;

        /// <summary>
        /// Whether a fish reaches this place through water deep enough for it every metre of the way:
        /// as deep as it swims at its float for a school's fish on its way in, which may still be rising
        /// from deep, else as deep as it swims now. A fish already in shallower water than that may
        /// take any way that never gets shallower than where it is, so it can swim back out.
        /// </summary>
        public static bool DeepWayTo(Fish fish, Vector3 place)
        {
            var from = fish.transform.position;
            var across = Across(from, place);
            if (across < 0.5f) return true;
            var need = CastOf.TryGetValue(fish, out var cast) && !cast.In ? Mathf.Min(fish.m_minDepth, cast.SwimUnder) : fish.m_minDepth;
            var floor = Mathf.Min(need + 0.5f, WaterDepth(from) - 0.1f);
            var dir = Flat(place - from);
            for (var distance = 1f; distance < across; distance += 1f)
            {
                if (WaterDepth(from + dir * distance) < floor) return false;
            }
            return WaterDepth(place) >= floor;
        }

        /// <summary>How deep the water is at a place, from the surface to the ground; none on land.</summary>
        private static float WaterDepth(Vector3 p) =>
            ZoneSystem.instance.GetSolidHeight(p, out var ground) ? Mathf.Max(0f, ZoneSystem.instance.m_waterLevel - ground) : 0f;

        /// <summary>Whether the water is deep enough every 2 m of the way, out to its end.</summary>
        private static bool DeepAlong(Vector3 from, Vector3 dir, float reach, float surface, float depth)
        {
            for (var distance = 2f; distance < reach; distance += 2f)
            {
                if (!DeepEnough(from + dir * distance, surface, depth)) return false;
            }
            return DeepEnough(from + dir * reach, surface, depth);
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
            Trail.Remove(fish);
        }

        /// <summary>Notes where each of the schools' fish is, once a second, keeping the last few notes.</summary>
        private static void TakeSamples()
        {
            foreach (var fish in Ours)
            {
                if (fish == null) continue;
                if (!Trail.TryGetValue(fish, out var trail))
                {
                    trail = new List<Sample>();
                    Trail[fish] = trail;
                }
                trail.Add(Here(fish));
                if (trail.Count > TrailLength) trail.RemoveAt(0);
            }
        }

        private static Sample Here(Fish fish)
        {
            var at = fish.transform.position;
            return new Sample
            {
                At = Time.time,
                Player = NearestPlayer(at),
                Water = WaterDepth(at),
                Place = Across(at, FishWaypoint(fish)),
                Home = Across(at, SpawnPoint(fish)),
                ForFloat = WaypointFloat(fish) != null,
            };
        }

        private static string Told(Sample sample, float now)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return (now - sample.At).ToString("0", c) + " s before " + sample.Player.ToString("0.0", c) + " m from the nearest player over "
                + sample.Water.ToString("0.0", c) + " m of water, its place " + sample.Place.ToString("0", c) + " m and home " + sample.Home.ToString("0", c) + " m off"
                + (sample.ForFloat ? ", going for a float" : "");
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
            parts.Add("heading for a place " + Across(fish.transform.position, FishWaypoint(fish)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m off, its home "
                + Across(fish.transform.position, SpawnPoint(fish)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m off, at "
                + fish.m_speed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m/s");
            var here = Here(fish);
            parts.Add("now " + here.Player.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m from the nearest player over "
                + here.Water.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m of water");
            if (here.ForFloat) parts.Add("going for a float");
            if (Trail.TryGetValue(fish, out var trail))
            {
                for (var i = trail.Count - 1; i >= 0; i--) parts.Add(Told(trail[i], now));
            }
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
            Trail.Clear();
            CalledBy.Clear();
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
