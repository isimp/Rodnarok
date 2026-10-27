using System;
using System.Linq;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Rodnarok
{
    /// <summary>
    /// Runs a step right after the game's animators have posed the characters, before any script of
    /// the frame reads a bone. An animator that moves with the physics poses its character in the
    /// physics step, which some frames have and some do not, and one that moves with the frame poses
    /// it just before the frame's late scripts; a change to a bone made in a late script of its own
    /// is seen by the scripts that run before it on the frames the animator posed, and not on the
    /// others, so a line drawn from a hand flickers between the two. The step is placed into Unity's
    /// own loop after both.
    /// </summary>
    internal static class AfterAnimation
    {
        /// <summary>The marks of the step in Unity's loop, after the physics step's animation and after the frame's.</summary>
        private static class AfterPhysicsStep
        {
        }

        private static class AfterFrame
        {
        }

        /// <summary>Places the step after both; false where Unity's loop lacks either place, nothing then placed.</summary>
        public static bool Hook(PlayerLoopSystem.UpdateFunction step)
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (!Insert(ref loop, typeof(FixedUpdate.DirectorFixedUpdatePostPhysics), typeof(AfterPhysicsStep), step)) return false;
            if (!Insert(ref loop, typeof(PreLateUpdate.DirectorUpdateAnimationEnd), typeof(AfterFrame), step)) return false;
            PlayerLoop.SetPlayerLoop(loop);
            return true;
        }

        /// <summary>How many places the step has in Unity's loop now, for the self-test: two when placed.</summary>
        [ForTest]
        public static int PlacesForTest() => Count(PlayerLoop.GetCurrentPlayerLoop());

        private static int Count(PlayerLoopSystem system) =>
            system.subSystemList == null ? 0 : system.subSystemList.Sum(s => (s.type == typeof(AfterPhysicsStep) || s.type == typeof(AfterFrame) ? 1 : 0) + Count(s));

        /// <summary>Takes the step out of Unity's loop again.</summary>
        public static void Unhook()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Remove(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
        }

        private static bool Insert(ref PlayerLoopSystem system, Type after, Type mark, PlayerLoopSystem.UpdateFunction step)
        {
            var subs = system.subSystemList;
            if (subs == null) return false;
            var at = Array.FindIndex(subs, s => s.type == after);
            if (at >= 0)
            {
                var list = subs.ToList();
                list.Insert(at + 1, new PlayerLoopSystem { type = mark, updateDelegate = step });
                system.subSystemList = list.ToArray();
                return true;
            }
            for (var i = 0; i < subs.Length; i++)
            {
                var sub = subs[i];
                if (!Insert(ref sub, after, mark, step)) continue;
                subs[i] = sub;
                return true;
            }
            return false;
        }

        private static void Remove(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null) return;
            system.subSystemList = system.subSystemList.Where(s => s.type != typeof(AfterPhysicsStep) && s.type != typeof(AfterFrame)).ToArray();
            for (var i = 0; i < system.subSystemList.Length; i++)
            {
                var sub = system.subSystemList[i];
                Remove(ref sub);
                system.subSystemList[i] = sub;
            }
        }
    }
}
