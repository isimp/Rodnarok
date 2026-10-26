using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Rodnarok
{
    /// <summary>
    /// A fishing rod and bait put in the character's hands for a scenario, and everything as it was
    /// put back afterwards: both hands, the items put away with the hide key, the selected bait,
    /// every stack of the inventory, and the items and recipes the character knows. A rod the
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

        /// <summary>Puts hands, put-away items, selected bait, inventory and knowledge back as they were; once.</summary>
        public void PutBack()
        {
            if (_putBack) return;
            _putBack = true;
            var inventory = _player.GetInventory();
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
            var now = _player.GetInventory().GetAllItems();
            if (now.Count != _items.Count || _items.Any(e => !now.Contains(e.Item) || e.Item.m_stack != e.Stack)) differs.Add("inventory");
            if (!KnownMaterial(_player).SetEquals(_knownMaterial)) differs.Add("known items");
            if (!KnownRecipes(_player).SetEquals(_knownRecipes)) differs.Add("known recipes");
            return string.Join(", ", differs);
        }

        private static string BaitName() => ObjectDB.instance.GetItemPrefab(BaitPrefab)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name ?? "$item_fishingbait";

        private static int BaitCount(Player player) => player.GetInventory().CountItems(BaitName());
    }
}
