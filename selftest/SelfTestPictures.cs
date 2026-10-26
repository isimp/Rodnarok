using System;
using System.IO;
using System.Reflection;

namespace Rodnarok
{
    /// <summary>
    /// Pictures of the screen a scenario takes to be looked at, in
    /// <c>BepInEx/Rodnarok-selftest-pictures</c>, each named for what it shows. The game's own
    /// screen capture is found by name, so the self-test builds without its module; it saves the
    /// screen once the frame is drawn.
    /// </summary>
    internal static class SelfTestPictures
    {
        private static readonly MethodInfo Capture =
            Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule", false)?.GetMethod("CaptureScreenshot", new[] { typeof(string) });

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
    }
}
