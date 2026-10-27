using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rodnarok
{
    /// <summary>
    /// Pictures of the screen a scenario takes to be looked at, in
    /// <c>BepInEx/Rodnarok-selftest-pictures</c>, each named for what it shows. The game's own
    /// screen capture is found by name, so the self-test builds without its module; it saves the
    /// screen once the frame is drawn. Panels for a picture of several moments are grabbed the
    /// same way, cropped around the crosshair and what lies below it, each with a caption in the
    /// game's own message font, and put together two by two.
    /// </summary>
    internal static class SelfTestPictures
    {
        private static readonly Type ScreenCaptureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule", false);
        private static readonly MethodInfo Capture = ScreenCaptureType?.GetMethod("CaptureScreenshot", new[] { typeof(string) });
        private static readonly MethodInfo CaptureTexture = ScreenCaptureType?.GetMethod("CaptureScreenshotAsTexture", Type.EmptyTypes);
        private static readonly MethodInfo EncodePng = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false)?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });

        /// <summary>The share of the screen a panel shows: its width, and how far it reaches above and below the crosshair.</summary>
        private const float PanelWidth = 0.42f;
        private const float PanelAbove = 0.17f;
        private const float PanelBelow = 0.27f;

        /// <summary>How far under a panel's top edge its caption sits, as a share of the screen's height.</summary>
        private const float CaptionDown = 0.045f;

        /// <summary>The gap between panels and around them, in pixels, and its colour.</summary>
        private const int Gap = 12;
        private static readonly Color Back = new Color(0.08f, 0.08f, 0.08f);

        private static readonly Dictionary<string, Texture2D> Panels = new Dictionary<string, Texture2D>();

        public static string Folder => Path.Combine(BepInEx.Paths.BepInExRootPath, "Rodnarok-selftest-pictures");

        /// <summary>Takes a picture named for what it shows; says why when it cannot.</summary>
        public static string Take(string what)
        {
            if (Capture == null) return "the game's screen capture is not there";
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, what + ".png");
            Capture.Invoke(null, new object[] { path });
            return null;
        }

        /// <summary>Whether a panel of this name was grabbed.</summary>
        public static bool Has(string name) => Panels.ContainsKey(name);

        /// <summary>
        /// Grabs a panel of this frame once it is drawn, with its caption shown on the screen for
        /// the frame and the game's centre message hidden for it; says why when it cannot.
        /// </summary>
        public static string Grab(string name, string caption)
        {
            if (CaptureTexture == null) return "the game's screen capture is not there";
            if (SelfTestPlugin.Instance == null) return "the self-test plugin is not there";
            var message = MessageHud.instance != null ? MessageHud.instance.m_messageCenterText : null;
            if (message == null) return "the game's message text is not there";
            var label = CaptionAt(message, caption);
            var messageAlpha = message.canvasRenderer.GetAlpha();
            message.canvasRenderer.SetAlpha(0f);
            SelfTestPlugin.Instance.StartCoroutine(GrabWhenDrawn(name, label, message, messageAlpha));
            return null;
        }

        /// <summary>Puts the panels grabbed under these names together two by two, in reading order, as one picture; says why when it cannot.</summary>
        public static string Compose(string what, IReadOnlyList<string> names)
        {
            if (EncodePng == null) return "the game's picture encoding is not there";
            var missing = new List<string>();
            foreach (var name in names) if (!Panels.ContainsKey(name)) missing.Add(name);
            if (missing.Count > 0) return "no panel of " + string.Join(", ", missing);
            var first = Panels[names[0]];
            int w = first.width, h = first.height;
            foreach (var name in names)
                if (Panels[name].width != w || Panels[name].height != h) return "the panels differ in size, the screen changed while they were grabbed";

            var columns = 2;
            var rows = (names.Count + 1) / 2;
            var sheet = new Texture2D(columns * w + (columns + 1) * Gap, rows * h + (rows + 1) * Gap, TextureFormat.RGB24, false);
            var back = new Color[sheet.width * sheet.height];
            for (var i = 0; i < back.Length; i++) back[i] = Back;
            sheet.SetPixels(back);
            for (var i = 0; i < names.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                // Textures count rows from the bottom, so the first row of panels is the highest.
                var x = Gap + column * (w + Gap);
                var y = sheet.height - (row + 1) * (h + Gap);
                sheet.SetPixels(x, y, w, h, Panels[names[i]].GetPixels());
            }
            var png = (byte[])EncodePng.Invoke(null, new object[] { sheet });
            Object.Destroy(sheet);
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, what + ".png"), png);
            return null;
        }

        /// <summary>Lets go of every grabbed panel.</summary>
        public static void Forget()
        {
            foreach (var panel in Panels.Values) Object.Destroy(panel);
            Panels.Clear();
        }

        private static IEnumerator GrabWhenDrawn(string name, TMP_Text label, TMP_Text message, float messageAlpha)
        {
            yield return new WaitForEndOfFrame();
            var screen = (Texture2D)CaptureTexture.Invoke(null, null);
            Object.Destroy(label.gameObject);
            message.canvasRenderer.SetAlpha(messageAlpha);
            if (screen == null) yield break;
            var w = Mathf.RoundToInt(screen.width * PanelWidth);
            var h = Mathf.RoundToInt(screen.height * (PanelAbove + PanelBelow));
            var x = (screen.width - w) / 2;
            var y = Mathf.RoundToInt(screen.height * (0.5f - PanelBelow));
            var panel = new Texture2D(w, h, TextureFormat.RGB24, false);
            panel.SetPixels(screen.GetPixels(x, y, w, h));
            Object.Destroy(screen);
            if (Panels.TryGetValue(name, out var old)) Object.Destroy(old);
            Panels[name] = panel;
        }

        /// <summary>A caption in the game's message font and colour, at the top of where a panel is cut from the screen.</summary>
        private static TMP_Text CaptionAt(TMP_Text message, string caption)
        {
            var label = Object.Instantiate(message.gameObject, message.transform.parent).GetComponent<TMP_Text>();
            label.gameObject.name = "RodnarokSelfTestCaption";
            label.text = caption;
            label.canvasRenderer.SetAlpha(1f);
            var rect = (RectTransform)label.transform;
            var parent = (RectTransform)rect.parent;
            var canvas = label.canvas;
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var point = new Vector2(Screen.width * 0.5f, Screen.height * (0.5f + PanelAbove - CaptionDown));
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, point, camera, out var world)) rect.position = world;
            return label;
        }
    }
}
