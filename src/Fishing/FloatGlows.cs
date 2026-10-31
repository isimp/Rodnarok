using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Every fishing float this game shows carries a small light that follows <see cref="FloatGlow"/>:
    /// a soft glow by night, a white flash at a bite fading over the hook window. A bite is known
    /// only in the game of the one fishing, so other players' floats glow without the flash. With
    /// the player's FloatGlow setting off no float carries a light.
    /// </summary>
    internal static class FloatGlows
    {
        /// <summary>Where on the float the light sits, just above its top, in metres.</summary>
        private static readonly Vector3 Above = new Vector3(0f, 0.15f, 0f);

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        private static readonly Dictionary<FishingFloat, Light> Lights = new Dictionary<FishingFloat, Light>();
        private static readonly List<FishingFloat> Gone = new List<FishingFloat>();

        /// <summary>A darkness the self-test sets in place of the time of day's; null for the time of day's.</summary>
        [ForTest]
        public static float? DarknessForTest { get; set; }

        /// <summary>The light a float carries, null for none, for the self-test.</summary>
        [ForTest]
        public static Light LightForTest(FishingFloat ff) => ff != null && Lights.TryGetValue(ff, out var light) ? light : null;

        /// <summary>Each frame: every float shown carries its light at the darkness and bite of the moment.</summary>
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
            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null) continue;
                if (!Lights.TryGetValue(ff, out var light) || light == null)
                {
                    light = Make(ff);
                    Lights[ff] = light;
                }
                // The flash runs from a bite until it is hooked or the hook window runs out, as the ring's does.
                var sinceBite = Nibbler(ff) != null && ff.GetCatch() == null && NibbleTime(ff) > 0f ? Time.time - NibbleTime(ff) : float.PositiveInfinity;
                var look = FloatGlow.Look(darkness, sinceBite, window);
                light.enabled = look.On;
                if (!look.On) continue;
                light.color = new Color(look.R, look.G, look.B);
                light.intensity = look.Intensity;
                light.range = look.Range;
            }
            Sweep();
        }

        private static Light Make(FishingFloat ff)
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
            return light;
        }

        /// <summary>Lets go of floats no longer there.</summary>
        private static void Sweep()
        {
            Gone.Clear();
            foreach (var pair in Lights)
                if (pair.Key == null) Gone.Add(pair.Key);
            foreach (var ff in Gone) Lights.Remove(ff);
        }

        /// <summary>Takes every float's light away, on leaving a world or with the setting off.</summary>
        public static void Forget()
        {
            foreach (var light in Lights.Values)
                if (light != null) Object.Destroy(light.gameObject);
            Lights.Clear();
        }
    }
}
