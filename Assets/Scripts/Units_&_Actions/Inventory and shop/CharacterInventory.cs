using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterInventory : MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Inventory")]
    [SerializeField] private int maxItems = 5;
    [SerializeField] private List<OwnedItem> ownedItems = new();

    public IReadOnlyList<OwnedItem> OwnedItems => ownedItems;

    public event Action OnInventoryChanged;

    public int Count => ownedItems.Count;
    public bool IsFull => ownedItems.Count >= maxItems;

    public bool HasItem(string itemID)
    {
        return ownedItems.Exists(x => x.itemID == itemID);
    }

    public OwnedItem GetOwnedItem(string itemID)
    {
        return ownedItems.Find(x => x.itemID == itemID);
    }

    public ItemData GetItemData(OwnedItem ownedItem)
    {
        if (ownedItem == null || itemDatabase == null)
            return null;

        return itemDatabase.Get(ownedItem.itemID);
    }

    public bool AddItem(string itemID)
    {
        if (string.IsNullOrWhiteSpace(itemID))
            return false;

        if (itemDatabase == null)
        {
            Debug.LogError(
                $"[CharacterInventory] {name} has no ItemDatabase assigned!");

            return false;
        }

        if (IsFull)
        {
            Debug.LogWarning(
                $"[CharacterInventory] {name}'s inventory is full.");

            return false;
        }

        if (HasItem(itemID))
        {
            Debug.LogWarning(
                $"[CharacterInventory] {name} already owns {itemID}.");

            return false;
        }

        ItemData data = itemDatabase.Get(itemID);

        if (data == null)
        {
            Debug.LogError(
                $"[CharacterInventory] Could not find item '{itemID}'.");

            return false;
        }

        PlayerStats stats = GetComponent<PlayerStats>();

        if (stats != null &&
            data.playerClass != stats.playerClass)
        {
            Debug.LogWarning(
                $"[CharacterInventory] {name} cannot equip {data.displayName}.");

            return false;
        }

        ownedItems.Add(new OwnedItem(itemID));

        OnInventoryChanged?.Invoke();

        Debug.Log(
            $"[CharacterInventory] {name} acquired {data.displayName}. " +
            $"Inventory: {ownedItems.Count}/{maxItems}");

        return true;
    }
    public bool UpgradeItem(string itemID)
    {
        OwnedItem ownedItem = GetOwnedItem(itemID);

        if (ownedItem == null)
        {
            Debug.LogWarning(
                $"[CharacterInventory] Cannot upgrade {itemID}; not owned.");

            return false;
        }

        ownedItem.upgradeLevel++;

        OnInventoryChanged?.Invoke();

        Debug.Log(
            $"[CharacterInventory] {name} upgraded {itemID} " +
            $"to +{ownedItem.upgradeLevel}");

        return true;
    }

    public float GetCurrentEffect(OwnedItem ownedItem)
    {
        ItemData data = GetItemData(ownedItem);

        if (data == null)
            return 0f;

        switch (data.effectType)
        {
            // Becoming more negative means a larger cost reduction.
            case ItemEffectType.AttackStaminaCost:
                return data.baseEffectValue - ownedItem.upgradeLevel;

            default:
                return data.baseEffectValue + ownedItem.upgradeLevel;
        }
    }

    public float GetTotalEffect(ItemEffectType effectType)
    {
        float total = 0f;

        foreach (OwnedItem ownedItem in ownedItems)
        {
            ItemData data = GetItemData(ownedItem);

            if (data == null)
                continue;

            if (data.effectType != effectType)
                continue;

            total += GetCurrentEffect(ownedItem);
        }

        return total;
    }

    public int GetBonusDamage()
    {
        return Mathf.RoundToInt(
            GetTotalEffect(ItemEffectType.Damage));
    }

    public int GetBonusMaxHealth()
    {
        return Mathf.RoundToInt(
            GetTotalEffect(ItemEffectType.MaxHealth));
    }

    public int GetBonusMaxStamina()
    {
        return Mathf.RoundToInt(
            GetTotalEffect(ItemEffectType.MaxStamina));
    }

    public float GetDamageReductionPercent()
    {
        return GetTotalEffect(
            ItemEffectType.DamageReduction);
    }

    public int GetAttackStaminaCostModifier()
    {
        return Mathf.RoundToInt(
            GetTotalEffect(ItemEffectType.AttackStaminaCost));
    }
}