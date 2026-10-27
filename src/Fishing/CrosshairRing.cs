using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Rodnarok
{
    /// <summary>
    /// The crosshair ring tells the bite and the fight as one ring of one size: after a bite its arc
    /// drains clockwise from full white toward yellow as the hook window runs out; in the fight it
    /// is red while the fish struggles and yellow while it rests, and with tension its arc is the
    /// line's strain filling clockwise over a faint full track. It is a ring of its own, made from
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

        /// <summary>How far the outline reaches around the arc's line, in the hud's units.</summary>
        private const float Thickness = 1.5f;

        /// <summary>The ring as drawn, for the self-test; null before the first draw.</summary>
        [ForTest]
        public static Image ImageForTest => _ring;

        /// <summary>The track behind the ring as drawn, for the self-test; null before the first draw.</summary>
        [ForTest]
        public static Image TrackForTest => _track;

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
                return;
            }
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            var scale = new Vector3(draw.Scale, draw.Scale, draw.Scale);
            ring.transform.localScale = scale;
            ring.fillAmount = draw.Fill;
            ring.color = new Color(draw.R, draw.G, draw.B, draw.A);
            if (_thickness != null) _thickness.effectColor = ring.color;

            var trackShown = draw.TrackA > 0f;
            if (_track.gameObject.activeSelf != trackShown) _track.gameObject.SetActive(trackShown);
            if (!trackShown) return;
            _track.transform.localScale = scale;
            _track.color = new Color(draw.R, draw.G, draw.B, draw.TrackA);
            if (_trackThickness != null) _trackThickness.effectColor = _track.color;
        }

        /// <summary>The ring of this hud and its track behind it, made once from the game's bow ring: its picture, drawn as an arc filled clockwise from the top.</summary>
        private static Image RingOf(Hud hud)
        {
            if (_ring != null) return _ring;
            var bow = hud.m_crosshairBow;
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

        /// <summary>The ring a float calls for: the fight while a fish is on the line, else the bite while its window lasts.</summary>
        private static Ring RingFor(FishingFloat ff)
        {
            if (ff == null) return Ring.Hidden;
            var fish = ff.GetCatch();
            if (fish != null)
                return PlayRules.Now.Tension ? Ring.ForFight(fish.IsEscaping(), Lines.StrainOf(ff)) : Ring.ForFight(fish.IsEscaping());
            if (Nibbler(ff) == null) return Ring.Hidden;
            return Ring.ForBite(Time.time - NibbleTime(ff), PlayRules.Now.HookWindow);
        }

        /// <summary>The float this game cast, if one is out.</summary>
        private static FishingFloat OwnFloat()
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
