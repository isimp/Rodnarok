using System;
using System.Collections.Generic;

namespace Rodnarok
{
    /// <summary>
    /// The one path a float's arrival sign walks, so the eye can follow it. The game steers each fish
    /// of a school to a new place every second or two, so the fish nearest the float, or one picked at
    /// random, jumps about; the path follows the school's centre instead, smoothed over
    /// <see cref="SchoolFollow"/>, and a fish going for the bait closer, over <see cref="GoingFollow"/>.
    /// It faces the way it moves and keeps that heading while still, and remembers where it has been
    /// for a wake. Once the school mills about at the float with no fish going for the bait, it is
    /// quiet: the travelling sign fades and showings come by the float only every
    /// <see cref="QuietEvery"/>; a fish going for the bait is followed in every <see cref="GoingEvery"/>.
    /// </summary>
    internal sealed class SignPath
    {
        /// <summary>Seconds the path takes to close most of the way to the school's centre.</summary>
        public const float SchoolFollow = 0.9f;

        /// <summary>Seconds the path takes to close most of the way to a fish going for the bait.</summary>
        public const float GoingFollow = 0.25f;

        /// <summary>How long a wake remembers where the path has been, in seconds, and how often it takes a point.</summary>
        public const float WakeSeconds = 1.6f;
        public const float TrailStep = 0.05f;

        /// <summary>Seconds between showings while the school mills about at the float, and while a fish goes for the bait.</summary>
        public const float QuietEvery = 2f;
        public const float GoingEvery = 0.9f;

        /// <summary>Seconds the travelling sign takes to fade out at the float and back in.</summary>
        public const float FadeSeconds = 0.5f;

        /// <summary>Speed in metres a second below which the path counts as still and keeps its heading.</summary>
        public const float Still = 0.05f;

        /// <summary>Speed in metres a second at which the wake shows in full.</summary>
        public const float FullWake = 1.2f;

        private readonly List<(Spot At, float Age)> _trail = new List<(Spot At, float Age)>();
        private bool _started;
        private float _sinceTrail;

        /// <summary>Where the path is now.</summary>
        public Spot At { get; private set; }

        /// <summary>The way the path faces across the water, a unit direction in x and z.</summary>
        public float HeadX { get; private set; } = 1f;
        public float HeadZ { get; private set; }

        /// <summary>How fast the path moves, in metres a second.</summary>
        public float Speed { get; private set; }

        /// <summary>How much a travelling sign shows, 1 on the way in and for a fish going for the bait, fading to 0 while quiet at the float.</summary>
        public float Showing { get; private set; } = 1f;

        /// <summary>Whether the school mills about at the float with no fish going for the bait.</summary>
        public bool Quiet { get; private set; }

        /// <summary>Whether the path follows a fish going for the bait.</summary>
        public bool Going { get; private set; }

        /// <summary>Where the path has been, newest first, each with its age in seconds.</summary>
        public IReadOnlyList<(Spot At, float Age)> Trail => _trail;

        /// <summary>The centre of a school's fish across the water, the mean of their places.</summary>
        public static Spot Centre(IEnumerable<Spot> fish)
        {
            float x = 0f, y = 0f, z = 0f;
            var n = 0;
            foreach (var f in fish)
            {
                x += f.X;
                y += f.Y;
                z += f.Z;
                n++;
            }
            return n == 0 ? default : new Spot(x / n, y / n, z / n);
        }

        /// <summary>How far to the side of the path a wake point lies at an age in seconds, in metres: it spreads behind the sign.</summary>
        public static float WakeSide(float age) => 0.2f + 0.6f * Math.Max(0f, age);

        /// <summary>How much of the wake shows at a speed: none when still, all from <see cref="FullWake"/>.</summary>
        public static float Moving(float speed) => Clamp01(speed / FullWake);

        /// <summary>
        /// One step of the path toward where the sign belongs: the school's centre, or the fish going
        /// for the bait when one is. The first step starts it there.
        /// </summary>
        public void Step(Spot target, bool going, Spot floatAt, float seconds)
        {
            if (seconds <= 0f) return;
            Going = going;
            if (!_started)
            {
                _started = true;
                At = target;
            }
            else
            {
                var k = 1f - (float)Math.Exp(-seconds / (going ? GoingFollow : SchoolFollow));
                var was = At;
                At = new Spot(was.X + (target.X - was.X) * k, was.Y + (target.Y - was.Y) * k, was.Z + (target.Z - was.Z) * k);
                var dx = At.X - was.X;
                var dz = At.Z - was.Z;
                var moved = (float)Math.Sqrt(dx * dx + dz * dz);
                Speed = moved / seconds;
                if (Speed >= Still)
                {
                    HeadX = dx / moved;
                    HeadZ = dz / moved;
                }
            }

            Quiet = !going && At.Across(floatAt) <= ArrivalSign.There;
            var fade = 1f - (float)Math.Exp(-seconds / FadeSeconds);
            Showing += ((Quiet ? 0f : 1f) - Showing) * fade;

            for (var i = _trail.Count - 1; i >= 0; i--)
            {
                var aged = _trail[i].Age + seconds;
                if (aged > WakeSeconds) _trail.RemoveAt(i);
                else _trail[i] = (_trail[i].At, aged);
            }
            _sinceTrail += seconds;
            if (_trail.Count == 0 || _sinceTrail >= TrailStep)
            {
                _trail.Insert(0, (At, 0f));
                _sinceTrail = 0f;
            }
        }

        /// <summary>Seconds to the next showing at a nearness of the school.</summary>
        public float Every(float nearness) => Going ? GoingEvery : Quiet ? QuietEvery : ArrivalSign.Every(nearness);

        /// <summary>Where the next showing comes: by the float while quiet, on the path otherwise.</summary>
        public Spot ShowAt(Spot floatAt) => Quiet ? floatAt : At;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
