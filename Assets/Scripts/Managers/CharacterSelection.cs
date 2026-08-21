using System.Collections.Generic;
using UnityEngine;

public static class CharacterSelection
{
    // Legacy: single character selection
    public static int        Index  { get; set; } = 0;
    public static GameObject Prefab { get; set; } = null;

    private static List<PartySlotInfo> _slots = new();
    public static IReadOnlyList<PartySlotInfo> Slots => _slots;

    public static void SetCharacterPrefabs(List<GameObject> prefabs)
    {
        _characterPrefabs = prefabs;
    }

    public static void SetSlots(IReadOnlyList<int> characterIndices)
    {
        _slots.Clear();
        for (int i = 0; i < characterIndices.Count; i++)
        {
            _slots.Add(new PartySlotInfo(characterIndices[i], i));
        }
    }

    public static void ClearSlots()
    {
        _slots.Clear();
        Index = 0;
        Prefab = null;
    }

    public static GameObject GetPrefabForSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Count && _characterPrefabs != null)
            return _characterPrefabs[_slots[slotIndex].CharacterIndex];
        return null;
    }

    /// <summary>Get the character index for a given slot index.</summary>
    public static int GetCharacterForSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Count)
            return _slots[slotIndex].CharacterIndex;
        return -1;
    }

    /// <summary>Number of active party slots.</summary>
    public static int SlotCount => _slots.Count;

    /// <summary>Maximum allowed party size.</summary>
    public const int MaxSlots = 4;

    /// <summary>Data for one party slot (character index + player position).</summary>
    public struct PartySlotInfo
    {
        public int CharacterIndex; // which character class to use
        public int PlayerNumber;   // 0-based player position (determines ring color)

        public PartySlotInfo(int charIdx, int playerNum)
        {
            CharacterIndex = charIdx;
            PlayerNumber = playerNum;
        }
    }

    private static List<GameObject> _characterPrefabs = new();
}
