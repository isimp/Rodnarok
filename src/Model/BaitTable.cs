using System;
using System.Collections.Generic;
using System.Linq;

namespace Rodnarok
{
    /// <summary>One kind of fish as the game makes it: its prefab and the baits it takes.</summary>
    internal sealed class FishKind
    {
        public FishKind(string prefab, IReadOnlyList<string> baits)
        {
            Prefab = prefab;
            Baits = baits ?? Array.Empty<string>();
        }

        public string Prefab { get; }
        public IReadOnlyList<string> Baits { get; }
    }

    /// <summary>
    /// Which fish each bait brings, built from the baits the game's own fish take. A bait brings
    /// its fish in any water: where a fish lives in the world plays no part. A bait several fish
    /// take brings each of them in equal share.
    /// </summary>
    internal sealed class BaitTable
    {
        private static readonly IReadOnlyList<string> NoFish = Array.Empty<string>();

        private readonly Dictionary<string, List<string>> _fish = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>Fish in the order given; each bait's fish keep that order.</summary>
        public BaitTable(IEnumerable<FishKind> fish)
        {
            foreach (var kind in fish ?? Enumerable.Empty<FishKind>())
            {
                foreach (var bait in kind.Baits.Where(b => !string.IsNullOrEmpty(b)).Distinct(StringComparer.Ordinal))
                {
                    if (!_fish.TryGetValue(bait, out var list)) _fish[bait] = list = new List<string>();
                    list.Add(kind.Prefab);
                }
            }
            Baits = _fish.Keys.ToList();
        }

        /// <summary>Every bait some fish takes.</summary>
        public IReadOnlyList<string> Baits { get; }

        /// <summary>The fish a bait brings; none for what no fish takes.</summary>
        public IReadOnlyList<string> FishFor(string bait) =>
            bait != null && _fish.TryGetValue(bait, out var list) ? list : NoFish;

        /// <summary>The fish that comes to a bait for a roll from 0 to 1, each of its fish taking an equal share of the rolls; null for what no fish takes.</summary>
        public string Choose(string bait, double roll) => Choose(bait, roll, null);

        /// <summary>
        /// The fish that comes to a bait for a roll from 0 to 1 among those of its fish that fit, such
        /// as the water at a float; each fitting fish takes an equal share of the rolls; null for none.
        /// </summary>
        public string Choose(string bait, double roll, Func<string, bool> fits)
        {
            IReadOnlyList<string> fish = fits == null ? FishFor(bait) : FishFor(bait).Where(fits).ToList();
            if (fish.Count == 0) return null;
            var index = (int)Math.Floor(Math.Max(0.0, roll) * fish.Count);
            return fish[Math.Min(index, fish.Count - 1)];
        }
    }
}
