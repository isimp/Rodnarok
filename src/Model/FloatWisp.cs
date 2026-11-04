using System;

namespace Rodnarok
{
    /// <summary>What a Wisplight's wisp does about its player's float.</summary>
    internal enum WispTask
    {
        /// <summary>No float out: the wisp follows its player as the game has it.</summary>
        Home,

        /// <summary>The float waits: the wisp drifts round it, searching, now and then dipping low.</summary>
        Search,

        /// <summary>A bite in the hook window: the wisp is in close over the float, flaring.</summary>
        Focus,

        /// <summary>A fish on the line: the wisp follows the float close.</summary>
        Follow,

        /// <summary>The float gone: the wisp drifts back to its player until the game's own following can take it.</summary>
        Return,
    }

    /// <summary>
    /// Where a Wisplight's wisp goes while its player fishes, as offsets from the float, how hard
    /// it is drawn there and how bright it shines. The game's own following carries the wisp to
    /// a place over the player; while a float is out the place is near the float instead.
    /// </summary>
    internal static class FloatWisp
    {
        /// <summary>Whether a Wisplight's wisp guards the float before a player switches it off.</summary>
        public const bool OnByDefault = true;

        /// <summary>How far round the float the searching wisp drifts, in metres.</summary>
        public const float SearchRadius = 1.5f;

        /// <summary>How high over the water the searching wisp drifts, in metres, rising and falling by <see cref="SearchBob"/>.</summary>
        public const float SearchHeight = 1.4f;

        public const float SearchBob = 0.35f;

        /// <summary>How fast the searching wisp goes round the float, in radians a second.</summary>
        public const float SearchTurn = 0.7f;

        /// <summary>The searching wisp dips once every this many seconds, for <see cref="DipLength"/>, down to <see cref="DipLow"/> over the water.</summary>
        public const float DipEvery = 4f;

        public const float DipLength = 1.1f;

        public const float DipLow = 0.4f;

        /// <summary>How high over the float the wisp holds at a bite, in metres.</summary>
        public const float FocusHeight = 0.45f;

        /// <summary>How high over the float the wisp follows it through a fight, in metres.</summary>
        public const float FollowHeight = 0.8f;

        /// <summary>How bright the wisp shines at the moment of a bite and through a fight, against its own light.</summary>
        public const float FlareStart = 2.2f;

        public const float FollowBright = 1.3f;

        /// <summary>
        /// Within this many metres of its place over the player the wisp is handed back to the
        /// game's own following: the Wisplight's own leash, past which the game would snap it there.
        /// </summary>
        public const float HandBack = 6f;

        /// <summary>
        /// How near its noiseless place over the player the wisp is handed back, in metres: the
        /// Wisplight's leash less how far the game's place drifts round it, so the game's first step
        /// never drags the wisp to its leash.
        /// </summary>
        public static float HandBackWithin(float leash, float drift) => Math.Max(0.5f, leash - drift);

        /// <summary>How wide the wisp's own clearing round the float is, in metres: the game's clearing where wisps gather.</summary>
        public const float PocketRange = 6f;

        /// <summary>
        /// Whether the wisp guards the float: while it is out and a school is at it or a fish is on the
        /// line; once the school has left with nothing hooked, the wisp comes back to the player.
        /// </summary>
        public static bool Guards(bool floatOut, bool schoolLeft, bool hooked) => floatOut && (hooked || !schoolLeft);

        public static WispTask Task(bool floatOut, bool biting, bool hooked)
        {
            if (!floatOut) return WispTask.Home;
            if (hooked) return WispTask.Follow;
            return biting ? WispTask.Focus : WispTask.Search;
        }

        /// <summary>Where the wisp is drawn, from the float, at a time in seconds.</summary>
        public static (float X, float Y, float Z) Offset(WispTask task, float time)
        {
            switch (task)
            {
                case WispTask.Search:
                    var angle = time * SearchTurn;
                    var radius = SearchRadius * (0.75f + 0.25f * (float)Math.Sin(time * 0.45f));
                    var y = SearchHeight + SearchBob * (float)Math.Sin(time * 1.3f);
                    var into = time % DipEvery - (DipEvery - DipLength);
                    if (into > 0f)
                    {
                        var dip = (float)Math.Sin(Math.PI * into / DipLength);
                        y += (DipLow - y) * dip;
                    }
                    return (radius * (float)Math.Cos(angle), y, radius * (float)Math.Sin(angle));
                case WispTask.Focus:
                    return (0.05f * (float)Math.Sin(time * 9f), FocusHeight, 0.05f * (float)Math.Cos(time * 9f));
                case WispTask.Follow:
                    return (0.2f * (float)Math.Sin(time * 1.7f), FollowHeight, 0.2f * (float)Math.Cos(time * 1.1f));
                default:
                    return (0f, 0f, 0f);
            }
        }

        /// <summary>How hard the wisp is drawn to its place, against the game's own drawing.</summary>
        public static float Pull(WispTask task)
        {
            switch (task)
            {
                case WispTask.Focus: return 3f;
                case WispTask.Follow: return 2f;
                case WispTask.Return: return 1.5f;
                default: return 1f;
            }
        }

        /// <summary>How bright the wisp shines against its own light, a bite's flare fading over the hook window.</summary>
        public static float Brightness(WispTask task, float sinceBite, float window)
        {
            switch (task)
            {
                case WispTask.Focus:
                    if (!(sinceBite >= 0f) || !(window > 0f) || sinceBite >= window) return FollowBright;
                    return FlareStart + (FollowBright - FlareStart) * (sinceBite / window);
                case WispTask.Follow:
                    return FollowBright;
                default:
                    return 1f;
            }
        }
    }

    /// <summary>
    /// A wisp moving as the game moves a Wisplight's: drawn toward its place at a steady
    /// acceleration, held under a top speed, and slowed by a share of its speed every physics
    /// step of 0.02 s.
    /// </summary>
    internal struct WispMotion
    {
        private const float GameStep = 0.02f;

        public float X, Y, Z, VX, VY, VZ;

        public WispMotion(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
            VX = VY = VZ = 0f;
        }

        public void Step(float toX, float toY, float toZ, float acceleration, float topSpeed, float friction, float pull, float seconds)
        {
            var dx = toX - X;
            var dy = toY - Y;
            var dz = toZ - Z;
            var distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (distance > 1e-5f)
            {
                var push = acceleration * pull * seconds / distance;
                VX += dx * push;
                VY += dy * push;
                VZ += dz * push;
            }
            var speed = (float)Math.Sqrt(VX * VX + VY * VY + VZ * VZ);
            if (speed > topSpeed)
            {
                var cap = topSpeed / speed;
                VX *= cap;
                VY *= cap;
                VZ *= cap;
            }
            var keep = (float)Math.Pow(Math.Max(0f, 1f - friction * pull), seconds / GameStep);
            VX *= keep;
            VY *= keep;
            VZ *= keep;
            X += VX * seconds;
            Y += VY * seconds;
            Z += VZ * seconds;
        }
    }
}
