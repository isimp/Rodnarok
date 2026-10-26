using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// What the running game says about its fish: which bait brings which fish (from the baits each
    /// fish prefab takes), and how the world spawns each (its group size, levels and level-up
    /// chance, from the spawn lists every zone uses). Read once per world, so fish another mod
    /// adds come in too.
    /// </summary>
    internal sealed class FishBook
    {
        private static FishBook _current;
        private static ZNetScene _readFor;

        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, SpawnSystem.SpawnData> _spawns = new Dictionary<string, SpawnSystem.SpawnData>(System.StringComparer.Ordinal);

        private FishBook()
        {
            var fish = ZNetScene.instance.m_prefabs.Where(p => p != null && p.GetComponent<Fish>() != null).ToList();
            foreach (var prefab in fish) _prefabs[prefab.name] = prefab;
            Baits = new BaitTable(fish.Select(p => new FishKind(p.name,
                p.GetComponent<Fish>().m_baits.Where(b => b?.m_bait != null).Select(b => b.m_bait.name).ToList())));

            var spawner = ZoneSystem.instance?.m_zoneCtrlPrefab?.GetComponent<SpawnSystem>();
            if (spawner == null) return;
            foreach (var entry in spawner.m_spawnLists.Where(l => l != null).SelectMany(l => l.m_spawners))
            {
                if (entry?.m_prefab == null || !entry.m_enabled || entry.m_prefab.GetComponent<Fish>() == null) continue;
                if (!_spawns.ContainsKey(entry.m_prefab.name)) _spawns[entry.m_prefab.name] = entry;
            }
        }

        /// <summary>The book of the world being played; null outside a world.</summary>
        public static FishBook Current
        {
            get
            {
                if (ZNetScene.instance == null || ZoneSystem.instance == null) return null;
                if (_readFor != ZNetScene.instance)
                {
                    _current = new FishBook();
                    _readFor = ZNetScene.instance;
                }
                return _current;
            }
        }

        public BaitTable Baits { get; }

        public GameObject Prefab(string name) => name != null && _prefabs.TryGetValue(name, out var prefab) ? prefab : null;

        /// <summary>How many of a fish the world spawns together; unknown for one it never spawns in groups.</summary>
        public GroupSize Group(string name) =>
            name != null && _spawns.TryGetValue(name, out var entry) ? new GroupSize(entry.m_groupSizeMin, entry.m_groupSizeMax) : GroupSize.Unknown;

        /// <summary>A fish's quality for a school beside this place, rolled as the world's spawn of it would roll it.</summary>
        public int RollQuality(string name, Vector3 place)
        {
            if (name != null && _spawns.TryGetValue(name, out var entry))
                return Stars.Roll(entry.m_minLevel, entry.m_maxLevel, SpawnSystem.GetLevelUpChance(place, entry), () => Random.Range(0f, 100f));
            return Stars.Roll(1, 5, SpawnSystem.GetLevelUpChance(place), () => Random.Range(0f, 100f));
        }
    }
}
