using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Rodnarok
{
    /// <summary>
    /// A fishing rod put in the character's hands for a scenario, and everything as it was put back
    /// afterwards: both hands, the items put away with the hide key, the inventory and what the
    /// character knows. A rod the character carries is used; otherwise one is added, taken out
    /// again at the end, and forgotten again by a character that did not know it. The game shows
    /// a character that newly learns an item a notice, which it shows once whatever comes after.
    /// </summary>
    internal sealed class SelfTestGear
    {
        private const string RodName = "$item_fishingrod";

        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HiddenRight = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_hiddenRightItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HiddenLeft = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_hiddenLeftItem");
        private static readonly AccessTools.FieldRef<Humanoid, VisEquipment> Looks = AccessTools.FieldRefAccess<Humanoid, VisEquipment>("m_visEquipment");
        private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownMaterial = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");
        private static readonly System.Reflection.MethodInfo SetupLooks = AccessTools.Method(typeof(Humanoid), "SetupVisEquipment");

        private readonly Player _player;
        private readonly ItemDrop.ItemData _right;
        private readonly ItemDrop.ItemData _left;
        private readonly ItemDrop.ItemData _hiddenRight;
        private readonly ItemDrop.ItemData _hiddenLeft;
        private readonly bool _knewRod;
        private readonly List<ItemDrop.ItemData> _items;
        private ItemDrop.ItemData _added;
        private bool _putBack;

        private SelfTestGear(Player player)
        {
            _player = player;
            _right = player.RightItem;
            _left = player.LeftItem;
            _hiddenRight = HiddenRight(player);
            _hiddenLeft = HiddenLeft(player);
            _knewRod = player.IsKnownMaterial(RodName);
            _items = player.GetInventory().GetAllItems().ToList();
        }

        /// <summary>The rod now in hand, or null with what stood in the way.</summary>
        public ItemDrop.ItemData Rod { get; private set; }

        public string Trouble { get; private set; }

        /// <summary>Puts a fishing rod in the character's hands, noting how things were.</summary>
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
                gear._added = rod;
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
            gear.Rod = rod;
            return gear;
        }

        /// <summary>Puts both hands, the put-away items, the inventory and what the character knows back as they were; once.</summary>
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
            if (_added != null && inventory.ContainsItem(_added)) inventory.RemoveItem(_added);
            if (!_knewRod) KnownMaterial(_player).Remove(RodName);
        }

        /// <summary>Whether hands, put-away items, inventory and knowledge are as before.</summary>
        public bool AsBefore() => Difference().Length == 0;

        /// <summary>What differs from before, empty when nothing does.</summary>
        public string Difference()
        {
            var differs = new List<string>();
            if (_player.RightItem != _right) differs.Add("right hand");
            if (_player.LeftItem != _left) differs.Add("left hand");
            if (HiddenRight(_player) != _hiddenRight || HiddenLeft(_player) != _hiddenLeft) differs.Add("put-away items");
            var now = _player.GetInventory().GetAllItems();
            if (now.Count != _items.Count || now.Any(i => !_items.Contains(i))) differs.Add("inventory");
            if (_player.IsKnownMaterial(RodName) != _knewRod) differs.Add("knows the fishing rod");
            return string.Join(", ", differs);
        }
    }
}
