using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Three looks for the Serpent post side by side, for choosing one: a tall darkwood totem with
    /// the head on top tilted toward the water, a post with a beam reaching out over the water and
    /// the head at its tip like a longship's prow, and a post with the game's horizontal item stand
    /// holding the trophy as the game hangs trophies. They are made as the game makes building
    /// ghosts, so nothing of them reaches the world or its save, stand on dry land beside the
    /// character facing the water, and are pictured in <c>post-looks.png</c>, then taken away.
    /// </summary>
    internal static class SelfTestPostLooks
    {
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");
        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");

        /// <summary>How far apart the three looks stand, and how large the head is drawn on each, in metres.</summary>
        private const float Apart = 3.5f;
        private const float HeadSize = 1f;

        /// <summary>The game's parts the looks are made of.</summary>
        private static readonly string[] PartNames = { "darkwood_pole4", "darkwood_pole", "darkwood_beam", "itemstandh" };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("SerpentPostLooksSideBySide", Looks);
        }

        private static IEnumerable Looks(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var water = SelfTestArrival.DeepWaterBeside(player.transform.position, 1f);
            check.That(water.HasValue, "water within 6 to 20 m of the character");
            if (!water.HasValue) yield break;
            var parts = PartNames.ToDictionary(n => n, n => ZNetScene.instance.GetPrefab(n));
            var trophy = ObjectDB.instance.GetItemPrefab("TrophySerpent");
            foreach (var part in parts) check.That(part.Value != null, part.Key + " is a prefab of the game");
            check.That(trophy != null, "the Serpent trophy is an item of the game");
            if (parts.Values.Any(p => p == null) || trophy == null) yield break;

            var toWater = water.Value - player.transform.position;
            toWater.y = 0f;
            toWater.Normalize();
            var side = Vector3.Cross(Vector3.up, toWater);
            var middle = player.transform.position + toWater * 2.5f;
            var made = new List<GameObject>();
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            try
            {
                Totem(made, parts, trophy, Ground(middle - side * Apart), toWater);
                Prow(made, parts, trophy, Ground(middle), toWater);
                Stand(made, parts, trophy, Ground(middle + side * Apart), toWater);

                // Seen from the side, a step back, so all three show against the water.
                var eye = player.m_eye.position;
                var look = middle + Vector3.up * 1.8f - eye;
                player.SetLookDir(look);
                LookPitch(player) = -Mathf.Asin(Mathf.Clamp(look.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                var trouble = SelfTestPictures.Take("post-looks");
                check.That(trouble == null, "a picture of the three looks is taken: " + trouble);
                yield return null;
                check.Note("left to right as the character looks: totem, prow, item stand");
            }
            finally
            {
                foreach (var go in made) if (go != null) Object.Destroy(go);
                LookYaw(player) = yaw;
                LookPitch(player) = pitch;
            }
        }

        /// <summary>A tall darkwood pole with the head on top, tilted down toward the water.</summary>
        private static void Totem(List<GameObject> made, Dictionary<string, GameObject> parts, GameObject trophy, Vector3 at, Vector3 toWater)
        {
            var pole = Ghost(made, parts["darkwood_pole4"], at, Quaternion.LookRotation(toWater));
            var top = TopOf(pole);
            var head = Head(made, trophy, HeadSize);
            head.transform.rotation = Quaternion.LookRotation(toWater) * Quaternion.Euler(20f, 0f, 0f);
            Rest(head, new Vector3(at.x, top, at.z));
        }

        /// <summary>A post with a beam reaching out over the water from its top, the head at the beam's tip facing out.</summary>
        private static void Prow(List<GameObject> made, Dictionary<string, GameObject> parts, GameObject trophy, Vector3 at, Vector3 toWater)
        {
            var pole = Ghost(made, parts["darkwood_pole"], at, Quaternion.LookRotation(toWater));
            var top = TopOf(pole);
            var beam = Ghost(made, parts["darkwood_beam"], new Vector3(at.x, top - 0.2f, at.z), Quaternion.LookRotation(Vector3.Cross(toWater, Vector3.up)));
            var bounds = BoundsOf(beam);
            var tip = at + toWater * (bounds.extents.magnitude * 0.9f);
            var head = Head(made, trophy, HeadSize * 0.9f);
            head.transform.rotation = Quaternion.LookRotation(toWater);
            Rest(head, new Vector3(tip.x, bounds.max.y, tip.z));
        }

        /// <summary>A post with the game's horizontal item stand on top, holding the trophy as the game hangs one.</summary>
        private static void Stand(List<GameObject> made, Dictionary<string, GameObject> parts, GameObject trophy, Vector3 at, Vector3 toWater)
        {
            var pole = Ghost(made, parts["darkwood_pole"], at, Quaternion.LookRotation(toWater));
            var top = TopOf(pole);
            var stand = Ghost(made, parts["itemstandh"], new Vector3(at.x, top, at.z), Quaternion.LookRotation(-toWater));
            var attach = stand.GetComponentInChildren<ItemStand>()?.m_attachOther ?? stand.transform;
            var model = trophy.transform.Find("attach")?.gameObject;
            if (model == null) return;
            var hung = Object.Instantiate(model, attach);
            hung.transform.localPosition = Vector3.zero;
            hung.transform.localRotation = Quaternion.identity;
            Strip(hung);
            made.Add(hung);
        }

        /// <summary>A part of the game made as a building ghost is: no saved data, nothing of the world.</summary>
        private static GameObject Ghost(List<GameObject> made, GameObject prefab, Vector3 at, Quaternion rotation)
        {
            ZNetView.m_forceDisableInit = true;
            GameObject go;
            try
            {
                go = Object.Instantiate(prefab, at, rotation);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
            }
            Strip(go);
            made.Add(go);
            return go;
        }

        /// <summary>The trophy's head, scaled to the given largest side.</summary>
        private static GameObject Head(List<GameObject> made, GameObject trophy, float size)
        {
            var model = trophy.transform.Find("attach")?.gameObject ?? trophy;
            var head = Object.Instantiate(model);
            Strip(head);
            made.Add(head);
            var b = BoundsOf(head);
            var largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            head.transform.localScale *= size / Mathf.Max(0.01f, largest);
            return head;
        }

        /// <summary>Sets a part down so its lowest drawn point rests at the given place.</summary>
        private static void Rest(GameObject part, Vector3 on)
        {
            part.transform.position = on;
            var b = BoundsOf(part);
            part.transform.position += new Vector3(on.x - b.center.x, on.y - b.min.y, on.z - b.center.z);
        }

        /// <summary>Takes away what would make a ghost act: colliders, bodies, and the parts that count it as a piece or a stand.</summary>
        private static void Strip(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(r);
        }

        private static float TopOf(GameObject part) => BoundsOf(part).max.y;

        private static Bounds BoundsOf(GameObject part)
        {
            var renderers = part.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToList();
            if (renderers.Count == 0) return new Bounds(part.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }

        private static Vector3 Ground(Vector3 p) =>
            ZoneSystem.instance.GetSolidHeight(p, out var ground) ? new Vector3(p.x, ground, p.z) : p;
    }
}
