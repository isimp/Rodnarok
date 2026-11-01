using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Rodnarok
{
    /// <summary>
    /// A fishing rod and bait put in the character's hands for a scenario, and everything as it was
    /// put back afterwards: both hands, the items put away with the hide key, the selected bait,
    /// every stack of the inventory, every item worn or held, and the items and recipes the character knows. A rod the
    /// character carries is used, otherwise one is added; bait is added for the run and the
    /// character's own bait count set back after, so a cast never spends the character's bait.
    /// The game shows a character that newly learns an item a notice, which it shows once whatever
    /// comes after.
    /// </summary>
    internal sealed class SelfTestGear
    {
        private const string BaitPrefab = "FishingBait";

        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HiddenRight = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_hiddenRightItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HiddenLeft = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_hiddenLeftItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> Ammo = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_ammoItem");
        private static readonly AccessTools.FieldRef<Humanoid, VisEquipment> Looks = AccessTools.FieldRefAccess<Humanoid, VisEquipment>("m_visEquipment");
        private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownMaterial = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");
        private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownRecipes = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownRecipes");
        private static readonly System.Reflection.MethodInfo SetupLooks = AccessTools.Method(typeof(Humanoid), "SetupVisEquipment");

        private readonly Player _player;
        private readonly ItemDrop.ItemData _right;
        private readonly ItemDrop.ItemData _left;
        private readonly ItemDrop.ItemData _hiddenRight;
        private readonly ItemDrop.ItemData _hiddenLeft;
        private readonly ItemDrop.ItemData _ammo;
        private readonly List<(ItemDrop.ItemData Item, int Stack)> _items;
        private readonly int _bait;
        private readonly HashSet<string> _knownMaterial;
        private readonly HashSet<string> _knownRecipes;
        private readonly List<ItemDrop.ItemData> _equipped;
        private ItemDrop.ItemData _addedRod;
        private bool _putBack;

        private SelfTestGear(Player player)
        {
            _player = player;
            _right = player.RightItem;
            _left = player.LeftItem;
            _hiddenRight = HiddenRight(player);
            _hiddenLeft = HiddenLeft(player);
            _ammo = Ammo(player);
            _items = player.GetInventory().GetAllItems().Select(i => (i, i.m_stack)).ToList();
            _bait = BaitCount(player);
            _knownMaterial = new HashSet<string>(KnownMaterial(player));
            _knownRecipes = new HashSet<string>(KnownRecipes(player));
            _equipped = player.GetInventory().GetEquippedItems().ToList();
        }

        /// <summary>The rod now in hand, or null with what stood in the way.</summary>
        public ItemDrop.ItemData Rod { get; private set; }

        public string Trouble { get; private set; }

        /// <summary>Puts a fishing rod in the character's hands and bait on it, noting how things were.</summary>
        public static SelfTestGear RodInHand(Player player)
        {
            var gear = new SelfTestGear(player);
            var inventory = player.GetInventory();
            var rod = inventory.GetAllItems().Find(i => i.m_dropPrefab != null && i.m_dropPrefab.name == "FishingRod");
            if (rod == null)
            {
                if (!inventory.HaveEmptySlot())
                {
                    gear.Trouble = "no free inventory slot for a fishing rod";
                    return gear;
                }
                rod = inventory.AddItem("FishingRod", 1, 1, 0, 0L, "", false);
                gear._addedRod = rod;
                if (rod == null)
                {
                    gear.Trouble = "a fishing rod could not be added";
                    return gear;
                }
            }
            if (!player.IsItemEquiped(rod) && !player.EquipItem(rod, triggerEquipEffects: false))
            {
                gear.Trouble = "the fishing rod could not be equipped (attacking, dodging or swimming?)";
                return gear;
            }

            // Bait of the run's own, selected for the rod.
            var bait = inventory.AddItem(BaitPrefab, 10, 1, 0, 0L, "", false);
            if (bait == null)
            {
                gear.Trouble = "no room for Fishing Bait";
                return gear;
            }
            player.EquipItem(bait, triggerEquipEffects: false);
            gear.Rod = rod;
            return gear;
        }

        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> Utility = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_utilityItem");
        private ItemDrop.ItemData _utility;
        private ItemDrop.ItemData _addedWorn;
        private ItemDrop.ItemData _lentWorn;
        private bool _wore;

        /// <summary>Puts an item on in the utility slot, one of the character's own if it carries one, else one added for the run; null with what stood in the way.</summary>
        public ItemDrop.ItemData Wear(string prefab)
        {
            var inventory = _player.GetInventory();
            if (!_wore)
            {
                _wore = true;
                _utility = Utility(_player);
            }
            var item = inventory.GetAllItems().Find(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab);
            if (item == null)
            {
                if (!inventory.HaveEmptySlot())
                {
                    Trouble = "no free inventory slot for a " + prefab;
                    return null;
                }
                item = inventory.AddItem(prefab, 1, 1, 0, 0L, "", false);
                _addedWorn = item;
                if (item == null)
                {
                    Trouble = "a " + prefab + " could not be added";
                    return null;
                }
            }
            if (!_player.IsItemEquiped(item))
            {
                _lentWorn = item;
                _player.EquipItem(item, triggerEquipEffects: false);
            }
            return _player.IsItemEquiped(item) ? item : null;
        }

        /// <summary>Puts hands, put-away items, selected bait, inventory and knowledge back as they were; once.</summary>
        public void PutBack()
        {
            if (_putBack) return;
            _putBack = true;
            var inventory = _player.GetInventory();
            if (_wore)
            {
                // Taken off by itself, as a mod giving more utility slots may wear it in a slot of its own.
                if (_lentWorn != null && _player.IsItemEquiped(_lentWorn)) _player.UnequipItem(_lentWorn, triggerEquipEffects: false);
                var worn = Utility(_player);
                if (worn != null && worn != _utility) _player.UnequipItem(worn, triggerEquipEffects: false);
                if (_utility != null && inventory.ContainsItem(_utility) && !_player.IsItemEquiped(_utility)) _player.EquipItem(_utility, triggerEquipEffects: false);
                if (_addedWorn != null && inventory.ContainsItem(_addedWorn)) inventory.RemoveItem(_addedWorn);
            }
            if (Rod != null && _player.IsItemEquiped(Rod) && Rod != _right && Rod != _left) _player.UnequipItem(Rod, triggerEquipEffects: false);
            if (_right != null && inventory.ContainsItem(_right) && !_player.IsItemEquiped(_right)) _player.EquipItem(_right, triggerEquipEffects: false);
            if (_left != null && inventory.ContainsItem(_left) && !_player.IsItemEquiped(_left)) _player.EquipItem(_left, triggerEquipEffects: false);
            HiddenRight(_player) = _hiddenRight;
            HiddenLeft(_player) = _hiddenLeft;
            // The put-away items are drawn on the character's back again.
            SetupLooks?.Invoke(_player, new object[] { Looks(_player), false });
            if (_addedRod != null && inventory.ContainsItem(_addedRod)) inventory.RemoveItem(_addedRod);

            // The bait count as before: what the run added taken out, what a cast spent of the character's given back.
            var bait = BaitCount(_player);
            var name = BaitName();
            if (bait > _bait) inventory.RemoveItem(name, bait - _bait);
            else if (bait < _bait) inventory.AddItem(BaitPrefab, _bait - bait, 1, 0, 0L, "", false);
            var current = Ammo(_player);
            if (current != _ammo)
            {
                if (current != null) _player.UnequipItem(current, triggerEquipEffects: false);
                if (_ammo != null && inventory.ContainsItem(_ammo)) _player.EquipItem(_ammo, triggerEquipEffects: false);
            }

            KnownMaterial(_player).RemoveWhere(m => !_knownMaterial.Contains(m));
            KnownRecipes(_player).RemoveWhere(r => !_knownRecipes.Contains(r));
        }

        /// <summary>Whether hands, put-away items, selected bait, inventory and knowledge are as before.</summary>
        public bool AsBefore() => Difference().Length == 0;

        /// <summary>What differs from before, empty when nothing does.</summary>
        public string Difference()
        {
            var differs = new List<string>();
            if (_player.RightItem != _right) differs.Add("right hand");
            if (_player.LeftItem != _left) differs.Add("left hand");
            if (HiddenRight(_player) != _hiddenRight || HiddenLeft(_player) != _hiddenLeft) differs.Add("put-away items");
            if (Ammo(_player) != _ammo) differs.Add("selected bait or ammunition");
            if (_wore && Utility(_player) != _utility) differs.Add("utility item");
            var equippedNow = _player.GetInventory().GetEquippedItems();
            var came = equippedNow.Where(i => !_equipped.Contains(i)).Select(Named).ToList();
            var went = _equipped.Where(i => !equippedNow.Contains(i)).Select(Named).ToList();
            if (came.Count > 0 || went.Count > 0)
                differs.Add("worn or held (" + string.Join(", ", came.Select(n => n + " put on").Concat(went.Select(n => n + " taken off"))) + ")");
            var now = _player.GetInventory().GetAllItems();
            if (now.Count != _items.Count || _items.Any(e => !now.Contains(e.Item) || e.Item.m_stack != e.Stack))
            {
                // Named item by item, so a failure says what came, went or changed.
                var changes = new List<string>();
                foreach (var (item, stack) in _items)
                {
                    if (!now.Contains(item)) changes.Add(Named(item) + " gone");
                    else if (item.m_stack != stack) changes.Add(Named(item) + " " + stack + " -> " + item.m_stack);
                }
                foreach (var item in now)
                    if (!_items.Any(e => e.Item == item)) changes.Add(Named(item) + " x" + item.m_stack + " new");
                differs.Add("inventory (" + string.Join(", ", changes) + ")");
            }
            if (!KnownMaterial(_player).SetEquals(_knownMaterial)) differs.Add("known items");
            if (!KnownRecipes(_player).SetEquals(_knownRecipes)) differs.Add("known recipes");
            return string.Join(", ", differs);
        }

        private static string Named(ItemDrop.ItemData item) => item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;

        private static string BaitName() => ObjectDB.instance.GetItemPrefab(BaitPrefab)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name ?? "$item_fishingbait";

        private static int BaitCount(Player player) => player.GetInventory().CountItems(BaitName());
    }
}
