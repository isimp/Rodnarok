using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Rodnarok
{
    /// <summary>
    /// The crosshair ring tells the bite and the fight as one ring of one size: after a bite its arc
    /// drains clockwise from full white toward yellow as the hook window runs out; in the fight it
    /// is red while the fish struggles and yellow while it rests, and with tension its arc is the
    /// line's strain filling clockwise over a faint full track. After a heave the rest's track empties
    /// and refills counter-clockwise as its slack is wound in, and a heave refused for that slack
    /// shakes the ring sideways. It is a ring of its own, made from
    /// the game's bow ring beside it, so the bow's ring is left as the game draws it and the two are
    /// never seen at once: while a bow or the rod is drawn this ring is hidden.
    /// </summary>
    internal static class CrosshairRing
    {
        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        private static Image _ring;
        private static Outline _thickness;
        private static Image _track;
        private static Outline _trackThickness;
        private static Image _under;
        private static Outline _underThickness;

        /// <summary>Where the ring sits unshaken, as the game's bow ring sits.</summary>
        private static Vector2 _at;

        /// <summary>How far the outline reaches around the arc's line, in the hud's units.</summary>
        private const float Thickness = 1.5f;

        /// <summary>The ring as drawn, for the self-test; null before the first draw.</summary>
        [ForTest]
        public static Image ImageForTest => _ring;

        /// <summary>The track behind the ring as drawn, for the self-test; null before the first draw.</summary>
        [ForTest]
        public static Image TrackForTest => _track;

        /// <summary>The fainter ring where the rest's track has not refilled yet, for the self-test; null before the first draw.</summary>
        [ForTest]
        public static Image UnderForTest => _under;

        /// <summary>How far aside the ring was last drawn, for the self-test.</summary>
        [ForTest]
        public static float ShakeForTest { get; private set; }

        /// <summary>After the game set the crosshair for this frame, draws the ring for the player's own float.</summary>
        public static void Draw(Hud hud, float bowDrawPercentage)
        {
            if (hud == null || hud.m_crosshairBow == null) return;
            var ring = RingOf(hud);
            var draw = RingDraw.For(RingFor(OwnFloat()));
            if (!draw.Shown || bowDrawPercentage > 0f)
            {
                if (ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                if (_track.gameObject.activeSelf) _track.gameObject.SetActive(false);
                if (_under.gameObject.activeSelf) _under.gameObject.SetActive(false);
                return;
            }
            ShakeForTest = Heave.Shake(Time.time - Heaves.RefusedAt);
            var at = _at + new Vector2(ShakeForTest, 0f);
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            var scale = new Vector3(draw.Scale, draw.Scale, draw.Scale);
            ring.transform.localScale = scale;
            ((RectTransform)ring.transform).anchoredPosition = at;
            ring.fillClockwise = !draw.ArcCounter;
            ring.fillAmount = draw.Fill;
            ring.color = new Color(draw.R, draw.G, draw.B, draw.A);
            if (_thickness != null) _thickness.effectColor = ring.color;

            Part(_track, _trackThickness, draw.TrackA, draw.TrackFill, draw.TrackClockwise, draw, scale, at);
            Part(_under, _underThickness, draw.UnderA, draw.UnderFill, true, draw, scale, at);
        }

        /// <summary>Draws one of the fainter rings behind the arc in the arc's colour, or hides it at no opacity.</summary>
        private static void Part(Image image, Outline thickness, float alpha, float fill, bool clockwise, RingDraw draw, Vector3 scale, Vector2 at)
        {
            var shown = alpha > 0f && fill > 0f;
            if (image.gameObject.activeSelf != shown) image.gameObject.SetActive(shown);
            if (!shown) return;
            image.transform.localScale = scale;
            ((RectTransform)image.transform).anchoredPosition = at;
            image.fillClockwise = clockwise;
            image.fillAmount = fill;
            image.color = new Color(draw.R, draw.G, draw.B, alpha);
            if (thickness != null) thickness.effectColor = image.color;
        }

        /// <summary>The ring of this hud and its track behind it, made once from the game's bow ring: its picture, drawn as an arc filled clockwise from the top.</summary>
        private static Image RingOf(Hud hud)
        {
            if (_ring != null) return _ring;
            var bow = hud.m_crosshairBow;
            _at = ((RectTransform)bow.transform).anchoredPosition;
            _under = Make(bow, "RodnarokRingUnder", out _underThickness);
            _track = Make(bow, "RodnarokRingTrack", out _trackThickness);
            _ring = Make(bow, "RodnarokRing", out _thickness);
            return _ring;
        }

        private static Image Make(Image bow, string name, out Outline thickness)
        {
            var go = Object.Instantiate(bow.gameObject, bow.transform.parent);
            go.name = name;
            go.SetActive(false);
            var image = go.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 1f;
            // The bow ring's line, drawn at the ring's smaller size, is thin; an outline of its own
            // colour around the arc gives it body.
            thickness = go.AddComponent<Outline>();
            thickness.effectDistance = new Vector2(Thickness, Thickness);
            thickness.useGraphicAlpha = true;
            return image;
        }

        /// <summary>
        /// The ring a float calls for: the fight while a fish is on the line, else the bite while its
        /// window lasts, else, with a school at the float, a ring growing stronger as the fish comes closer.
        /// </summary>
        private static Ring RingFor(FishingFloat ff)
        {
            if (ff == null) return Ring.Hidden;
            var fish = ff.GetCatch();
            if (fish != null)
            {
                var wound = Heave.Wound(Heaves.HeaveSlackOf(fish));
                return PlayRules.Now.Tension ? Ring.ForFight(fish.IsEscaping(), Lines.StrainOf(ff), wound) : Ring.ForFight(fish.IsEscaping(), wound);
            }
            if (Nibbler(ff) != null)
            {
                var bite = Ring.ForBite(Time.time - NibbleTime(ff), PlayRules.Now.HookWindow);
                if (bite.Look != RingLook.Hidden) return bite;
            }
            return Schools.ApproachRing(ff);
        }

        /// <summary>The float this game cast, if one is out.</summary>
        internal static FishingFloat OwnFloat()
        {
            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null) continue;
                var view = ff.GetComponent<ZNetView>();
                if (view != null && view.IsValid() && view.IsOwner()) return ff;
            }
            return null;
        }
    }
}
