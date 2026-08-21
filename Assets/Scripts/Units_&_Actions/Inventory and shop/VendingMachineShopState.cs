using System.Collections.Generic;
using UnityEngine;

public class VendingMachineShopState : MonoBehaviour
{
    public static VendingMachineShopState Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Shop Settings")]
    [SerializeField] private int itemsPerCharacter = 3;

    private readonly List<ItemData> knightItems = new();
    private readonly List<ItemData> mageItems = new();

    public IReadOnlyList<ItemData> KnightItems => knightItems;
    public IReadOnlyList<ItemData> MageItems => mageItems;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        LevelGenerator.OnLevelReady += GenerateFloorInventory;
    }

    private void OnDisable()
    {
        LevelGenerator.OnLevelReady -= GenerateFloorInventory;
    }

    private void Start()
    {
        // Useful if this object came online after the first generation event.
        if (knightItems.Count == 0 && mageItems.Count == 0)
            GenerateFloorInventory();
    }

    public void GenerateFloorInventory()
    {
        knightItems.Clear();
        mageItems.Clear();

        if (itemDatabase == null)
        {
            Debug.LogError(
                "[VendingMachineShopState] ItemDatabase not assigned.");
            return;
        }

        RollItemsForClass(
            PlayerClass.Knight,
            knightItems);

        RollItemsForClass(
            PlayerClass.Mage,
            mageItems);

        Debug.Log(
            $"[VendingMachineShopState] New floor inventory generated. " +
            $"Knight={knightItems.Count}, Mage={mageItems.Count}");
    }

    public IReadOnlyList<ItemData> GetItemsForClass(
        PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.Knight:
                return knightItems;

            case PlayerClass.Mage:
                return mageItems;

            default:
                return System.Array.Empty<ItemData>();
        }
    }

    private void RollItemsForClass(
        PlayerClass playerClass,
        List<ItemData> destination)
    {
        List<ItemData> pool =
            itemDatabase.GetForClass(playerClass);

        if (pool == null || pool.Count == 0)
            return;

        // Work on a copy so we don't mutate the database.
        List<ItemData> available =
            new List<ItemData>(pool);

        int count =
            Mathf.Min(itemsPerCharacter, available.Count);

        for (int i = 0; i < count; i++)
        {
            int index =
                Random.Range(0, available.Count);

            destination.Add(
                available[index]);

            available.RemoveAt(index);
        }
    }
}