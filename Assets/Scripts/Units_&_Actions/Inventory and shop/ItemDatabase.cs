using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Header("CSV Source")]
    public TextAsset sourceCSV;

    [Header("Imported Items")]
    public List<ItemData> items = new();

    public ItemData Get(string id)
    {
        foreach (var item in items)
        {
            if (item.id == id)
                return item;
        }

        Debug.LogError($"[ItemDatabase] No item with ID {id}");
        return null;
    }

    public List<ItemData> GetForClass(PlayerClass playerClass)
    {
        List<ItemData> results = new();

        foreach (var item in items)
        {
            if (item.playerClass == playerClass)
                results.Add(item);
        }

        return results;
    }
}