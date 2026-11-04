using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Every fish coming to a float tells itself by its sign, as a player meets it. A float with
    /// plain bait in deep water brings a school of Perch, every fish of it marked as coming to the
    /// float for every game; the float must show the Perch's rings on the way in, more often as the
    /// school nears, and once a fish bites the marks must be gone and no more signs show. Then one
    /// fish of each of the game's fish is put a few metres from a float and marked as coming: each
    /// must show its own sign, and a picture of each is taken, 5 m short of the float toward the
    /// character, the character looking down at it. On the way in the showings must follow one
    /// path, no further apart than the school can swim; at the float the signs go quiet, slow and by
    /// the float; and the fish marked for every game as going for the bait must be the one that
    /// bites. A Trollfish, Anglerfish and salmon are pictured swimming across the view, their sign
    /// facing that way, and the Trollfish's bulge must fade at the float, leaving a slow ring there.
    /// The float must tremble toward the Perch going for the bait in its last 1.5 m; hooked, the
    /// rest of the school must dart off and the float's look stand upright again; the signs must
    /// use their parts again rather than make new ones; a two-star Perch must show its hint coming
    /// and its size going for the bait, pictured; the server's contrast must reach the signs,
    /// pictured at twice as strong; the crest must be lit by the game's light; and
    /// rings near the camera must lie on the waves, the water's roughness noted.
    /// A Wisplight's wisp stays with the character meanwhile, so it does not sit in the pictures.
    /// The rules, the setting, the rod, the floats and the fish are put back after.
    /// </summary>
    internal static class SelfTestSigns
    {
        private const string NoBait = "Stone";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");
        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");

        private static readonly (string Prefab, string Name)[] GameFish =
        {
            ("Fish1", "perch"), ("Fish6", "herring"), ("Fish2", "pike"), ("Fish5", "trollfish"), ("Fish7", "grouper"), ("Fish8", "coralcod"),
            ("Fish12", "pufferfish"), ("Fish9", "anglerfish"), ("Fish11", "magmafish"), ("Fish3", "tuna"), ("Fish10", "salmon"), ("Fish4_cave", "tetra"),
        };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("EachFishComingToAFloatShowsItsOwnSign", Shows);
        }

        private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>How far short of the float, toward the character, each fish of the pictures is put, in metres: near enough for the Pike's swirl.</summary>
        private const float PictureShort = 5f;

        private static bool Travels(ArrivalSignKind kind) => kind == ArrivalSignKind.Bulge || kind == ArrivalSignKind.Glow || kind == ArrivalSignKind.Fin;

        /// <summary>Moves a test fish to a place through its physics body, which the game steers a fish by; setting its position alone is undone by the body.</summary>
        private static void Swim(GameObject go, Vector3 to)
        {
            var body = go.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = to;
                body.linearVelocity = Vector3.zero;
            }
            go.transform.position = to;
        }

        /// <summary>The character looks down at a place a little to its side, so the third-person camera shows it past the character.</summary>
        private static void Aim(Player player, Vector3 at)
        {
            var dir = Quaternion.Euler(0f, 10f, 0f) * (at - player.m_eye.position);
            if (dir.sqrMagnitude < 0.01f) return;
            player.SetLookDir(dir);
            LookPitch(player) = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private static IEnumerable Shows(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            if (!check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)")) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(BaitTable.PlainBait);
            var noBait = ObjectDB.instance.GetItemPrefab(NoBait);
            check.That(floatPrefab != null && baitPrefab != null && noBait != null, "the float and the bait prefabs are there");
            if (floatPrefab == null || baitPrefab == null || noBait == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            var wispWas = Settings.FloatWispForTest(false);
            FishingFloat ff = null;
            var placed = new List<GameObject>();
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // A real school of Perch on its way in.
                Schools.RollForTest = 0f;
                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, baitLost: false, bumps: false);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                List<Fish> school = null;
                for (var waited = 0f; waited < 15f && (school == null || school.Count == 0); waited += Time.deltaTime)
                {
                    school = SelfTestArrival.SchoolOf(floatId);
                    yield return null;
                }
                check.That(school != null && school.Count > 0, "plain bait brought a school within 15 s");
                if (school == null || school.Count == 0) yield break;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(school.Where(f => f != null).All(f => Schools.ComingTo(f) == floatId), "every fish of the school is marked for every game as coming to the float");

                var showsFar = new List<float>();
                var showsNear = new List<float>();
                var onTheWay = new List<(float Time, Vector3 At)>();
                var quietShows = new List<(float Time, Vector3 At)>();
                var lastShown = 0;
                var kindWhileComing = ArrivalSignKind.Rings;
                var bit = false;
                var quietSeen = false;
                Fish going = null;
                Fish bitBy = null;
                var madeBefore = ArrivalSigns.PartsMadeForTest();
                var ringFit = -1f;
                var tiltMost = 0f;
                var tiltToward = 0f;
                var lookParts = 0;
                var lookBefore = new List<(Transform T, Quaternion Rot, Vector3 Pos)>();
                foreach (Transform child in ff.transform) lookBefore.Add((child, child.localRotation, child.localPosition));
                for (var waited = 0f; waited < 90f && !bit; waited += Time.deltaTime)
                {
                    var (kind, shown, nearness) = ArrivalSigns.ForTest(floatId);
                    var path = ArrivalSigns.PathForTest(floatId);
                    if (shown > 0) kindWhileComing = kind;
                    if (path.HasValue && path.Value.Quiet) quietSeen = true;
                    foreach (var fish in school)
                        if (fish != null && Schools.GoingFor(fish) == floatId) going = fish;
                    if (shown > lastShown && path.HasValue)
                    {
                        var p = path.Value;
                        if (p.Quiet) quietShows.Add((Time.time, p.ShownAt));
                        else if (!p.Going)
                        {
                            onTheWay.Add((Time.time, p.ShownAt));
                            (nearness < 0.4f ? showsFar : nearness > 0.7f ? showsNear : new List<float>()).Add(Time.time);
                        }
                        lastShown = shown;
                    }
                    // The float trembles toward the fish going for the bait as it closes the last 1.5 m.
                    if (going != null)
                    {
                        var offset = going.transform.position - ff.transform.position;
                        if (new Vector2(offset.x, offset.z).magnitude < FloatTremble.From)
                        {
                            var tremble = ArrivalSigns.TrembleForTest(floatId);
                            lookParts = Mathf.Max(lookParts, tremble.Parts);
                            if (tremble.Degrees > tiltMost)
                            {
                                tiltMost = tremble.Degrees;
                                var (ax, az) = FloatTremble.Axis(offset.x, offset.z);
                                tiltToward = Vector3.Dot(tremble.Axis, new Vector3(ax, 0f, az));
                            }
                        }
                    }
                    bit = Nibbler(ff) != null && NibbleTime(ff) > 0f;
                    if (bit) bitBy = Nibbler(ff);
                    ringFit = Mathf.Max(ringFit, ArrivalSigns.RingFitForTest());
                    yield return null;
                }
                check.That(kindWhileComing == ArrivalSignKind.Rings && lastShown >= 3, "the Perch coming shows its rings on the way in: " + kindWhileComing + ", " + lastShown + " times");
                var farEvery = showsFar.Count > 1 ? (showsFar.Last() - showsFar.First()) / (showsFar.Count - 1) : -1f;
                var nearEvery = showsNear.Count > 1 ? (showsNear.Last() - showsNear.First()) / (showsNear.Count - 1) : -1f;
                if (farEvery > 0f && nearEvery > 0f)
                    check.That(nearEvery < farEvery, "the rings come more often near the float, every " + F(nearEvery) + " s against " + F(farEvery) + " s far off");
                else
                    check.Note("the school was not seen both far off and near, so how often the rings came was not compared");
                check.That(bit, "a Perch bit within 90 s");
                // Near the camera the rings rise and fall with the waves, and the water's roughness sets how clearly they show.
                if (ringFit >= 0f) check.That(ringFit <= 0.08f, "the rings lie on the waves, at worst " + ringFit.ToString("0.000", CultureInfo.InvariantCulture) + " m off the water");
                else check.Note("no ring showed near enough to the camera to check it lies on the waves");
                var water = ArrivalSigns.WaterForTest(spot.Value);
                check.Note("the water at the float: depth " + F(water.Depth) + ", wind " + F(water.Wind) + ", roughness " + F(water.Roughness) + ", signs shown " + (water.Clarity * 100f).ToString("0", CultureInfo.InvariantCulture) + "% clear");
                check.That(water.Depth >= 0f && Mathf.Abs(water.Clarity - WaterMood.Clarity(water.Roughness)) < 0.001f, "the signs read the water at the float: depth " + F(water.Depth) + ", wind " + F(water.Wind) + ", roughness " + F(water.Roughness) + ", shown " + (water.Clarity * 100f).ToString("0", CultureInfo.InvariantCulture) + "% clear");
                var made = ArrivalSigns.PartsMadeForTest() - madeBefore;
                check.That(made <= 8 || made * 2 <= lastShown, "the signs use their parts again: " + made + " parts made for " + lastShown + " showings");

                // The signs walk one path: one showing after another no further apart than the school can swim between them.
                var jumps = new List<string>();
                for (var i = 1; i < onTheWay.Count; i++)
                {
                    var apart = Vector2.Distance(new Vector2(onTheWay[i].At.x, onTheWay[i].At.z), new Vector2(onTheWay[i - 1].At.x, onTheWay[i - 1].At.z));
                    var could = Drift.MaxCruise * (onTheWay[i].Time - onTheWay[i - 1].Time) + 1f;
                    if (apart > could) jumps.Add(F(apart) + " m in " + F(onTheWay[i].Time - onTheWay[i - 1].Time) + " s");
                }
                check.That(onTheWay.Count >= 2 && jumps.Count == 0, "on the way in the rings follow one path, " + onTheWay.Count + " showings, jumps: " + (jumps.Count == 0 ? "none" : string.Join(", ", jumps)));
                // At the float the school goes quiet: slow showings by the float.
                check.That(quietSeen, "the signs went quiet once the school was at the float");
                if (quietShows.Count > 0)
                {
                    var farFromFloat = quietShows.Max(q => Vector2.Distance(new Vector2(q.At.x, q.At.z), new Vector2(spot.Value.x, spot.Value.z)));
                    var quickest = quietShows.Count > 1 ? Enumerable.Range(1, quietShows.Count - 1).Min(i => quietShows[i].Time - quietShows[i - 1].Time) : SignPath.QuietEvery;
                    check.That(farFromFloat < 1f && quickest > SignPath.QuietEvery - 0.2f, "quiet showings come by the float, at most " + F(farFromFloat) + " m off, every " + F(quickest) + " s or slower");
                }
                else check.Note("the school was quiet at the float too briefly for a quiet showing");
                // The fish going for the bait is marked for every game, and it is the one that bites.
                check.That(going != null && going == Nibbler(ff), "the fish marked as going for the bait is the one that bit" + (going == null ? ": none was marked" : ""));
                check.That(lookParts > 0 && tiltMost >= 3f && tiltToward > 0.8f, "the float trembled toward the fish closing in: up to " + F(tiltMost) + " degrees, toward it " + F(tiltToward) + ", " + lookParts + " parts of its look tilting");

                // Hooked, the rest of the school darts off, and the float's look stands upright again.
                if (bitBy != null)
                {
                    var dartsBefore = ArrivalSigns.DartsForTest();
                    var others = school.Count(f => f != null && f != bitBy);
                    Nibbler(ff) = bitBy;
                    NibbleTime(ff) = Time.time - 0.6f;
                    TryToHook.Invoke(ff, null);
                    var hooked = ff.GetCatch();
                    check.That(hooked == bitBy, "reeling just after the bite hooks the Perch");
                    for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime) yield return null;
                    var darted = ArrivalSigns.DartsForTest() - dartsBefore;
                    if (hooked != null) check.That(darted >= others, "the rest of the school darted off when the Perch was hooked: " + darted + " darts for " + others + " fish");
                    var tilted = lookBefore.Where(l => l.T != null && (Quaternion.Angle(l.T.localRotation, l.Rot) > 0.5f || Vector3.Distance(l.T.localPosition, l.Pos) > 0.005f)).Select(l => l.T.name).ToList();
                    check.That(tilted.Count == 0, "after the bite the float's look stands as before" + (tilted.Count == 0 ? "" : ": still tilted " + string.Join(", ", tilted)));
                    if (hooked != null)
                    {
                        SetCatch.Invoke(ff, new object[] { null });
                        hooked.OnHooked(null);
                    }
                }
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(school.Where(f => f != null).All(f => Schools.ComingTo(f).IsNone()), "after the bite no fish of the school is marked as coming any more");
                var afterBite = ArrivalSigns.ForTest(floatId).Shown;
                for (var waited = 0f; waited < 3f; waited += Time.deltaTime) yield return null;
                check.That(ArrivalSigns.ForTest(floatId).Shown == 0 || ArrivalSigns.ForTest(floatId).Shown == afterBite, "after the bite no more signs show");
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                Schools.RollForTest = null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // One of each fish, a few metres off a bare float, marked as coming.
                var stone = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                stone.m_dropPrefab = noBait;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, stone);
                floatId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                var away = (spot.Value - player.transform.position);
                away.y = 0f;
                away = away.normalized;
                var wrong = new List<string>();
                foreach (var (prefab, name) in GameFish)
                {
                    var fishPrefab = ZNetScene.instance.GetPrefab(prefab);
                    if (fishPrefab == null)
                    {
                        wrong.Add(prefab + " not in the game");
                        continue;
                    }
                    var at = spot.Value - away * PictureShort + Vector3.down * 1.5f;
                    var go = Object.Instantiate(fishPrefab, at, Quaternion.LookRotation(-away));
                    placed.Add(go);
                    var fish = go.GetComponent<Fish>();
                    // A travelling sign is pictured on the move: its fish swims across the view at 2 m/s, steered by the test.
                    var across = Vector3.Cross(Vector3.up, away);
                    var swims = Travels(ArrivalSign.Of(prefab));
                    if (swims) fish.m_speed = 0f;
                    yield return null;
                    Schools.MarkComingForTest(fish, floatId);
                    var expected = ArrivalSign.Of(prefab);
                    var shownBy = 0;
                    var kindSeen = ArrivalSignKind.Rings;
                    // The picture is taken just after a showing, a travelling sign after 2 s of its fish swimming.
                    var shownAt = -1f;
                    for (var waited = 0f; waited < 4f; waited += Time.deltaTime)
                    {
                        if (swims) Swim(go, at + across * (-2f + 2f * waited));
                        // The camera follows a swimming fish, so its travelling sign stays in the picture.
                        Aim(player, (swims ? go.transform.position : at) + Vector3.up * 1.5f);
                        var seen = ArrivalSigns.ForTest(floatId);
                        kindSeen = seen.Kind;
                        if (seen.Shown > shownBy && shownAt < 0f && waited > 1f) shownAt = waited;
                        shownBy = seen.Shown;
                        if (swims ? waited >= 2f : shownAt >= 0f && waited >= shownAt + 0.35f) break;
                        yield return null;
                    }
                    if (kindSeen != expected || shownBy == 0) wrong.Add(name + " showed " + kindSeen + " " + shownBy + " times, " + expected + " wanted");
                    if (expected == ArrivalSignKind.Rings)
                    {
                        // A starred fish: a hint while it only comes, clearly bigger once it goes for the bait.
                        var zdo = go.GetComponent<ZNetView>().GetZDO();
                        zdo.Set(ZDOVars.s_quality, 3);
                        for (var waited = 0f; waited < 0.2f; waited += Time.deltaTime) yield return null;
                        var hint = ArrivalSigns.StarsForTest(floatId);
                        Schools.MarkGoingForTest(fish, floatId);
                        for (var waited = 0f; waited < 0.2f; waited += Time.deltaTime) yield return null;
                        var goingStars = ArrivalSigns.StarsForTest(floatId);
                        for (var waited = 0f; waited < 2.5f; waited += Time.deltaTime)
                        {
                            Aim(player, at + Vector3.up * 1.5f);
                            yield return null;
                        }
                        var starredPicture = SelfTestPictures.Take("sign-" + name + "-two-stars-going");
                        // The game keeps one screen capture a frame: the next picture waits for the next frame.
                        yield return null;
                        if (starredPicture != null) wrong.Add("no picture of the starred " + name + ": " + starredPicture);
                        Schools.MarkGoingForTest(fish, ZDOID.None);
                        zdo.Set(ZDOVars.s_quality, 1);
                        // The server's contrast reaches every game's signs: twice as strong, lines half again as wide, pictured.
                        var rulesWere = PlayRules.ForTest;
                        PlayRules.ForTest = PlayRules.Now.With(signContrast: 2f);
                        for (var waited = 0f; waited < 2.5f; waited += Time.deltaTime)
                        {
                            Aim(player, at + Vector3.up * 1.5f);
                            yield return null;
                        }
                        var contrast = ArrivalSigns.ContrastForTest();
                        var strongPicture = SelfTestPictures.Take("sign-" + name + "-contrast-2");
                        yield return null;
                        PlayRules.ForTest = rulesWere;
                        if (strongPicture != null) wrong.Add("no picture of the " + name + " at contrast 2: " + strongPicture);
                        if (Mathf.Abs(contrast.Contrast - 2f) > 0.001f || Mathf.Abs(contrast.LineWidth - SignLight.LineWidth(2f)) > 0.001f)
                            wrong.Add("the server's contrast 2 drew the signs at " + F(contrast.Contrast) + " with lines " + F(contrast.LineWidth) + "x wide");
                        if (Mathf.Abs(hint.Size - SignStars.Size(3, false, 1)) > 0.001f || Mathf.Abs(goingStars.Size - SignStars.Size(3, true, 3)) > 0.001f)
                            wrong.Add("a two-star " + name + " showed " + F(hint.Size) + "x coming and " + F(goingStars.Size) + "x going for the bait, " + F(SignStars.Size(3, false, 1)) + "x and " + F(SignStars.Size(3, true, 3)) + "x wanted");
                    }
                    var trouble = SelfTestPictures.Take("sign-" + name);
                    if (trouble != null) wrong.Add("no picture of the " + name + ": " + trouble);
                    if (swims)
                    {
                        var path = ArrivalSigns.PathForTest(floatId);
                        var facing = path.HasValue ? Vector3.Dot(path.Value.Heading, across) : 0f;
                        if (facing < 0.9f) wrong.Add("the " + name + "'s sign faces " + F(facing) + " along the way it swims, not that way");
                    }
                    if (expected == ArrivalSignKind.Bulge)
                    {
                        // Milling at the float, the bulge fades and a slow ring by the float shows the school is there.
                        var by = spot.Value + across * 1.5f + Vector3.down * 1.5f;
                        for (var waited = 0f; waited < 2.5f; waited += Time.deltaTime)
                        {
                            Swim(go, by);
                            Aim(player, spot.Value + Vector3.up * 1.5f);
                            yield return null;
                        }
                        var quiet = ArrivalSigns.PathForTest(floatId);
                        if (!quiet.HasValue || !quiet.Value.Quiet || quiet.Value.Showing > 0.15f)
                            wrong.Add("at the float the " + name + "'s bulge did not fade: " + (quiet.HasValue ? "quiet " + quiet.Value.Quiet + ", showing " + F(quiet.Value.Showing) : "no sign"));
                        else if (Vector2.Distance(new Vector2(quiet.Value.ShownAt.x, quiet.Value.ShownAt.z), new Vector2(spot.Value.x, spot.Value.z)) > 1f)
                            wrong.Add("at the float the " + name + " left no ring by the float");
                        trouble = SelfTestPictures.Take("sign-" + name + "-at-float");
                        if (trouble != null) wrong.Add("no picture of the " + name + " at the float: " + trouble);
                    }
                    Schools.MarkComingForTest(fish, ZDOID.None);
                    ZNetScene.instance.Destroy(go);
                    placed.Remove(go);
                    // The float's signs end with the last fish coming to it, so the next fish starts afresh.
                    for (var waited = 0f; waited < 0.3f; waited += Time.deltaTime) yield return null;
                }
                check.That(wrong.Count == 0, "each fish of the game coming to a float shows its own sign: " + string.Join("; ", wrong));
                // The signs are lit by the game's own light this frame, readable however dark.
                var light = ArrivalSigns.LightForTest();
                var expect = SignLight.Crest(new Shade(light.Sun.r, light.Sun.g, light.Sun.b), light.Intensity, light.SunUp, new Shade(light.Ambient.r, light.Ambient.g, light.Ambient.b));
                var crest = new Shade(light.Crest.r, light.Crest.g, light.Crest.b);
                var trough = new Shade(light.Trough.r, light.Trough.g, light.Trough.b);
                check.That(Mathf.Abs(crest.R - expect.R) < 0.01f && Mathf.Abs(crest.G - expect.G) < 0.01f && Mathf.Abs(crest.B - expect.B) < 0.01f
                    && crest.Luminance >= SignLight.MinLight - 0.01f && trough.Luminance < crest.Luminance,
                    "the signs take the game's light: crest " + crest + " (wanted " + expect + "), trough " + trough);
                check.Note("the game's light: sun " + new Shade(light.Sun.r, light.Sun.g, light.Sun.b) + " x " + F(light.Intensity) + ", " + F(light.SunUp) + " up, ambient "
                    + new Shade(light.Ambient.r, light.Ambient.g, light.Ambient.b) + ", fog " + light.Fog + " density " + light.FogDensity.ToString("0.0000", CultureInfo.InvariantCulture));
                check.Note("sign pictures taken at " +(EnvMan.instance != null ? (EnvMan.instance.GetDayFraction() * 24f).ToString("00.0", CultureInfo.InvariantCulture) : "?") + " o'clock in game");
            }
            finally
            {
                Schools.RollForTest = null;
                PlayRules.ForTest = null;
                Settings.FloatWispForTest(wispWas);
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                foreach (var go in placed)
                    if (go != null) ZNetScene.instance.Destroy(go);
                gear.PutBack();
                if (player != null)
                {
                    LookYaw(player) = yaw;
                    LookPitch(player) = pitch;
                }
            }
        }
    }
}
