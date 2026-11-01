using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Every fishing float this game shows carries a small light and a glowing bead that follow
    /// <see cref="FloatGlow"/>: a soft glow by night, a white flash at a bite fading over the hook
    /// window. The bead is a round sprite turned to the camera, drawn without the scene's light so
    /// it shows from far off; the light brightens the float and the water close to it. A bite is
    /// known only in the game of the one fishing, so other players' floats glow without the
    /// flash. With the player's FloatGlow setting off no float carries either.
    /// </summary>
    internal static class FloatGlows
    {
        /// <summary>Where on the float the light and the bead sit, just above its top, in metres.</summary>
        private static readonly Vector3 Above = new Vector3(0f, 0.15f, 0f);

        /// <summary>The bead's picture, in pixels a side.</summary>
        private const int BeadPixels = 64;

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        private sealed class Glow
        {
            public Light Light;
            public SpriteRenderer Bead;
        }

        private static readonly Dictionary<FishingFloat, Glow> Glows = new Dictionary<FishingFloat, Glow>();
        private static readonly List<FishingFloat> Gone = new List<FishingFloat>();
        private static Sprite _beadSprite;

        /// <summary>A darkness the self-test sets in place of the time of day's; null for the time of day's.</summary>
        [ForTest]
        public static float? DarknessForTest { get; set; }

        /// <summary>The light a float carries, null for none, for the self-test.</summary>
        [ForTest]
        public static Light LightForTest(FishingFloat ff) => ff != null && Glows.TryGetValue(ff, out var glow) ? glow.Light : null;

        /// <summary>The bead a float carries, null for none, for the self-test.</summary>
        [ForTest]
        public static SpriteRenderer BeadForTest(FishingFloat ff) => ff != null && Glows.TryGetValue(ff, out var glow) ? glow.Bead : null;

        /// <summary>Each frame: every float shown carries its glow at the darkness and bite of the moment.</summary>
        public static void Tick()
        {
            if (!Settings.FloatGlow)
            {
                Forget();
                return;
            }
            var env = EnvMan.instance;
            var darkness = DarknessForTest ?? (env != null ? FloatGlow.Darkness(env.GetDayFraction()) : 0f);
            var window = PlayRules.Now.HookWindow;
            var camera = Utils.GetMainCamera();
            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null) continue;
                if (!Glows.TryGetValue(ff, out var glow) || glow.Light == null || glow.Bead == null)
                {
                    if (glow != null) Take(glow);
                    glow = Make(ff);
                    Glows[ff] = glow;
                }
                // The flash runs from a bite until it is hooked or the hook window runs out, as the ring's does.
                var sinceBite = Nibbler(ff) != null && ff.GetCatch() == null && NibbleTime(ff) > 0f ? Time.time - NibbleTime(ff) : float.PositiveInfinity;
                var look = FloatGlow.Look(darkness, sinceBite, window);
                glow.Light.enabled = look.On;
                glow.Bead.enabled = look.On;
                if (!look.On) continue;
                var colour = new Color(look.R, look.G, look.B);
                glow.Light.color = colour;
                glow.Light.intensity = look.Intensity;
                glow.Light.range = look.Range;
                glow.Bead.color = new Color(look.R, look.G, look.B, look.Alpha);
                var parentScale = ff.transform.lossyScale.x;
                glow.Bead.transform.localScale = Vector3.one * (look.Size / (parentScale > 0f ? parentScale : 1f));
                if (camera != null) glow.Bead.transform.rotation = camera.transform.rotation;
            }
            Sweep();
        }

        private static Glow Make(FishingFloat ff)
        {
            var go = new GameObject("RodnarokGlow");
            go.transform.SetParent(ff.transform, false);
            go.transform.localPosition = Above;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            // Drawn per pixel, so the light shows on the float and the water around it.
            light.renderMode = LightRenderMode.ForcePixel;
            light.enabled = false;

            var beadObject = new GameObject("RodnarokGlowBead");
            beadObject.transform.SetParent(go.transform, false);
            var bead = beadObject.AddComponent<SpriteRenderer>();
            bead.sprite = BeadSprite();
            bead.enabled = false;
            return new Glow { Light = light, Bead = bead };
        }

        /// <summary>A round white spot, solid in the middle and fading to nothing at the edge, tinted by the bead's colour.</summary>
        private static Sprite BeadSprite()
        {
            if (_beadSprite != null) return _beadSprite;
            var texture = new Texture2D(BeadPixels, BeadPixels, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RodnarokGlowBead" };
            var centre = (BeadPixels - 1) / 2f;
            for (var y = 0; y < BeadPixels; y++)
            for (var x = 0; x < BeadPixels; x++)
            {
                var r = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                var a = r <= 0.3f ? 1f : Mathf.Clamp01(1f - (r - 0.3f) / 0.7f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            texture.Apply();
            _beadSprite = Sprite.Create(texture, new Rect(0f, 0f, BeadPixels, BeadPixels), new Vector2(0.5f, 0.5f), BeadPixels);
            return _beadSprite;
        }

        private static void Take(Glow glow)
        {
            if (glow.Light != null) Object.Destroy(glow.Light.gameObject);
            else if (glow.Bead != null) Object.Destroy(glow.Bead.gameObject);
        }

        /// <summary>Lets go of floats no longer there.</summary>
        private static void Sweep()
        {
            Gone.Clear();
            foreach (var pair in Glows)
                if (pair.Key == null) Gone.Add(pair.Key);
            foreach (var ff in Gone) Glows.Remove(ff);
        }

        /// <summary>Takes every float's glow away, on leaving a world or with the setting off.</summary>
        public static void Forget()
        {
            foreach (var glow in Glows.Values) Take(glow);
            Glows.Clear();
        }
    }
}
