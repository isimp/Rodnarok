using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The serpent post: the game's darkwood pole with a Serpent trophy's head on top, built with
    /// the hammer for the pole's own cost and a Serpent trophy. A post standing in the world calms
    /// the water around it (<see cref="CalmWaters"/>). It is made once from the game's own
    /// prefabs, kept out of the world, and given to the game's prefab list and the hammer in every
    /// world.
    /// </summary>
    internal static class SerpentPost
    {
        public const string PrefabName = "Rodnarok_SerpentPost";
        private const string Pole = "darkwood_pole";
        private const string Trophy = "TrophySerpent";

        private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> NamedPrefabs = AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");

        private static GameObject _holder;
        private static GameObject _prefab;

        /// <summary>Makes the post once both the game's prefabs and its items are there, and gives it to the prefab list and the hammer.</summary>
        public static void Ensure()
        {
            var scene = ZNetScene.instance;
            var db = ObjectDB.instance;
            if (scene == null || db == null || db.m_items.Count == 0) return;
            if (_prefab == null) _prefab = Make(scene, db);
            if (_prefab == null) return;

            if (!scene.m_prefabs.Contains(_prefab))
            {
                scene.m_prefabs.Add(_prefab);
                NamedPrefabs(scene)[_prefab.name.GetStableHashCode()] = _prefab;
            }
            var pieces = db.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces?.m_pieces;
            if (pieces != null && !pieces.Contains(_prefab)) pieces.Add(_prefab);
        }

        private static GameObject Make(ZNetScene scene, ObjectDB db)
        {
            var pole = scene.GetPrefab(Pole);
            var trophy = db.GetItemPrefab(Trophy)?.GetComponent<ItemDrop>();
            if (pole == null || trophy == null)
            {
                Log.Warning("The serpent post could not be made: " + (pole == null ? Pole : Trophy) + " is not in the game.");
                return null;
            }

            // Made under an inactive holder, so nothing of the copy wakes as a world object.
            _holder = new GameObject("Rodnarok prefabs");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
            var post = Object.Instantiate(pole, _holder.transform);
            post.name = PrefabName;

            var piece = post.GetComponent<Piece>();
            piece.m_name = "Serpent post";
            piece.m_description = "A serpent's head on a darkwood post. The waves around it calm.";
            piece.m_icon = trophy.m_itemData.GetIcon();
            piece.m_resources = piece.m_resources
                .Select(r => new Piece.Requirement { m_resItem = r.m_resItem, m_amount = r.m_amount, m_extraAmountOnlyOneIngredient = r.m_extraAmountOnlyOneIngredient, m_amountPerLevel = r.m_amountPerLevel, m_recover = r.m_recover })
                .Append(new Piece.Requirement { m_resItem = trophy, m_amount = 1, m_recover = true })
                .ToArray();

            var head = HeadOf(trophy.gameObject);
            if (head != null)
            {
                var top = Object.Instantiate(head, post.transform);
                top.name = "SerpentHead";
                foreach (var collider in top.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var body in top.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                top.transform.localRotation = Quaternion.identity;
                top.transform.localPosition = Vector3.zero;
                top.transform.localScale = Vector3.one;
                var (low, high) = Extent(top.transform);
                var scale = HeadSize / Mathf.Max(0.01f, Mathf.Max(high.x - low.x, Mathf.Max(high.y - low.y, high.z - low.z)));
                top.transform.localScale = Vector3.one * scale;
                // The head's lowest point rests on the pole's top.
                top.transform.localPosition = new Vector3(0f, TopOf(post, top.transform) - low.y * scale, 0f);
            }

            post.AddComponent<CalmPost>();
            return post;
        }

        /// <summary>How large the head is drawn on the post, its largest side in metres.</summary>
        private const float HeadSize = 0.7f;

        /// <summary>The lowest and highest corner of what a part draws, in its own space.</summary>
        private static (Vector3 Low, Vector3 High) Extent(Transform part)
        {
            var low = Vector3.positiveInfinity;
            var high = Vector3.negativeInfinity;
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = part.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    low = Vector3.Min(low, p);
                    high = Vector3.Max(high, p);
                }
            }
            return float.IsInfinity(low.x) ? (Vector3.zero, Vector3.zero) : (low, high);
        }

        /// <summary>The trophy's look: the model the game hangs on an item stand, else the first one drawn.</summary>
        private static GameObject HeadOf(GameObject trophy)
        {
            var attach = trophy.transform.Find("attach");
            if (attach != null) return attach.gameObject;
            return trophy.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.gameObject != trophy)?.gameObject;
        }

        /// <summary>The height of the post's highest drawn point above its foot, leaving out the head.</summary>
        private static float TopOf(GameObject post, Transform head)
        {
            var top = 0f;
            foreach (var filter in post.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.transform.IsChildOf(head)) continue;
                var max = filter.sharedMesh.bounds.max;
                top = Mathf.Max(top, post.transform.InverseTransformPoint(filter.transform.TransformPoint(max)).y);
            }
            return top;
        }
    }

    /// <summary>A built serpent post: while it stands, it is one of the posts calming the water. A building ghost, which has no saved data, is none.</summary>
    internal sealed class CalmPost : MonoBehaviour
    {
        private bool _counted;

        private void Start()
        {
            var view = GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null) return;
            _counted = true;
            CalmWaters.Add(this);
        }

        private void OnDestroy()
        {
            if (_counted) CalmWaters.Remove(this);
        }
    }
}
