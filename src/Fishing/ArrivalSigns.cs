using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Rodnarok
{
    /// <summary>
    /// Every game shows the sign of each fish coming to a float (<see cref="ArrivalSign"/>), read
    /// from the mark the game steering the fish puts on it (<see cref="Schools.ComingTo"/>), so
    /// every player at the water sees what comes. A float's coming fish show one sign together, on
    /// one path (<see cref="SignPath"/>): the school's way in, quiet while it mills about at the
    /// float, then the fish going for the bait, more often and stronger as they near the float.
    /// The signs are drawn on the water's surface without the scene's
    /// light, white by day and dimmer and moonlit by night as the game's night light weighs.
    /// </summary>
    internal static class ArrivalSigns
    {
        private const int RingPoints = 28;
        private const int SpritePixels = 32;

        /// <summary>A part of a sign on the water, living a while.</summary>
        private sealed class Piece
        {
            public GameObject Go;
            public float Age;
            public float Life;
            public float Delay;
            public Action<Piece, float> Show;
        }

        /// <summary>What a float's coming fish show.</summary>
        private sealed class Coming
        {
            public ArrivalSignKind Kind;
            public float NextAt;
            public bool Swirled;
            public GameObject Lasting;
            public int Shown;
            public float Nearness;
            public Vector3 ShownAt;
            public readonly SignPath Path = new SignPath();
            public readonly List<Fish> Fish = new List<Fish>();
            public readonly List<Spot> Places = new List<Spot>();
            public readonly Vector3[] Wake = new Vector3[WakePoints];
        }

        private const int WakePoints = (int)(SignPath.WakeSeconds / SignPath.TrailStep) + 2;

        private static readonly Dictionary<ZDOID, Coming> Floats = new Dictionary<ZDOID, Coming>();
        private static readonly List<Piece> Pieces = new List<Piece>();
        private static readonly List<ZDOID> Gone = new List<ZDOID>();
        private static readonly HashSet<ZDOID> Seen = new HashSet<ZDOID>();
        private static Material _material;
        private static Sprite _dot;
        private static Mesh _fin;
        private static float _darkness;

        /// <summary>The sign a float's coming fish show and how many times it showed, for the self-test; Rings and 0 for none.</summary>
        [ForTest]
        public static (ArrivalSignKind Kind, int Shown, float Nearness) ForTest(ZDOID floatId) =>
            Floats.TryGetValue(floatId, out var coming) ? (coming.Kind, coming.Shown, coming.Nearness) : (ArrivalSignKind.Rings, 0, 0f);

        /// <summary>
        /// A float's sign path for the self-test: whether it is quiet at the float or follows a fish
        /// going for the bait, where it is and faces, how much a travelling sign shows, and where the
        /// last showing came; null for no float with coming fish.
        /// </summary>
        [ForTest]
        public static (bool Quiet, bool Going, Vector3 At, Vector3 Heading, float Showing, Vector3 ShownAt)? PathForTest(ZDOID floatId)
        {
            if (!Floats.TryGetValue(floatId, out var coming)) return null;
            var path = coming.Path;
            return (path.Quiet, path.Going, new Vector3(path.At.X, path.At.Y, path.At.Z), new Vector3(path.HeadX, 0f, path.HeadZ), path.Showing, coming.ShownAt);
        }

        /// <summary>Each frame: the coming fish of every float show their signs, and the parts of signs shown age.</summary>
        public static void Tick()
        {
            var env = EnvMan.instance;
            _darkness = env != null ? ArrivalSign.Darkness(env.GetDayFraction()) : 0f;
            foreach (var coming in Floats.Values) coming.Fish.Clear();
            Seen.Clear();
            foreach (var any in Fish.Instances)
            {
                var fish = any as Fish;
                if (fish == null) continue;
                var to = Schools.ComingTo(fish);
                if (to.IsNone()) continue;
                if (!Floats.TryGetValue(to, out var coming))
                {
                    coming = new Coming { Kind = ArrivalSign.Of(Utils.GetPrefabName(fish.gameObject)), NextAt = Time.time + 1f };
                    Floats[to] = coming;
                }
                coming.Fish.Add(fish);
                Seen.Add(to);
            }

            Gone.Clear();
            foreach (var pair in Floats)
            {
                var floatObject = Seen.Contains(pair.Key) && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(pair.Key) : null;
                if (floatObject == null)
                {
                    Gone.Add(pair.Key);
                    continue;
                }
                Step(pair.Value, floatObject.transform.position, pair.Key);
            }
            foreach (var id in Gone) End(id);
            Age(Time.deltaTime);
        }

        /// <summary>
        /// One frame of a float's sign: its path steps to the school's centre, or to the fish going
        /// for the bait when one is marked; a travelling sign rides the path, and showings come on
        /// it, or by the float while the school mills about there.
        /// </summary>
        private static void Step(Coming coming, Vector3 at, ZDOID floatId)
        {
            var nearest = float.MaxValue;
            Fish going = null;
            coming.Places.Clear();
            foreach (var fish in coming.Fish)
            {
                var place = fish.transform.position;
                nearest = Mathf.Min(nearest, Across(place, at));
                coming.Places.Add(new Spot(place.x, place.y, place.z));
                if (going == null && Schools.GoingFor(fish) == floatId) going = fish;
            }
            if (coming.Places.Count == 0) return;
            var nearness = ArrivalSign.Nearness(nearest);
            coming.Nearness = nearness;
            var target = going != null ? new Spot(going.transform.position.x, going.transform.position.y, going.transform.position.z) : SignPath.Centre(coming.Places);
            var floatSpot = new Spot(at.x, at.y, at.z);
            var path = coming.Path;
            path.Step(target, going != null, floatSpot, Time.deltaTime);
            var pathAt = new Vector3(path.At.X, path.At.Y, path.At.Z);
            var surface = Surface(pathAt);

            // A sign that travels with the school rides the path, fading while the school mills about at the float.
            var lasts = Lasts(coming.Kind);
            if (lasts)
            {
                if (coming.Lasting == null) coming.Lasting = Lasting(coming.Kind);
                Place(coming, pathAt, nearness, surface);
                coming.Shown = Math.Max(coming.Shown, 1);
            }
            if (Time.time < coming.NextAt) return;
            if (lasts && !path.Quiet) return;
            if (!lasts && !ArrivalSign.Shows(coming.Kind, nearness)) return;
            if (coming.Kind == ArrivalSignKind.Swirl)
            {
                if (coming.Swirled) return;
                coming.Swirled = true;
            }
            var show = path.ShowAt(floatSpot);
            var showAt = path.Quiet
                ? Jitter(new Vector3(show.X, show.Y, show.Z), 0.4f)
                : Jitter(new Vector3(show.X, show.Y, show.Z), 0.15f);
            coming.ShownAt = showAt;
            // A travelling sign, faded at the float, leaves a slow ring there that the school is in.
            if (lasts) Ring(new Vector3(showAt.x, Surface(showAt) + 0.02f, showAt.z), 1.1f, 1.6f, ArrivalSign.Strength(nearness) * 0.8f, 0f);
            else Show(coming, showAt, at, nearness, Surface(showAt));
            coming.Shown++;
            coming.NextAt = Time.time + (path.Quiet ? SignPath.QuietEvery
                : coming.Kind == ArrivalSignKind.Fizz ? 0.08f + 0.25f * (1f - nearness)
                : path.Going ? SignPath.GoingEvery
                : coming.Kind == ArrivalSignKind.Dome ? path.Every(nearness) * 1.5f
                : path.Every(nearness));
        }

        private static bool Lasts(ArrivalSignKind kind) => kind == ArrivalSignKind.Bulge || kind == ArrivalSignKind.Glow || kind == ArrivalSignKind.Fin;

        /// <summary>One showing of a sign at a place by the school, toward the float.</summary>
        private static void Show(Coming coming, Vector3 by, Vector3 floatAt, float nearness, float surface)
        {
            var strength = ArrivalSign.Strength(nearness);
            var at = new Vector3(by.x, surface + 0.02f, by.z);
            switch (coming.Kind)
            {
                case ArrivalSignKind.Rings:
                    Ring(at, 1.1f, 1.3f, strength, 0f);
                    break;
                case ArrivalSignKind.Flicker:
                    for (var i = 0; i < 5; i++) Ring(Jitter(at, 1.4f), 0.45f, 0.7f, strength, Random.Range(0f, 0.15f));
                    break;
                case ArrivalSignKind.Swirl:
                    var by2 = Vector3.MoveTowards(new Vector3(floatAt.x, at.y, floatAt.z), at, 0.6f);
                    Ring(by2, 1.4f, 1.6f, 1f, 0f, 0.1f);
                    Ring(by2, 0.7f, 1.6f, 1f, 0.1f, 0.08f);
                    Blob(by2, new Vector3(1.3f, 0.5f, 1f), new Color(0.05f, 0.1f, 0.15f), 0.5f, 1.6f, true);
                    break;
                case ArrivalSignKind.Bloop:
                    var bloop = at;
                    Dot(bloop, 0.3f, 0.35f, 0.05f, strength, 0f, Color.white);
                    Ring(bloop, 0.85f, 0.8f, strength, 0.33f);
                    break;
                case ArrivalSignKind.Fizz:
                    for (var i = 0; i < 5; i++) Dot(Jitter(at, 0.8f), 0.1f, Random.Range(0.5f, 0.9f), 0.15f, strength, 0f, Color.white);
                    break;
                case ArrivalSignKind.Dome:
                    var dome = at;
                    Arc(dome, 0.55f, 1.5f, strength);
                    Ring(dome, 1.1f, 0.7f, strength, 1.5f);
                    break;
                case ArrivalSignKind.Steam:
                    Puff(at, strength);
                    break;
                case ArrivalSignKind.Boil:
                    for (var i = 0; i < 6; i++)
                    {
                        var spot = Jitter(at, 1.1f);
                        var delay = Random.Range(0f, 0.25f);
                        Splash(spot, Random.Range(0.3f, 0.55f), Random.Range(0.45f, 0.75f), strength, delay);
                        Ring(spot, 0.45f, 0.6f, strength * 0.7f, delay);
                    }
                    break;
                case ArrivalSignKind.Zigzag:
                    var dir = Mathf.Atan2(coming.Path.HeadZ, coming.Path.HeadX);
                    var glint = at;
                    for (var i = 0; i < 6; i++)
                    {
                        var turn = dir + (i % 2 == 0 ? 0.9f : -0.9f);
                        glint += new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * 0.45f;
                        Dot(glint, 0.13f, 0.4f, 0f, strength, i * 0.05f, new Color(0.9f, 0.96f, 1f));
                        Ring(glint, 0.25f, 0.4f, strength * 0.7f, i * 0.05f);
                    }
                    break;
            }
        }

        /// <summary>A sign that travels with the school: the Trollfish's bulge, the Anglerfish's glow, the salmon's fin.</summary>
        private static GameObject Lasting(ArrivalSignKind kind)
        {
            var go = new GameObject("RodnarokSign" + kind);
            switch (kind)
            {
                case ArrivalSignKind.Bulge:
                    var blob = new GameObject("Bulge").AddComponent<SpriteRenderer>();
                    blob.sprite = DotSprite();
                    blob.transform.SetParent(go.transform, false);
                    blob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    blob.transform.localScale = new Vector3(2.6f, 1f, 1f);
                    Wake(go, "WakeLeft");
                    Wake(go, "WakeRight");
                    break;
                case ArrivalSignKind.Glow:
                    var light = new GameObject("Lure").AddComponent<Light>();
                    light.transform.SetParent(go.transform, false);
                    light.type = LightType.Point;
                    light.shadows = LightShadows.None;
                    light.renderMode = LightRenderMode.ForcePixel;
                    light.color = new Color(0.82f, 1f, 0.67f);
                    var halo = new GameObject("Halo").AddComponent<SpriteRenderer>();
                    halo.sprite = DotSprite();
                    halo.transform.SetParent(go.transform, false);
                    halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case ArrivalSignKind.Fin:
                    var fin = new GameObject("Fin");
                    fin.transform.SetParent(go.transform, false);
                    fin.AddComponent<MeshFilter>().sharedMesh = FinMesh();
                    fin.transform.localScale = Vector3.one * 1.6f;
                    fin.AddComponent<MeshRenderer>().sharedMaterial = SignMaterial();
                    Wake(go, "Wake");
                    break;
            }
            return go;
        }

        /// <summary>
        /// Places a travelling sign on the path, facing the way the path moves, its wake drawn where
        /// the path has been and only while it moves; all of it as much as the path shows it.
        /// </summary>
        private static void Place(Coming coming, Vector3 pathAt, float nearness, float surface)
        {
            var go = coming.Lasting;
            var path = coming.Path;
            var showing = path.Showing;
            var strength = ArrivalSign.Strength(nearness) * showing;
            var moving = SignPath.Moving(path.Speed);
            var heading = new Vector3(path.HeadX, 0f, path.HeadZ);
            var at = new Vector3(pathAt.x, surface + 0.03f, pathAt.z);
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            switch (coming.Kind)
            {
                case ArrivalSignKind.Bulge:
                    var blob = go.transform.Find("Bulge").GetComponent<SpriteRenderer>();
                    blob.color = Tint(Color.white, 0.5f * strength * (0.4f + 0.6f * moving));
                    SetWake(go.transform.Find("WakeLeft"), coming, surface + 0.03f, -1f, 0.7f * strength * moving);
                    SetWake(go.transform.Find("WakeRight"), coming, surface + 0.03f, 1f, 0.7f * strength * moving);
                    break;
                case ArrivalSignKind.Glow:
                    var pulse = (0.75f + 0.25f * Mathf.Sin(Time.time * 3f)) * showing;
                    var light = go.GetComponentInChildren<Light>();
                    light.transform.position = at + Vector3.down * 0.6f;
                    light.intensity = (0.6f + 1.4f * nearness) * pulse;
                    light.range = 2f + 2f * nearness;
                    var halo = go.transform.Find("Halo").GetComponent<SpriteRenderer>();
                    halo.color = new Color(0.82f, 1f, 0.67f, (0.15f + 0.4f * nearness) * pulse);
                    halo.transform.localScale = Vector3.one * (0.8f + 0.8f * nearness);
                    break;
                case ArrivalSignKind.Fin:
                    // Breaking the surface now and then, as a fin does.
                    var up = Mathf.Sin(Time.time * 1.7f + 1f) > -0.2f && showing > 0.02f;
                    var fin = go.transform.Find("Fin");
                    fin.gameObject.SetActive(up);
                    // Dark against the day's water, by night a moonlit edge, so it shows on dark water too.
                    fin.GetComponent<MeshRenderer>().material.color = Tint(Color.Lerp(new Color(0.12f, 0.16f, 0.2f), new Color(0.5f, 0.6f, 0.7f), _darkness), 0.9f * showing, false);
                    SetWake(go.transform.Find("Wake"), coming, surface + 0.03f, 0f, up ? 0.6f * strength * moving : 0f);
                    break;
            }
        }

        private static void Wake(GameObject parent, string name)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent.transform, false);
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = 0.07f;
            line.sharedMaterial = SignMaterial();
        }

        /// <summary>
        /// A wake along where the path has been, newest point first: on one side of it, spreading as
        /// the point ages, or on the path itself for a side of 0.
        /// </summary>
        private static void SetWake(Transform wake, Coming coming, float y, float side, float alpha)
        {
            var line = wake.GetComponent<LineRenderer>();
            var trail = coming.Path.Trail;
            var n = Math.Min(trail.Count, coming.Wake.Length);
            for (var i = 0; i < n; i++)
            {
                var point = trail[i].At;
                // The way the path went at this point, from the older point behind it to the newer one ahead.
                var newer = trail[Math.Max(0, i - 1)].At;
                var older = trail[Math.Min(n - 1, i + 1)].At;
                var dx = newer.X - older.X;
                var dz = newer.Z - older.Z;
                var length = Mathf.Sqrt(dx * dx + dz * dz);
                if (length < 0.0001f)
                {
                    dx = coming.Path.HeadX;
                    dz = coming.Path.HeadZ;
                    length = 1f;
                }
                var off = side * SignPath.WakeSide(trail[i].Age);
                coming.Wake[i] = new Vector3(point.X - dz / length * off, y, point.Z + dx / length * off);
            }
            line.positionCount = n;
            if (n > 0) line.SetPositions(coming.Wake);
            var colour = Tint(Color.white, alpha);
            line.startColor = colour;
            line.endColor = new Color(colour.r, colour.g, colour.b, 0f);
        }

        // Parts of signs.

        private static void Ring(Vector3 at, float radius, float life, float strength, float delay, float width = 0.07f)
        {
            var line = new GameObject("RodnarokSignRing").AddComponent<LineRenderer>();
            line.transform.position = at;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = RingPoints;
            line.widthMultiplier = width;
            line.sharedMaterial = SignMaterial();
            line.enabled = false;
            Add(line.gameObject, life, delay, (p, q) =>
            {
                // Thin against its size from the start, so a young ring is a ring and not a disc.
                var r = radius * (0.25f + 0.75f * q);
                line.widthMultiplier = Mathf.Min(width, r * 0.12f);
                for (var i = 0; i < RingPoints; i++)
                {
                    var a = i * Mathf.PI * 2f / RingPoints;
                    line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
                var c = Tint(Color.white, 0.6f * strength * Mathf.Min(1f, q * 6f) * (1f - q));
                line.startColor = c;
                line.endColor = c;
                line.enabled = true;
            });
        }

        private static void Dot(Vector3 at, float size, float life, float rise, float strength, float delay, Color colour)
        {
            var dot = new GameObject("RodnarokSignDot").AddComponent<SpriteRenderer>();
            dot.sprite = DotSprite();
            dot.enabled = false;
            var start = at;
            Add(dot.gameObject, life, delay, (p, q) =>
            {
                dot.transform.position = start + Vector3.up * rise * q;
                dot.transform.localScale = Vector3.one * size;
                Face(dot.transform);
                dot.color = Tint(colour, 0.85f * strength * (1f - q));
                dot.enabled = true;
            });
        }

        private static void Puff(Vector3 at, float strength)
        {
            var puff = new GameObject("RodnarokSignSteam").AddComponent<SpriteRenderer>();
            puff.sprite = DotSprite();
            Add(puff.gameObject, 1.8f, 0f, (p, q) =>
            {
                puff.transform.position = at + new Vector3(0.1f * q, 1.2f * q, 0f);
                puff.transform.localScale = Vector3.one * (0.5f + 1.2f * q);
                Face(puff.transform);
                puff.color = Tint(new Color(0.92f, 0.92f, 0.92f), 0.65f * strength * (1f - q));
            });
        }

        private static void Splash(Vector3 at, float height, float life, float strength, float delay)
        {
            var line = new GameObject("RodnarokSignSplash").AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.07f;
            line.sharedMaterial = SignMaterial();
            line.enabled = false;
            var lean = new Vector3(Random.Range(-0.05f, 0.05f), 0f, Random.Range(-0.05f, 0.05f));
            Add(line.gameObject, life, delay, (p, q) =>
            {
                line.SetPosition(0, at);
                line.SetPosition(1, at + lean + Vector3.up * height * Mathf.Sin(q * Mathf.PI));
                var c = Tint(Color.white, 0.9f * strength * (1f - q));
                line.startColor = c;
                line.endColor = c;
                line.enabled = true;
            });
        }

        /// <summary>The Pufferfish's dome: a half ring standing on the water, facing the camera, swelling.</summary>
        private static void Arc(Vector3 at, float radius, float life, float strength)
        {
            var line = new GameObject("RodnarokSignDome").AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = RingPoints / 2 + 1;
            line.widthMultiplier = 0.07f;
            line.sharedMaterial = SignMaterial();
            Add(line.gameObject, life, 0f, (p, q) =>
            {
                var camera = Utils.GetMainCamera();
                var right = camera != null ? Flat(camera.transform.right) : Vector3.right;
                var r = 0.05f + radius * q;
                for (var i = 0; i <= RingPoints / 2; i++)
                {
                    var a = i * Mathf.PI / (RingPoints / 2);
                    line.SetPosition(i, at + right * Mathf.Cos(a) * r + Vector3.up * Mathf.Sin(a) * r * 0.8f);
                }
                var c = Tint(Color.white, 0.8f * strength);
                line.startColor = c;
                line.endColor = c;
            });
        }

        private static void Blob(Vector3 at, Vector3 scale, Color colour, float alpha, float life, bool dark)
        {
            var blob = new GameObject("RodnarokSignBlob").AddComponent<SpriteRenderer>();
            blob.sprite = DotSprite();
            blob.transform.position = at;
            blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Add(blob.gameObject, life, 0f, (p, q) =>
            {
                blob.transform.localScale = scale * (1f + q);
                blob.color = Tint(colour, alpha * (1f - q), !dark);
            });
        }

        private static void Add(GameObject go, float life, float delay, Action<Piece, float> show) =>
            Pieces.Add(new Piece { Go = go, Life = life, Delay = delay, Show = show });

        private static void Age(float seconds)
        {
            for (var i = Pieces.Count - 1; i >= 0; i--)
            {
                var piece = Pieces[i];
                piece.Age += seconds;
                var q = (piece.Age - piece.Delay) / piece.Life;
                if (piece.Go == null || q >= 1f)
                {
                    if (piece.Go != null) Object.Destroy(piece.Go);
                    Pieces.RemoveAt(i);
                    continue;
                }
                if (q >= 0f) piece.Show(piece, q);
            }
        }

        private static void End(ZDOID id)
        {
            if (Floats.TryGetValue(id, out var coming) && coming.Lasting != null) Object.Destroy(coming.Lasting);
            Floats.Remove(id);
        }

        /// <summary>Takes every sign away, on leaving a world.</summary>
        public static void Forget()
        {
            foreach (var coming in Floats.Values)
                if (coming.Lasting != null) Object.Destroy(coming.Lasting);
            Floats.Clear();
            foreach (var piece in Pieces)
                if (piece.Go != null) Object.Destroy(piece.Go);
            Pieces.Clear();
        }

        // Helpers.

        /// <summary>A colour as the water shows it now: by night dimmer and moonlit, unless it is a dark shape.</summary>
        private static Color Tint(Color colour, float alpha, bool light = true)
        {
            if (!light) return new Color(colour.r, colour.g, colour.b, alpha);
            var moon = new Color(0.67f, 0.82f, 0.94f);
            var c = Color.Lerp(colour, moon * colour, _darkness);
            return new Color(c.r, c.g, c.b, alpha * (1f - 0.35f * _darkness));
        }

        private static float Surface(Vector3 at)
        {
            var level = Floating.GetLiquidLevel(at, 1f, LiquidType.Water);
            return level > -1000f ? level : ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : at.y;
        }

        private static Vector3 Jitter(Vector3 at, float spread) =>
            at + new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread) * 0.6f);

        private static void Face(Transform t)
        {
            var camera = Utils.GetMainCamera();
            if (camera != null) t.rotation = camera.transform.rotation;
        }

        private static float Across(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        private static Material SignMaterial()
        {
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default")) { name = "RodnarokSign" };
            return _material;
        }

        /// <summary>A round white spot fading to nothing at its edge.</summary>
        private static Sprite DotSprite()
        {
            if (_dot != null) return _dot;
            var texture = new Texture2D(SpritePixels, SpritePixels, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RodnarokSignDot" };
            var centre = (SpritePixels - 1) / 2f;
            for (var y = 0; y < SpritePixels; y++)
            for (var x = 0; x < SpritePixels; x++)
            {
                var r = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                var a = Mathf.Clamp01(1f - r);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            texture.Apply();
            _dot = Sprite.Create(texture, new Rect(0f, 0f, SpritePixels, SpritePixels), new Vector2(0.5f, 0.5f), SpritePixels);
            return _dot;
        }

        /// <summary>A fin: a thin triangle standing on the water, seen from both sides.</summary>
        private static Mesh FinMesh()
        {
            if (_fin != null) return _fin;
            _fin = new Mesh { name = "RodnarokFin" };
            _fin.vertices = new[] { new Vector3(0f, 0f, -0.18f), new Vector3(0f, 0.26f, -0.05f), new Vector3(0f, 0f, 0.14f) };
            _fin.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            _fin.colors = new[] { Color.white, Color.white, Color.white };
            _fin.RecalculateNormals();
            return _fin;
        }
    }
}
