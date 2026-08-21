using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VendingMachineUI : MonoBehaviour
{
    public static VendingMachineUI Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject vendingRoot;

    [Header("UI To Hide While Shopping")]
    [SerializeField] private List<GameObject> hideWhileOpen = new();

    [Header("Header")]
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Button closeButton;

    [Header("Store Cards")]
    [SerializeField] private ShopItemCardUI[] itemCards;

    [Header("Upgrades")]
    [SerializeField] private Transform upgradeContent;
    [SerializeField] private UpgradeEntryUI upgradeEntryPrefab;
    [SerializeField] private TMP_Text upgradeCostText;
    [SerializeField] private Button upgradeButton;

    [Header("Healing")]
    [SerializeField] private Button healOneButton;
    [SerializeField] private Button healAllButton;
    [SerializeField] private TMP_Text healOneText;
    [SerializeField] private TMP_Text healAllText;

    private readonly Dictionary<GameObject, bool> previousStates = new();
    private readonly List<UpgradeEntryUI> spawnedUpgradeEntries = new();

    private UpgradeEntryUI selectedUpgradeEntry;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (vendingRoot != null)
            vendingRoot.SetActive(false);
    }

    private void Start()
    {
        SubscribeToPartyManager();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(UpgradeSelectedItem);
            upgradeButton.interactable = false;
        }

        if (healOneButton != null)
            healOneButton.onClick.AddListener(HealOne);

        if (healAllButton != null)
            healAllButton.onClick.AddListener(HealAll);

        // Listen for clicks on all three store cards.
        foreach (ShopItemCardUI card in itemCards)
        {
            if (card != null)
                card.OnClicked += OnShopItemClicked;
        }

    }

    private void OnEnable()
    {
        SubscribeToPartyManager();
    }

    private void OnDisable()
    {
        if (PartyManager.IsValid)
            PartyManager.Instance.OnSelectedUnitChanged -= OnSelectedUnitChanged;
    }

    private void SubscribeToPartyManager()
    {
        if (!PartyManager.IsValid)
            return;

        PartyManager.Instance.OnSelectedUnitChanged -= OnSelectedUnitChanged;
        PartyManager.Instance.OnSelectedUnitChanged += OnSelectedUnitChanged;
    }

    // ─────────────────────────────────────────────────────────────
    // Open / Close
    // ─────────────────────────────────────────────────────────────

    public void Open()
    {
        if (IsOpen)
            return;

        IsOpen = true;

        previousStates.Clear();

        foreach (GameObject uiObject in hideWhileOpen)
        {
            if (uiObject == null)
                continue;

            previousStates[uiObject] = uiObject.activeSelf;
            uiObject.SetActive(false);
        }

        if (vendingRoot != null)
            vendingRoot.SetActive(true);

        RefreshForSelectedCharacter();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        if (vendingRoot != null)
            vendingRoot.SetActive(false);

        foreach (var pair in previousStates)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }

        previousStates.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    // Character Switching
    // ─────────────────────────────────────────────────────────────

    private void OnSelectedUnitChanged(Unit unit)
    {
        if (!IsOpen)
            return;

        RefreshForSelectedCharacter();
    }

    private void RefreshForSelectedCharacter()
    {
        if (!PartyManager.IsValid)
            return;

        Unit selected = PartyManager.Instance.SelectedUnit;

        if (selected == null)
            return;

        if (characterNameText != null)
        {
            characterNameText.text =
                selected.name.Replace("(Clone)", "").ToUpper();
        }

        RefreshStoreCards(selected);
        RefreshUpgradeSection(selected);
        RefreshHealingSection(selected);
    }

    // ─────────────────────────────────────────────────────────────
    // Store
    // ─────────────────────────────────────────────────────────────

    private void RefreshStoreCards(Unit selected)
    {
        if (selected == null)
            return;

        PlayerStats stats =
            selected.GetComponent<PlayerStats>();

        if (stats == null)
            return;

        if (VendingMachineShopState.Instance == null)
        {
            Debug.LogWarning(
                "[VendingMachineUI] No VendingMachineShopState found.");

            return;
        }

        IReadOnlyList<ItemData> shopItems =
            VendingMachineShopState.Instance
                .GetItemsForClass(stats.playerClass);

        for (int i = 0; i < itemCards.Length; i++)
        {
            if (itemCards[i] == null)
                continue;

            if (i < shopItems.Count)
            {
                itemCards[i].SetItem(shopItems[i]);
            }
            else
            {
                itemCards[i].Clear();
            }
        }
    }
    

    private void OnShopItemClicked(ItemData item)
    {
        if (item == null)
            return;

        Unit selected = PartyManager.Instance?.SelectedUnit;

        if (selected == null)
            return;

        CharacterInventory inventory =
            selected.GetComponent<CharacterInventory>();

        if (inventory == null)
            return;

        // Currency purchase logic will go here.
        Debug.Log(
            $"[VendingMachineUI] Selected shop item: {item.displayName}");
    }

    // ─────────────────────────────────────────────────────────────
    // Upgrades
    // ─────────────────────────────────────────────────────────────

    private void RefreshUpgradeSection(Unit selected)
    {
        ClearUpgradeEntries();

        selectedUpgradeEntry = null;

        CharacterInventory inventory =
            selected.GetComponent<CharacterInventory>();

        if (inventory == null ||
            inventory.OwnedItems.Count == 0)
        {
            if (upgradeCostText != null)
                upgradeCostText.text = "NO ITEMS";

            if (upgradeButton != null)
                upgradeButton.interactable = false;

            return;
        }

        foreach (OwnedItem ownedItem in inventory.OwnedItems)
        {
            ItemData data =
                inventory.GetItemData(ownedItem);

            if (data == null)
                continue;

            UpgradeEntryUI entry =
                Instantiate(
                    upgradeEntryPrefab,
                    upgradeContent);

            float currentEffect =
                inventory.GetCurrentEffect(ownedItem);

            entry.Setup(
                ownedItem,
                data,
                currentEffect);

            entry.OnClicked += SelectUpgradeEntry;

            spawnedUpgradeEntries.Add(entry);
        }

        if (upgradeCostText != null)
            upgradeCostText.text = "SELECT ITEM";

        if (upgradeButton != null)
            upgradeButton.interactable = false;
    }

    private void SelectUpgradeEntry(UpgradeEntryUI entry)
    {
        selectedUpgradeEntry = entry;

        if (entry == null)
            return;

        int upgradeCost = GetUpgradeCost(entry.OwnedItem);

        if (upgradeCostText != null)
            upgradeCostText.text = $"{upgradeCost} GOLD";

        if (upgradeButton != null)
            upgradeButton.interactable = true;
    }

    private void UpgradeSelectedItem()
    {
        if (selectedUpgradeEntry == null)
            return;

        Unit selected = PartyManager.Instance?.SelectedUnit;

        if (selected == null)
            return;

        CharacterInventory inventory =
            selected.GetComponent<CharacterInventory>();

        if (inventory == null)
            return;

        OwnedItem ownedItem =
            selectedUpgradeEntry.OwnedItem;

        int cost = GetUpgradeCost(ownedItem);

        // Currency check/subtraction will go here.

        if (!inventory.UpgradeItem(ownedItem.itemID))
            return;

        RefreshUpgradeSection(selected);
    }

    private int GetUpgradeCost(OwnedItem item)
    {
        if (item == null)
            return 0;

        // Temporary formula.
        // Base 5, +2 for every previous upgrade.
        return 5 + (item.upgradeLevel * 2);
    }

    private void ClearUpgradeEntries()
    {
        foreach (UpgradeEntryUI entry in spawnedUpgradeEntries)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }

        spawnedUpgradeEntries.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    // Healing
    // ─────────────────────────────────────────────────────────────

    private void RefreshHealingSection(Unit selected)
    {
        PlayerStats stats = selected.GetComponent<PlayerStats>();

        if (stats == null)
            return;

        int missingHealth =
            Mathf.Max(
                0,
                stats.maxHealth - stats.currentHealth);

        int healAllCost = missingHealth * 2;

        if (healOneText != null)
        {
            healOneText.text =
                missingHealth > 0
                    ? "HEAL 1 POINT\n2 GOLD"
                    : "FULL HEALTH";
        }

        if (healAllText != null)
        {
            healAllText.text =
                missingHealth > 0
                    ? $"HEAL TO FULL\n{healAllCost} GOLD"
                    : "FULL HEALTH";
        }

        if (healOneButton != null)
            healOneButton.interactable = missingHealth > 0;

        if (healAllButton != null)
            healAllButton.interactable = missingHealth > 0;
    }

    private void HealOne()
    {
        Unit selected = PartyManager.Instance?.SelectedUnit;

        if (selected == null)
            return;

        // Currency + HealthComponent implementation comes next.
        Debug.Log("[VendingMachineUI] Heal 1 requested.");
    }

    private void HealAll()
    {
        Unit selected = PartyManager.Instance?.SelectedUnit;

        if (selected == null)
            return;

        // Currency + HealthComponent implementation comes next.
        Debug.Log("[VendingMachineUI] Heal All requested.");
    }
}