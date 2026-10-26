using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Rodnarok
{
    /// <summary>
    /// The crosshair ring tells the bite and the fight as one ring of one size: after a bite its arc
    /// drains clockwise from full white toward yellow as the hook window runs out; while a hooked
    /// fish struggles the whole ring is red, while it rests yellow. It is a ring of its own, made
    /// from the game's bow ring beside it, so the bow's ring is left as the game draws it and the
    /// two are never seen at once: while a bow or the rod is drawn this ring is hidden.
    /// </summary>
    internal static class CrosshairRing
    {
        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        private static Image _ring;
        private static Outline _thickness;

        /// <summary>How far the outline reaches around the arc's line, in the hud's units.</summary>
        private const float Thickness = 1.5f;

        /// <summary>The ring as drawn, for the self-test; null before the first draw.</summary>
        [ForTest]
        public static Image ImageForTest => _ring;

        /// <summary>After the game set the crosshair for this frame, draws the ring for the player's own float.</summary>
        public static void Draw(Hud hud, float bowDrawPercentage)
        {
            if (hud == null || hud.m_crosshairBow == null) return;
            var ring = RingOf(hud);
            var draw = RingDraw.For(RingFor(OwnFloat()));
            if (!draw.Shown || bowDrawPercentage > 0f)
            {
                if (ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                return;
            }
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            ring.transform.localScale = new Vector3(draw.Scale, draw.Scale, draw.Scale);
            ring.fillAmount = draw.Fill;
            ring.color = new Color(draw.R, draw.G, draw.B, draw.A);
            if (_thickness != null) _thickness.effectColor = ring.color;
        }

        /// <summary>The ring of this hud, made once from the game's bow ring: its picture, drawn as an arc filled clockwise from the top.</summary>
        private static Image RingOf(Hud hud)
        {
            if (_ring != null) return _ring;
            var bow = hud.m_crosshairBow;
            var go = Object.Instantiate(bow.gameObject, bow.transform.parent);
            go.name = "RodnarokRing";
            go.SetActive(false);
            _ring = go.GetComponent<Image>();
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = true;
            _ring.fillAmount = 1f;
            // The bow ring's line, drawn at the ring's smaller size, is thin; an outline of its own
            // colour around the arc gives it body.
            _thickness = go.AddComponent<Outline>();
            _thickness.effectDistance = new Vector2(Thickness, Thickness);
            _thickness.useGraphicAlpha = true;
            return _ring;
        }

        /// <summary>The ring a float calls for: the fight while a fish is on the line, else the bite while its window lasts.</summary>
        private static Ring RingFor(FishingFloat ff)
        {
            if (ff == null) return Ring.Hidden;
            var fish = ff.GetCatch();
            if (fish != null) return Ring.ForFight(fish.IsEscaping());
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
