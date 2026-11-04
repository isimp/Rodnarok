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
    /// The signs are drawn on the water's surface as crests over darker troughs, coloured by the
    /// game's own sun or moon, ambient light and fog each frame (<see cref="SignLight"/>), the
    /// crest of a ring brightest on the side facing the light.
    /// </summary>
    internal static class ArrivalSigns
    {
        private const int RingPoints = 28;
        private const int SpritePixels = 32;

        /// <summary>A part of a sign on the water, living a while.</summary>
        private sealed class Piece
        {
            public Part Part;
            public GameObject Go => Part?.Go;
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
            public float Size = 1f;
            public float Bold = 1f;
            public float Phase;
            public float Tilt;
            public Vector3 Axis;
            public List<(Transform T, Vector3 Pos, Quaternion Rot)> Look;
            public WaterVolume Water;
            public float Surge;
            public float HumpHeight;
            public float HumpLength;
            public float HumpSpray;
            public Mesh HumpMesh;
            public readonly Vector3[] HumpTop = new Vector3[HumpPoints + 1];
            public readonly Vector3[] HumpVerts = new Vector3[(HumpPoints + 1) * 2];
            public readonly Color[] HumpColours = new Color[(HumpPoints + 1) * 2];
            public readonly float[] WakeY = new float[WakePoints];
            public readonly float[] WakeSamples = new float[WakePoints / WakeEvery + 2];
            public readonly int[] WakeIndex = new int[WakePoints / WakeEvery + 2];
        }

        /// <summary>How many points along a wake apart the waves are sampled; the wake follows them between.</summary>
        private const int WakeEvery = 4;

        private const int WakePoints = (int)(SignPath.WakeSeconds / SignPath.TrailStep) + 2;

        private static readonly Dictionary<ZDOID, Coming> Floats = new Dictionary<ZDOID, Coming>();
        private static readonly List<Piece> Pieces = new List<Piece>();
        private static readonly List<ZDOID> Gone = new List<ZDOID>();
        private static readonly HashSet<ZDOID> Seen = new HashSet<ZDOID>();

        /// <summary>How long after it last came to a float a fish is still watched for a flee to dart off from, in seconds.</summary>
        private const float WatchAfter = 10f;

        /// <summary>Each fish that came to a float lately, with when it last did, and the flee each last darted for.</summary>
        private static readonly Dictionary<Fish, float> Lately = new Dictionary<Fish, float>();
        private static readonly Dictionary<Fish, long> DartedAt = new Dictionary<Fish, long>();
        private static readonly List<Fish> Dropped = new List<Fish>();

        private static Material _material;
        private static Sprite _dot;
        private static Mesh _fin;
        private static float _darkness;
        private static Color _crest = Color.white;
        private static Color _trough = new Color(0.1f, 0.12f, 0.14f);
        private static Vector3 _toSun = Vector3.up;
        private static Vector3 _camera;
        private static Color _fogColour = Color.grey;
        private static SignFog _fogMode;
        private static float _fogDensity;
        private static float _fogStart;
        private static float _fogEnd;
        private static float _wind;
        private static float _contrast = 1f;
        private static float _lineWidth = 1f;
        private static Vector3 _windFlat = Vector3.forward;

        /// <summary>How much bigger and bolder the sign being drawn shows for the stars of its fish (<see cref="SignStars"/>).</summary>
        private static float _size = 1f;
        private static float _bold = 1f;

        /// <summary>The contrast the signs are drawn at now and the width of their lines for it, for the self-test.</summary>
        [ForTest]
        public static (float Contrast, float LineWidth) ContrastForTest() => (_contrast, _lineWidth);

        /// <summary>The light the signs are drawn in now, for the self-test: the crest and trough colours, the game's light they come from, and its fog.</summary>
        [ForTest]
        public static (Color Crest, Color Trough, Color Sun, float Intensity, float SunUp, Color Ambient, SignFog Fog, float FogDensity, Color Sky) LightForTest()
        {
            var env = EnvMan.instance;
            var sun = env != null && env.m_dirLight != null ? env.m_dirLight : null;
            return (_crest, _trough, sun != null ? sun.color : Color.black, sun != null ? sun.intensity : 0f, _toSun.y, RenderSettings.ambientLight, _fogMode, _fogDensity, RenderSettings.fogColor);
        }

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
            ReadLight(env);
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

            // A fish that came to a float lately and fled just now, from a hooked school or a fright, darts off, once a flee.
            // The game takes its coming mark at the nibble, before the hook scatters the rest, so this watches it a while longer.
            _size = 1f;
            _bold = 1f;
            foreach (var coming in Floats.Values)
                foreach (var fish in coming.Fish)
                    Lately[fish] = Time.time;
            Dropped.Clear();
            foreach (var pair in Lately)
            {
                var fish = pair.Key;
                if (fish == null || Time.time - pair.Value > WatchAfter)
                {
                    Dropped.Add(fish);
                    continue;
                }
                if (!Schools.FledJustNow(fish, out var from, out var fledAt)) continue;
                if (DartedAt.TryGetValue(fish, out var was) && was == fledAt) continue;
                DartedAt[fish] = fledAt;
                Dart(fish.transform.position, from);
            }
            foreach (var fish in Dropped)
            {
                Lately.Remove(fish);
                DartedAt.Remove(fish);
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
                Step(pair.Value, floatObject, pair.Key);
            }
            foreach (var id in Gone) End(id);
            Age(Time.deltaTime);
        }

        /// <summary>
        /// One frame of a float's sign: its path steps to the school's centre, or to the fish going
        /// for the bait when one is marked; a travelling sign rides the path, and showings come on
        /// it, or by the float while the school mills about there.
        /// </summary>
        private static void Step(Coming coming, GameObject floatObject, ZDOID floatId)
        {
            var at = floatObject.transform.position;
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
            // A starred fish tells itself: a little bolder for a school holding one, by its own stars once it goes for the bait.
            var best = 1;
            foreach (var fish in coming.Fish) best = Math.Max(best, Quality(fish));
            var goingQuality = going != null ? Quality(going) : 1;
            coming.Size = SignStars.Size(best, going != null, goingQuality);
            coming.Bold = SignStars.Boldness(best, going != null, goingQuality);
            _size = coming.Size;
            _bold = coming.Bold;
            Tremble(coming, going, floatObject);
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
            if (lasts) Ring(new Vector3(showAt.x, Surface(showAt) + 0.02f, showAt.z), 1.1f, 1.6f, Mathf.Min(1f, ArrivalSign.Strength(nearness) * _bold) * 0.8f, 0f);
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
            var strength = Mathf.Min(1f, ArrivalSign.Strength(nearness) * _bold);
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
                    Blob(by2, new Vector3(1.3f, 0.5f, 1f), 0.5f, 1.6f);
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
                    // A hump of water standing up from the surface, facing the one looking: a crest over a darker band, spray off its front.
                    var band = new GameObject("Band");
                    band.transform.SetParent(go.transform, false);
                    band.AddComponent<MeshFilter>().sharedMesh = new Mesh { name = "RodnarokHump" };
                    band.AddComponent<MeshRenderer>().sharedMaterial = SignMaterial();
                    var crest = new GameObject("Crest").AddComponent<LineRenderer>();
                    crest.transform.SetParent(go.transform, false);
                    crest.useWorldSpace = true;
                    crest.positionCount = HumpPoints + 1;
                    crest.sharedMaterial = SignMaterial();
                    for (var i = 0; i < SprayTicks; i++)
                    {
                        var spray = new GameObject("Spray" + i).AddComponent<LineRenderer>();
                        spray.transform.SetParent(go.transform, false);
                        spray.useWorldSpace = true;
                        spray.positionCount = 2;
                        spray.sharedMaterial = SignMaterial();
                    }
                    Wake(go, "WakeLeftTrough");
                    Wake(go, "WakeRightTrough");
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
            coming.Water = WaterOf(pathAt);
            var strength = Mathf.Min(1f, ArrivalSign.Strength(nearness) * _bold) * showing * WaterMood.Clarity(RoughnessAt(coming.Water, pathAt));
            WakeHeights(coming, surface);
            var moving = SignPath.Moving(path.Speed);
            var heading = new Vector3(path.HeadX, 0f, path.HeadZ);
            var at = new Vector3(pathAt.x, surface + 0.03f, pathAt.z);
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            switch (coming.Kind)
            {
                case ArrivalSignKind.Bulge:
                    Hump(coming, go, at, heading, strength, moving);
                    SetWake(go.transform.Find("WakeLeftTrough"), coming, 0.025f, -1f, 0.45f * strength * moving, dark: true);
                    SetWake(go.transform.Find("WakeRightTrough"), coming, 0.025f, 1f, 0.45f * strength * moving, dark: true);
                    SetWake(go.transform.Find("WakeLeft"), coming, 0.03f, -1f, 0.7f * strength * moving);
                    SetWake(go.transform.Find("WakeRight"), coming, 0.03f, 1f, 0.7f * strength * moving);
                    break;
                case ArrivalSignKind.Glow:
                    var pulse = (0.75f + 0.25f * Mathf.Sin(Time.time * 3f)) * showing;
                    var light = go.GetComponentInChildren<Light>();
                    light.transform.position = at + Vector3.down * 0.6f;
                    light.intensity = (0.6f + 1.4f * nearness) * pulse * Mathf.Clamp(_contrast, SignLight.MinContrast, SignLight.MaxContrast);
                    light.range = (2f + 2f * nearness) * _size;
                    var halo = go.transform.Find("Halo").GetComponent<SpriteRenderer>();
                    halo.color = new Color(0.82f, 1f, 0.67f, SignLight.Shown((0.15f + 0.4f * nearness) * pulse, _contrast) * (1f - FogAt(at)));
                    halo.transform.localScale = Vector3.one * (0.8f + 0.8f * nearness) * _size;
                    break;
                case ArrivalSignKind.Fin:
                    // Breaking the surface now and then, as a fin does.
                    var up = Mathf.Sin(Time.time * 1.7f + 1f) > -0.2f && showing > 0.02f;
                    var fin = go.transform.Find("Fin");
                    fin.gameObject.SetActive(up);
                    fin.localScale = Vector3.one * 1.6f * _size;
                    // Dark against the day's water, by night edged in the night's own light, so it shows on dark water too.
                    var finColour = Color.Lerp(new Color(0.12f, 0.16f, 0.2f), _crest * 0.75f, _darkness);
                    fin.GetComponent<MeshRenderer>().material.color = Fogged(at, new Color(finColour.r, finColour.g, finColour.b, 0.9f * showing));
                    SetWake(go.transform.Find("Wake"), coming, 0.03f, 0f, up ? 0.6f * strength * moving : 0f);
                    break;
            }
        }

        /// <summary>How many pieces the Trollfish's hump is drawn in along its length, and how many spray ticks fly off its front.</summary>
        private const int HumpPoints = 14;
        private const int SprayTicks = 3;

        /// <summary>The triangles of the hump's dark band, a strip between its crest and the water, the same for every hump.</summary>
        private static readonly int[] HumpTriangles = BandTriangles();

        private static int[] BandTriangles()
        {
            var triangles = new int[HumpPoints * 6];
            for (var i = 0; i < HumpPoints; i++)
            {
                var t = i * 6;
                var top = i * 2;
                triangles[t] = top;
                triangles[t + 1] = top + 2;
                triangles[t + 2] = top + 1;
                triangles[t + 3] = top + 1;
                triangles[t + 4] = top + 2;
                triangles[t + 5] = top + 3;
            }
            return triangles;
        }

        /// <summary>
        /// The Trollfish's hump (<see cref="TrollHump"/>): standing up from the water across the view
        /// of the one looking, as long as the fish swims across it and higher toward its front, riding
        /// the waves near the camera; a crest lit by the game's light over a dark band of the water it
        /// pushes up, surging and throwing spray off its front, all as strongly as the sign shows.
        /// </summary>
        private static void Hump(Coming coming, GameObject go, Vector3 at, Vector3 heading, float strength, float moving)
        {
            coming.Surge += Time.deltaTime / TrollHump.SurgeSeconds;
            var camera = Utils.GetMainCamera();
            var right = camera != null ? Flat(camera.transform.right) : Vector3.right;
            var across = Vector3.Dot(heading, right);
            var front = across >= 0f ? 1f : -1f;
            var length = TrollHump.Length(across) * _size;
            var height = TrollHump.Height(coming.Surge, moving) * _size;
            var spray = TrollHump.Spray(coming.Surge) * moving;
            coming.HumpHeight = height;
            coming.HumpLength = length;
            coming.HumpSpray = spray;
            var near = Near(at);
            var shown = strength * (0.4f + 0.6f * moving);
            var dark = Dark(at, 0.5f * shown);
            var under = new Color(dark.r, dark.g, dark.b, dark.a * 0.3f);
            for (var i = 0; i <= HumpPoints; i++)
            {
                var u = i / (float)HumpPoints * 2f - 1f;
                var place = at + right * (u * length / 2f);
                var water = near ? SurfaceOn(coming.Water, place) : at.y - 0.03f;
                var top = water + 0.01f + height * TrollHump.Arch(u, front);
                coming.HumpTop[i] = new Vector3(place.x, top, place.z);
                coming.HumpVerts[i * 2] = new Vector3(place.x, top - 0.01f, place.z);
                coming.HumpVerts[i * 2 + 1] = new Vector3(place.x, water - 0.02f, place.z);
                coming.HumpColours[i * 2] = dark;
                coming.HumpColours[i * 2 + 1] = under;
            }

            var band = go.transform.Find("Band");
            band.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var mesh = band.GetComponent<MeshFilter>().sharedMesh;
            coming.HumpMesh = mesh;
            mesh.vertices = coming.HumpVerts;
            mesh.colors = coming.HumpColours;
            if (mesh.triangles.Length == 0) mesh.triangles = HumpTriangles;
            mesh.RecalculateBounds();

            var crest = go.transform.Find("Crest").GetComponent<LineRenderer>();
            crest.SetPositions(coming.HumpTop);
            crest.widthMultiplier = 0.05f * _lineWidth;
            var lit = Lit(at, 0.85f * shown);
            crest.startColor = lit;
            crest.endColor = lit;

            // Spray off the front end as the hump surges.
            var nose = at + right * (front * length / 2f * 0.85f);
            var noseY = near ? SurfaceOn(coming.Water, nose) : at.y - 0.03f;
            var sprayColour = Lit(nose, 0.8f * spray * strength);
            for (var k = 0; k < SprayTicks; k++)
            {
                var tick = go.transform.Find("Spray" + k).GetComponent<LineRenderer>();
                var foot = new Vector3(nose.x, noseY + 0.03f, nose.z);
                var lean = right * (front * (0.2f + 0.15f * (k - 1)));
                tick.SetPosition(0, foot);
                tick.SetPosition(1, foot + (Vector3.up + lean).normalized * (spray * (0.25f + 0.1f * k) * _size));
                tick.widthMultiplier = 0.035f * _lineWidth;
                tick.startColor = sprayColour;
                tick.endColor = new Color(sprayColour.r, sprayColour.g, sprayColour.b, 0f);
            }
        }

        /// <summary>How high the Trollfish's hump stands at a float now, how long it is and how strongly spray flies off it, for the self-test; zeros for none.</summary>
        [ForTest]
        public static (float Height, float Length, float Spray) HumpForTest(ZDOID floatId) =>
            Floats.TryGetValue(floatId, out var coming) ? (coming.HumpHeight, coming.HumpLength, coming.HumpSpray) : (0f, 0f, 0f);

        private static void Wake(GameObject parent, string name)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent.transform, false);
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = 0.07f;
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, SignLight.Taper(1f));
            line.sharedMaterial = SignMaterial();
        }

        /// <summary>
        /// A wake along where the path has been, newest point first: on one side of it, spreading as
        /// the point ages, or on the path itself for a side of 0; a crest lit by the light, or its
        /// dark trough a little further out.
        /// </summary>
        private static void SetWake(Transform wake, Coming coming, float lift, float side, float alpha, bool dark = false)
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
                var off = side * (SignPath.WakeSide(trail[i].Age) * _size + (dark ? 0.09f : 0f));
                coming.Wake[i] = new Vector3(point.X - dz / length * off, coming.WakeY[i] + lift, point.Z + dx / length * off);
            }
            line.positionCount = n;
            line.widthMultiplier = 0.07f * _lineWidth;
            if (n > 0) line.SetPositions(coming.Wake);
            var head = new Vector3(coming.Path.At.X, coming.WakeY[0] + lift, coming.Path.At.Z);
            var colour = dark ? Dark(head, alpha) : Lit(head, alpha);
            line.startColor = colour;
            line.endColor = new Color(colour.r, colour.g, colour.b, 0f);
        }

        // Parts of signs, kept and used again (PartPool) rather than made and thrown away with every showing.

        /// <summary>One kept part of a sign: a line, a ring with its trough, or a spot.</summary>
        private sealed class Part
        {
            public GameObject Go;
            public LineRenderer Line;
            public LineRenderer Trough;
            public SpriteRenderer Sprite;
            public Gradient Gradient;
            public GradientColorKey[] Colours;
            public GradientAlphaKey[] Alphas;
            public PartPool<Part> Pool;
            public float[] Heights;
        }

        private const int PartsKept = 64;

        /// <summary>How many places round a ring the waves are sampled at; the ring follows them between.</summary>
        private const int RingSamples = 8;
        private static readonly AnimationCurve Even = AnimationCurve.Constant(0f, 1f, 1f);
        private static PartPool<Part> _rings;
        private static PartPool<Part> _lines;
        private static PartPool<Part> _spots;

        /// <summary>How many parts of signs this game has made, for the self-test.</summary>
        [ForTest]
        public static int PartsMadeForTest() => (_rings?.Made ?? 0) + (_lines?.Made ?? 0) + (_spots?.Made ?? 0);

        private static PartPool<Part> NewPool(Func<Part> make) =>
            new PartPool<Part>(make, p => { if (p.Go != null) p.Go.SetActive(false); }, p => { if (p.Go != null) Object.Destroy(p.Go); }, PartsKept);

        /// <summary>A kept part from a pool, or a new one; one whose object the game took away on a world change is dropped.</summary>
        private static Part Take(ref PartPool<Part> pool, Func<Part> make)
        {
            if (pool == null) pool = NewPool(make);
            var part = pool.Take();
            while (part.Go == null) part = pool.Take();
            part.Pool = pool;
            part.Go.SetActive(true);
            return part;
        }

        private static LineRenderer NewLine(GameObject go)
        {
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = SignMaterial();
            line.enabled = false;
            return line;
        }

        private static Part MakeRing()
        {
            var go = new GameObject("RodnarokSignRing");
            var line = NewLine(go);
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = RingPoints;
            var trough = NewLine(new GameObject("Trough"));
            trough.transform.SetParent(go.transform, false);
            trough.transform.localPosition = Vector3.down * 0.005f;
            trough.useWorldSpace = false;
            trough.loop = true;
            trough.positionCount = RingPoints;
            return new Part { Go = go, Line = line, Trough = trough, Gradient = new Gradient(), Colours = new GradientColorKey[GradientKeys], Alphas = new GradientAlphaKey[GradientKeys], Heights = new float[RingSamples] };
        }

        private static Part MakeLine()
        {
            var go = new GameObject("RodnarokSignLine");
            var line = NewLine(go);
            line.useWorldSpace = true;
            return new Part { Go = go, Line = line };
        }

        private static Part MakeSpot()
        {
            var go = new GameObject("RodnarokSignSpot");
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = DotSprite();
            sprite.enabled = false;
            return new Part { Go = go, Sprite = sprite };
        }

        /// <summary>A plain line in the world, of a number of points, width and flat across.</summary>
        private static Part Line(int points, float width)
        {
            var part = Take(ref _lines, MakeLine);
            var line = part.Line;
            line.loop = false;
            line.positionCount = points;
            line.widthMultiplier = width * _lineWidth;
            line.widthCurve = Even;
            line.enabled = false;
            return part;
        }

        /// <summary>A spot at a place, hidden until it first shows.</summary>
        private static Part Spot(Vector3 at)
        {
            var part = Take(ref _spots, MakeSpot);
            part.Go.transform.SetPositionAndRotation(at, Quaternion.identity);
            part.Go.transform.localScale = Vector3.one;
            part.Sprite.enabled = false;
            return part;
        }

        private static void Ring(Vector3 at, float radius, float life, float strength, float delay, float width = 0.07f)
        {
            radius *= _size;
            var part = Take(ref _rings, MakeRing);
            var line = part.Line;
            var trough = part.Trough;
            part.Go.transform.SetPositionAndRotation(at, Quaternion.identity);
            line.enabled = false;
            trough.enabled = false;
            // The water it is on: rough water shows it fainter and broken, each ring's breaks in its own places.
            var water = WaterOf(at);
            var roughness = RoughnessAt(water, at);
            var clarity = WaterMood.Clarity(roughness);
            var seed = Random.value;
            Add(part, life, delay, (p, q) =>
            {
                // Thin against its size from the start, so a young ring is a ring and not a disc, and thinner as it spreads.
                var r = radius * (0.25f + 0.75f * q);
                var w = Mathf.Min(width, r * 0.12f) * SignLight.Taper(q) * _lineWidth;
                line.widthMultiplier = w;
                trough.widthMultiplier = w * 1.4f;
                // Drifting downwind, and near the camera rising and falling with the waves, sampled round it and followed between.
                var centre = at + Downwind(q * life);
                var follows = Near(centre);
                var baseY = follows ? SurfaceOn(water, centre) + 0.02f : at.y;
                p.Part.Go.transform.position = new Vector3(centre.x, baseY, centre.z);
                var heights = p.Part.Heights;
                for (var k = 0; k < heights.Length; k++)
                {
                    var a = k * Mathf.PI * 2f / heights.Length;
                    heights[k] = follows ? SurfaceOn(water, centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)) + 0.02f - baseY : 0f;
                }
                for (var i = 0; i < RingPoints; i++)
                {
                    var a = i * Mathf.PI * 2f / RingPoints;
                    var h = WaterMood.AroundLoop(heights, i / (float)RingPoints);
                    line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r));
                    trough.SetPosition(i, new Vector3(Mathf.Cos(a) * r * 0.93f, h, Mathf.Sin(a) * r * 0.93f));
                }
                var alpha = 0.6f * strength * Mathf.Min(1f, q * 6f) * (1f - q) * clarity;
                var crest = Lit(centre, alpha);
                // Brightest where the ring faces the light across the water.
                var toSun = Flat(_toSun);
                for (var k = 0; k < GradientKeys; k++)
                {
                    var t = k / (float)(GradientKeys - 1);
                    var a = t * Mathf.PI * 2f;
                    var facing = Mathf.Cos(a) * toSun.x + Mathf.Sin(a) * toSun.z;
                    p.Part.Colours[k] = new GradientColorKey(crest * SignLight.SunSide(facing), t);
                    p.Part.Alphas[k] = new GradientAlphaKey(crest.a * WaterMood.Broken(roughness, t, seed), t);
                }
                p.Part.Gradient.SetKeys(p.Part.Colours, p.Part.Alphas);
                line.colorGradient = p.Part.Gradient;
                var dark = Dark(centre, alpha * 0.5f);
                trough.startColor = dark;
                trough.endColor = dark;
                line.enabled = true;
                trough.enabled = true;
            });
        }

        private static void Dot(Vector3 at, float size, float life, float rise, float strength, float delay, Color colour)
        {
            size *= _size;
            var part = Spot(at);
            var dot = part.Sprite;
            var water = WaterOf(at);
            var clarity = WaterMood.Clarity(RoughnessAt(water, at));
            Add(part, life, delay, (p, q) =>
            {
                var place = at + Downwind(q * life);
                var y = Near(place) ? SurfaceOn(water, place) + 0.02f : at.y;
                dot.transform.position = new Vector3(place.x, y + rise * q, place.z);
                dot.transform.localScale = Vector3.one * size;
                Face(dot.transform);
                dot.color = Lit(dot.transform.position, 0.85f * strength * (1f - q) * clarity, colour);
                dot.enabled = true;
            });
        }

        /// <summary>Steam rising off the water, leaning downwind as it rises.</summary>
        private static void Puff(Vector3 at, float strength)
        {
            var size = _size;
            var part = Spot(at);
            var puff = part.Sprite;
            Add(part, 1.8f, 0f, (p, q) =>
            {
                puff.transform.position = at + new Vector3(0.1f * q, 1.2f * q, 0f) + Downwind(q * 1.8f) * (1f + 2f * q);
                puff.transform.localScale = Vector3.one * (0.5f + 1.2f * q) * size;
                Face(puff.transform);
                puff.color = Lit(puff.transform.position, 0.65f * strength * (1f - q), new Color(0.92f, 0.92f, 0.92f));
                puff.enabled = true;
            });
        }

        private static void Splash(Vector3 at, float height, float life, float strength, float delay)
        {
            height *= _size;
            var part = Line(2, 0.07f);
            var line = part.Line;
            var lean = new Vector3(Random.Range(-0.05f, 0.05f), 0f, Random.Range(-0.05f, 0.05f));
            var water = WaterOf(at);
            var clarity = WaterMood.Clarity(RoughnessAt(water, at));
            Add(part, life, delay, (p, q) =>
            {
                var foot = Near(at) ? new Vector3(at.x, SurfaceOn(water, at) + 0.02f, at.z) : at;
                line.SetPosition(0, foot);
                line.SetPosition(1, foot + lean + Vector3.up * height * Mathf.Sin(q * Mathf.PI));
                var c = Lit(foot, 0.9f * strength * (1f - q) * clarity);
                line.startColor = c;
                line.endColor = c;
                line.enabled = true;
            });
        }

        /// <summary>The Pufferfish's dome: a half ring standing on the water, facing the camera, swelling.</summary>
        private static void Arc(Vector3 at, float radius, float life, float strength)
        {
            radius *= _size;
            var part = Line(RingPoints / 2 + 1, 0.07f);
            var line = part.Line;
            var water = WaterOf(at);
            var clarity = WaterMood.Clarity(RoughnessAt(water, at));
            Add(part, life, 0f, (p, q) =>
            {
                var camera = Utils.GetMainCamera();
                var right = camera != null ? Flat(camera.transform.right) : Vector3.right;
                var foot = Near(at) ? new Vector3(at.x, SurfaceOn(water, at) + 0.02f, at.z) : at;
                var r = 0.05f + radius * q;
                for (var i = 0; i <= RingPoints / 2; i++)
                {
                    var a = i * Mathf.PI / (RingPoints / 2);
                    line.SetPosition(i, foot + right * Mathf.Cos(a) * r + Vector3.up * Mathf.Sin(a) * r * 0.8f);
                }
                var c = Lit(foot, 0.8f * strength * clarity);
                line.startColor = c;
                line.endColor = c;
                line.enabled = true;
            });
        }

        /// <summary>A dark patch spreading on the water, in the trough's colour.</summary>
        private static void Blob(Vector3 at, Vector3 scale, float alpha, float life)
        {
            scale *= _size;
            var part = Spot(at);
            var blob = part.Sprite;
            blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var water = WaterOf(at);
            var clarity = WaterMood.Clarity(RoughnessAt(water, at));
            Add(part, life, 0f, (p, q) =>
            {
                if (Near(at)) blob.transform.position = new Vector3(at.x, SurfaceOn(water, at) + 0.02f, at.z);
                blob.transform.localScale = scale * (1f + q);
                blob.color = Dark(at, alpha * (1f - q) * clarity);
                blob.enabled = true;
            });
        }

        /// <summary>How many fleeing fish have darted off, for the self-test.</summary>
        private static int _darts;

        [ForTest]
        public static int DartsForTest() => _darts;

        /// <summary>
        /// A fish fleeing a hooked school or a fright darts off away from what frightened it: a short V
        /// of a wake running out from where it was, lit as the other signs, with a ring where it started.
        /// </summary>
        private static void Dart(Vector3 fishAt, Vector3 from)
        {
            var (ax, az) = ScatterBurst.Away(fishAt.x, fishAt.z, from.x, from.z);
            var away = new Vector3(ax, 0f, az);
            var side = new Vector3(-az, 0f, ax);
            var start = new Vector3(fishAt.x, Surface(fishAt) + 0.03f, fishAt.z);
            _darts++;
            Ring(start, 0.6f, 0.7f, 0.9f, 0f);
            var part = Line(3, 0.06f);
            var line = part.Line;
            var water = WaterOf(start);
            var clarity = WaterMood.Clarity(RoughnessAt(water, start));
            Add(part, ScatterBurst.Seconds, 0f, (p, q) =>
            {
                var head = start + away * ScatterBurst.Run(q);
                var spread = 0.3f + 0.4f * q;
                line.SetPosition(0, OnWater(water, head - away * 0.9f - side * spread, start.y));
                line.SetPosition(1, OnWater(water, head, start.y));
                line.SetPosition(2, OnWater(water, head - away * 0.9f + side * spread, start.y));
                line.widthMultiplier = 0.06f * SignLight.Taper(q) * _lineWidth;
                var c = Lit(head, ScatterBurst.Alpha(q) * clarity);
                line.startColor = c;
                line.endColor = c;
                line.enabled = true;
            });
        }

        // The float's tremble.

        /// <summary>
        /// The float trembles as the fish going for the bait closes its last 1.5 m: its look, never the
        /// game's float, tilts toward the fish in twitches; with no fish so close it eases upright.
        /// </summary>
        private static void Tremble(Coming coming, Fish going, GameObject floatObject)
        {
            var target = 0f;
            if (going != null)
            {
                var offset = going.transform.position - floatObject.transform.position;
                var closeness = FloatTremble.Closeness(new Vector2(offset.x, offset.z).magnitude);
                if (closeness > 0f)
                {
                    coming.Phase += Time.deltaTime / FloatTremble.Seconds(closeness);
                    target = FloatTremble.Degrees(closeness, coming.Phase);
                    var (x, z) = FloatTremble.Axis(offset.x, offset.z);
                    coming.Axis = new Vector3(x, 0f, z);
                }
            }
            coming.Tilt = target > coming.Tilt ? target : Mathf.MoveTowards(coming.Tilt, target, 60f * Time.deltaTime);
            if (coming.Tilt <= 0f && coming.Look == null) return;
            if (coming.Look == null) coming.Look = LookOf(floatObject);
            TiltLook(coming, floatObject.transform);
        }

        /// <summary>The float's look: each child of the float holding something drawn, but no line, with where it sits.</summary>
        private static List<(Transform T, Vector3 Pos, Quaternion Rot)> LookOf(GameObject floatObject)
        {
            var look = new List<(Transform T, Vector3 Pos, Quaternion Rot)>();
            var root = floatObject.transform;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.GetComponentInChildren<LineRenderer>(true) != null) continue;
                var drawn = child.GetComponentInChildren<MeshRenderer>(true) != null || child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                if (drawn) look.Add((child, child.localPosition, child.localRotation));
            }
            return look;
        }

        private static void TiltLook(Coming coming, Transform root)
        {
            var axis = root.InverseTransformDirection(coming.Axis);
            var turn = coming.Tilt > 0f && axis.sqrMagnitude > 0.0001f ? Quaternion.AngleAxis(coming.Tilt, axis) : Quaternion.identity;
            foreach (var (t, pos, rot) in coming.Look)
            {
                if (t == null) continue;
                t.localPosition = turn * pos;
                t.localRotation = turn * rot;
            }
        }

        /// <summary>Puts the float's look back as it was.</summary>
        private static void Upright(Coming coming)
        {
            if (coming.Look == null) return;
            foreach (var (t, pos, rot) in coming.Look)
            {
                if (t == null) continue;
                t.localPosition = pos;
                t.localRotation = rot;
            }
            coming.Look = null;
            coming.Tilt = 0f;
        }

        /// <summary>How far a float's look tilts now, toward what, and how many parts of it tilt, for the self-test; zeros for no float.</summary>
        [ForTest]
        public static (float Degrees, Vector3 Axis, int Parts) TrembleForTest(ZDOID floatId) =>
            Floats.TryGetValue(floatId, out var coming) ? (coming.Tilt, coming.Axis, coming.Look?.Count ?? 0) : (0f, Vector3.zero, 0);

        /// <summary>How much bigger and bolder a float's sign shows for the stars of its fish, for the self-test; 1 and 1 for none.</summary>
        [ForTest]
        public static (float Size, float Bold) StarsForTest(ZDOID floatId) =>
            Floats.TryGetValue(floatId, out var coming) ? (coming.Size, coming.Bold) : (1f, 1f);

        /// <summary>A fish's stars as the game's item quality, 1 for none, from its networked data so every game reads the same.</summary>
        private static int Quality(Fish fish)
        {
            var zdo = fish != null ? fish.GetComponent<ZNetView>()?.GetZDO() : null;
            return zdo == null ? 1 : zdo.GetInt(ZDOVars.s_quality, 1);
        }

        private static void Add(Part part, float life, float delay, Action<Piece, float> show) =>
            Pieces.Add(new Piece { Part = part, Life = life, Delay = delay, Show = show });

        private static void Age(float seconds)
        {
            for (var i = Pieces.Count - 1; i >= 0; i--)
            {
                var piece = Pieces[i];
                piece.Age += seconds;
                var q = (piece.Age - piece.Delay) / piece.Life;
                if (piece.Go == null || q >= 1f)
                {
                    piece.Part.Pool.Return(piece.Part);
                    Pieces.RemoveAt(i);
                    continue;
                }
                if (q >= 0f) piece.Show(piece, q);
            }
        }

        private static void End(ZDOID id)
        {
            if (Floats.TryGetValue(id, out var coming))
            {
                if (coming.Lasting != null) Object.Destroy(coming.Lasting);
                if (coming.HumpMesh != null) Object.Destroy(coming.HumpMesh);
                Upright(coming);
            }
            Floats.Remove(id);
        }

        /// <summary>Takes every sign away, on leaving a world.</summary>
        public static void Forget()
        {
            foreach (var coming in Floats.Values)
            {
                if (coming.Lasting != null) Object.Destroy(coming.Lasting);
                if (coming.HumpMesh != null) Object.Destroy(coming.HumpMesh);
                Upright(coming);
            }
            Floats.Clear();
            foreach (var piece in Pieces)
                if (piece.Go != null) Object.Destroy(piece.Go);
            Pieces.Clear();
            _rings?.Clear();
            _lines?.Clear();
            _spots?.Clear();
            Lately.Clear();
            DartedAt.Clear();
        }

        // Helpers.

        private const int GradientKeys = 8;

        /// <summary>
        /// Reads the light the game puts on the world this frame: its sun, or the moon it turns that
        /// light into at night, by colour, strength and height, its ambient light and its fog.
        /// </summary>
        private static void ReadLight(EnvMan env)
        {
            var sun = env != null ? env.m_dirLight : null;
            var ambient = RenderSettings.ambientLight;
            var air = new Shade(ambient.r, ambient.g, ambient.b);
            if (sun != null)
            {
                _toSun = -sun.transform.forward;
                // Never darker than the sky the water mirrors, which the game's fog colour stands for.
                var sky = RenderSettings.fogColor;
                var crest = SignLight.Crest(new Shade(sun.color.r, sun.color.g, sun.color.b), sun.intensity, _toSun.y, air, new Shade(sky.r, sky.g, sky.b));
                _crest = new Color(crest.R, crest.G, crest.B);
            }
            var trough = SignLight.Trough(air);
            _trough = new Color(trough.R, trough.G, trough.B);
            _fogColour = RenderSettings.fogColor;
            _fogMode = !RenderSettings.fog ? SignFog.Off
                : RenderSettings.fogMode == FogMode.Linear ? SignFog.Linear
                : RenderSettings.fogMode == FogMode.Exponential ? SignFog.Exponential
                : SignFog.ExponentialSquared;
            _fogDensity = RenderSettings.fogDensity;
            _fogStart = RenderSettings.fogStartDistance;
            _fogEnd = RenderSettings.fogEndDistance;
            var camera = Utils.GetMainCamera();
            if (camera != null) _camera = camera.transform.position;
            // How strongly the server has the signs show, and how wide their lines are for it.
            _contrast = PlayRules.Now.SignContrast;
            _lineWidth = SignLight.LineWidth(_contrast);
            // The wind the game's waves blow by, for signs drifting downwind.
            _wind = env != null ? env.GetWindIntensity() : 0f;
            if (env != null) _windFlat = Flat(env.GetWindDir());
        }

        /// <summary>
        /// The heights of the water under a wake's points: near the camera sampled every few points
        /// along where the path has been and followed between, farther all at the path's surface.
        /// </summary>
        private static void WakeHeights(Coming coming, float surface)
        {
            var trail = coming.Path.Trail;
            var n = Math.Min(trail.Count, coming.WakeY.Length);
            if (n == 0) return;
            if (!Near(new Vector3(trail[0].At.X, surface, trail[0].At.Z)))
            {
                for (var i = 0; i < n; i++) coming.WakeY[i] = surface;
                return;
            }
            var count = 0;
            for (var i = 0; i < n; i += WakeEvery) Sample(coming, i, count++, surface);
            if (coming.WakeIndex[count - 1] != n - 1) Sample(coming, n - 1, count++, surface);
            var j = 0;
            for (var i = 0; i < n; i++)
            {
                if (count == 1)
                {
                    coming.WakeY[i] = coming.WakeSamples[0];
                    continue;
                }
                while (j < count - 2 && i > coming.WakeIndex[j + 1]) j++;
                var from = coming.WakeIndex[j];
                var to = coming.WakeIndex[j + 1];
                coming.WakeY[i] = Mathf.Lerp(coming.WakeSamples[j], coming.WakeSamples[j + 1], to > from ? (i - from) / (float)(to - from) : 0f);
            }
        }

        private static void Sample(Coming coming, int point, int slot, float surface)
        {
            var at = coming.Path.Trail[point].At;
            coming.WakeIndex[slot] = point;
            coming.WakeSamples[slot] = SurfaceOn(coming.Water, new Vector3(at.X, surface, at.Z));
        }

        private static readonly Collider[] WaterHits = new Collider[8];
        private static int _waterMask;

        /// <summary>The game's water a place lies in, found once for a sign so its waves are read without a search each frame; none outside water.</summary>
        private static WaterVolume WaterOf(Vector3 at)
        {
            if (_waterMask == 0) _waterMask = LayerMask.GetMask("WaterVolume");
            var hits = Physics.OverlapSphereNonAlloc(at, 0f, WaterHits, _waterMask);
            for (var i = 0; i < hits; i++)
            {
                var water = WaterHits[i] != null ? WaterHits[i].GetComponent<WaterVolume>() : null;
                if (water != null && water.GetLiquidType() == LiquidType.Water) return water;
            }
            return null;
        }

        /// <summary>The water's surface at a place, waves and all, by the game's own wave sum.</summary>
        private static float SurfaceOn(WaterVolume water, Vector3 at) => water != null ? water.GetWaterSurface(at) : Surface(at);

        /// <summary>A place on the water's surface near the camera, or at a fixed height farther off.</summary>
        private static Vector3 OnWater(WaterVolume water, Vector3 at, float flatY) =>
            new Vector3(at.x, Near(at) ? SurfaceOn(water, at) + 0.03f : flatY, at.z);

        /// <summary>How rough the water is at a place: the game's wave strength there, its depth setting times the wind.</summary>
        private static float RoughnessAt(WaterVolume water, Vector3 at) => water != null ? WaterMood.Roughness(water.Depth(at), _wind) : 0f;

        /// <summary>How far downwind a sign has drifted after a number of seconds.</summary>
        private static Vector3 Downwind(float seconds) => _windFlat * WaterMood.Drift(_wind, seconds);

        private static bool Near(Vector3 at) => WaterMood.FollowsWaves(Vector3.Distance(_camera, at));

        /// <summary>How rough the water is at a float, how clearly signs show there, and the wind, for the self-test.</summary>
        [ForTest]
        public static (float Roughness, float Clarity, float Depth, float Wind) WaterForTest(Vector3 at)
        {
            var water = WaterOf(at);
            var roughness = RoughnessAt(water, at);
            return (roughness, WaterMood.Clarity(roughness), water != null ? water.Depth(at) : -1f, _wind);
        }

        /// <summary>How far any showing ring near the camera lies from the water under it, at worst, in metres, for the self-test; -1 for none.</summary>
        [ForTest]
        public static float RingFitForTest()
        {
            var worst = -1f;
            foreach (var piece in Pieces)
            {
                var part = piece.Part;
                if (part?.Trough == null || part.Go == null || !part.Line.enabled) continue;
                var centre = part.Go.transform.position;
                if (!Near(centre)) continue;
                for (var i = 0; i < RingPoints; i += 4)
                {
                    var point = part.Go.transform.TransformPoint(part.Line.GetPosition(i));
                    worst = Mathf.Max(worst, Mathf.Abs(point.y - (Surface(point) + 0.02f)));
                }
            }
            return worst;
        }

        /// <summary>How much of the game's fog lies between the camera and a place.</summary>
        private static float FogAt(Vector3 at) => SignLight.FogShare(_fogMode, _fogDensity, _fogStart, _fogEnd, Vector3.Distance(_camera, at));

        /// <summary>A colour as it shows at a place, as strong as the server's contrast has it, through the game's fog: toward the fog's colour and fainter.</summary>
        private static Color Fogged(Vector3 at, Color colour)
        {
            var share = FogAt(at);
            var c = Color.Lerp(colour, _fogColour, share);
            return new Color(c.r, c.g, c.b, SignLight.Shown(colour.a, _contrast) * (1f - share));
        }

        /// <summary>A crest's colour at a place: foam, or a tint of it, lit by the game's light, through its fog.</summary>
        private static Color Lit(Vector3 at, float alpha, Color? tint = null)
        {
            var c = tint.HasValue ? _crest * tint.Value : _crest;
            return Fogged(at, new Color(c.r, c.g, c.b, alpha));
        }

        /// <summary>A trough's colour at a place: the air's hue, dark, through the game's fog.</summary>
        private static Color Dark(Vector3 at, float alpha) => Fogged(at, new Color(_trough.r, _trough.g, _trough.b, alpha));

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
