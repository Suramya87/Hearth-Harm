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
    [SerializeField] private UpgradeEntryUI[] upgradeEntries;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeButtonText; 
    [SerializeField] private ScrollRect upgradeScrollRect;

    [Header("Healing")]
    [SerializeField] private Button healOneButton;
    [SerializeField] private Button healAllButton;
    [SerializeField] private TMP_Text healOneText;
    [SerializeField] private TMP_Text healAllText;

    private readonly Dictionary<GameObject, bool> previousStates = new();

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
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCoinsChanged += OnCoinsChanged;

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

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCoinsChanged -= OnCoinsChanged;
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

        if (VendingMachineShopState.Instance != null &&
    !VendingMachineShopState.Instance.HasGeneratedInventory)
        {
            VendingMachineShopState.Instance.GenerateFloorInventory();
        }

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

    private void OnCoinsChanged(int newAmount)
    {
        if (!IsOpen)
            return;

        RefreshForSelectedCharacter();
    }

    private void RefreshStoreCards(Unit selected)
    {
        if (selected == null)
            return;

        PlayerStats stats =
            selected.GetComponent<PlayerStats>();

        CharacterInventory inventory =
            selected.GetComponent<CharacterInventory>();

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
            ShopItemCardUI card = itemCards[i];

            if (card == null)
                continue;

            if (i >= shopItems.Count)
            {
                card.Clear();
                continue;
            }

            ItemData item =
                shopItems[i];

            card.SetItem(item);

            bool alreadyOwned =
                inventory != null &&
                inventory.HasItem(item.id);

            if (alreadyOwned)
            {
                card.SetInteractable(false);
                card.SetStatusText("OWNED");
                continue;
            }

            bool canAfford =
                CurrencyManager.Instance != null &&
                CurrencyManager.Instance.CurrentCoins >= item.baseCost;

            card.SetInteractable(canAfford);

            if (!canAfford)
                card.SetStatusText($"{item.baseCost} GOLD");
        }
    }


    private void OnShopItemClicked(ItemData item)
    {
        if (item == null)
            return;

        if (!PartyManager.IsValid)
            return;

        Unit selected =
            PartyManager.Instance.SelectedUnit;

        if (selected == null)
            return;

        CharacterInventory inventory =
            selected.GetComponent<CharacterInventory>();

        if (inventory == null)
        {
            Debug.LogWarning(
                "[VendingMachineUI] Selected character has no CharacterInventory.");

            return;
        }

        // Already owns this upgrade.
        if (inventory.HasItem(item.id))
        {
            Debug.Log(
                $"[VendingMachineUI] {selected.name} already owns {item.displayName}.");

            return;
        }

        // Inventory full.
        if (inventory.IsFull)
        {
            Debug.Log(
                $"[VendingMachineUI] {selected.name}'s inventory is full.");

            return;
        }

        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning(
                "[VendingMachineUI] CurrencyManager missing.");

            return;
        }

        // Try to pay first.
        if (!CurrencyManager.Instance.SpendCoins(item.baseCost))
        {
            Debug.Log(
                $"[VendingMachineUI] Not enough gold for {item.displayName}.");

            return;
        }

        // Give the item.
        bool added =
            inventory.AddItem(item.id);

        if (!added)
        {
            // This theoretically shouldn't happen after our checks.
            // Refund the player so money is never lost.
            CurrencyManager.Instance.AddCoins(item.baseCost);

            Debug.LogWarning(
                $"[VendingMachineUI] Purchase failed for {item.displayName}. Refunded.");

            return;
        }

        Debug.Log(
            $"[VendingMachineUI] Purchased {item.displayName} for {item.baseCost} gold.");

        // Refresh both sections.
        RefreshStoreCards(selected);
        RefreshUpgradeSection(selected);
    }

    // ─────────────────────────────────────────────────────────────
    // Upgrades
    // ─────────────────────────────────────────────────────────────


    private void RefreshUpgradeSection(Unit selected)
    {
        selectedUpgradeEntry = null;

        CharacterInventory inventory =
            selected.GetComponent<CharacterInventory>();

        Debug.Log(
            $"[VendingMachineUI] Refresh upgrades for {selected.name}. " +
            $"Inventory={(inventory != null ? inventory.OwnedItems.Count : -1)}, " +
            $"UI Entries={(upgradeEntries != null ? upgradeEntries.Length : -1)}");

        foreach (UpgradeEntryUI entry in upgradeEntries)
        {
            if (entry == null)
            {
                Debug.LogWarning(
                    "[VendingMachineUI] Null UpgradeEntryUI in array!");
                continue;
            }

            entry.gameObject.SetActive(false);
        }

        if (inventory == null ||
            inventory.OwnedItems.Count == 0)
        {
            Debug.Log(
                "[VendingMachineUI] No owned items to display.");

            if (upgradeButton != null)
                upgradeButton.interactable = false;

            return;
        }

        int count = Mathf.Min(
            inventory.OwnedItems.Count,
            upgradeEntries.Length);

        Debug.Log(
            $"[VendingMachineUI] Attempting to display {count} upgrade rows.");

        for (int i = 0; i < count; i++)
        {
            OwnedItem ownedItem =
                inventory.OwnedItems[i];

            Debug.Log(
                $"[VendingMachineUI] Upgrade row {i}: " +
                $"itemID={ownedItem.itemID}, level={ownedItem.upgradeLevel}");

            ItemData data =
                inventory.GetItemData(ownedItem);

            if (data == null)
            {
                Debug.LogError(
                    $"[VendingMachineUI] Could not resolve ItemData for " +
                    $"{ownedItem.itemID}");

                continue;
            }

            UpgradeEntryUI entry =
                upgradeEntries[i];

            if (entry == null)
            {
                Debug.LogError(
                    $"[VendingMachineUI] Upgrade Entries element {i} is NULL.");

                continue;
            }

            float currentEffect =
                inventory.GetCurrentEffect(ownedItem);

            entry.gameObject.SetActive(true);

            entry.Setup(
                ownedItem,
                data,
                currentEffect);

            entry.OnClicked -= SelectUpgradeEntry;
            entry.OnClicked += SelectUpgradeEntry;

            Debug.Log(
                $"[VendingMachineUI] Showing row {i}: " +
                $"{data.displayName}, effect={currentEffect}");
        }

        if (upgradeButton != null)
            upgradeButton.interactable = false;

        Canvas.ForceUpdateCanvases();

        if (upgradeEntries.Length > 0 &&
            upgradeEntries[0] != null)
        {
            RectTransform content =
                upgradeEntries[0].transform.parent as RectTransform;

            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);

                // Ensure the content itself begins at the top.
                content.anchoredPosition =
                    new Vector2(content.anchoredPosition.x, 0f);
            }
        }

        if (upgradeScrollRect != null)
        {
            // 1 = top, 0 = bottom.
            upgradeScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void SelectUpgradeEntry(UpgradeEntryUI entry)
    {
        selectedUpgradeEntry = entry;

        if (entry == null)
            return;

        OwnedItem ownedItem = entry.OwnedItem;
        ItemData data = entry.ItemData;

        if (ownedItem == null || data == null)
            return;

        Unit selected =
            PartyManager.Instance?.SelectedUnit;

        if (selected == null)
            return;

        CharacterInventory inventory =
            selected.GetComponent<CharacterInventory>();

        if (inventory == null)
            return;

        float currentEffect =
            inventory.GetCurrentEffect(ownedItem);

        float nextEffect;

        if (data.effectType ==
            ItemEffectType.AttackStaminaCost)
        {
            nextEffect = currentEffect - 1;
        }
        else
        {
            nextEffect = currentEffect + 1;
        }

        int upgradeCost =
            GetUpgradeCost(ownedItem);

        if (upgradeButtonText != null)
        {
            upgradeButtonText.text =
                $"UPGRADE — {upgradeCost} GOLD\n" +
                $"{FormatEffect(data.effectType, currentEffect)} " +
                $"→ {FormatEffect(data.effectType, nextEffect)}";
        }

        if (upgradeButton != null)
        {
            bool canAfford =
                CurrencyManager.Instance != null &&
                CurrencyManager.Instance.CurrentCoins >= upgradeCost;

            upgradeButton.interactable = canAfford;
        }
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

        int cost =
        GetUpgradeCost(ownedItem);

        if (CurrencyManager.Instance == null)
            return;

        if (!CurrencyManager.Instance.SpendCoins(cost))
        {
            Debug.Log(
                $"[VendingMachineUI] Not enough gold to upgrade {ownedItem.itemID}.");

            return;
        }

        if (!inventory.UpgradeItem(ownedItem.itemID))
        {
            CurrencyManager.Instance.AddCoins(cost);
            return;
        }

        RefreshUpgradeSection(selected);

        // Reselect the same logical item after rebuilding the rows.
        for (int i = 0; i < upgradeEntries.Length; i++)
        {
            UpgradeEntryUI entry = upgradeEntries[i];

            if (entry == null ||
                entry.OwnedItem == null)
                continue;

            if (entry.OwnedItem.itemID ==
                ownedItem.itemID)
            {
                SelectUpgradeEntry(entry);
                break;
            }
        }

    }

    private int GetUpgradeCost(OwnedItem item)
    {
        if (item == null)
            return 0;

        // Temporary formula.
        // Base 5, +2 for every previous upgrade.
        return 5 + (item.upgradeLevel * 2);
    }

    
    // ─────────────────────────────────────────────────────────────
    // Healing
    // ─────────────────────────────────────────────────────────────

    private void RefreshHealingSection(Unit selected)
    {
        if (selected == null)
            return;

        PlayerStats stats =
            selected.GetComponent<PlayerStats>();

        if (stats == null)
            return;

        int missingHealth =
            Mathf.Max(
                0,
                stats.maxHealth - stats.currentHealth);

        int healOneCost = 2;
        int healAllCost = missingHealth * 2;

        int coins =
            CurrencyManager.Instance != null
                ? CurrencyManager.Instance.CurrentCoins
                : 0;

        bool damaged =
            missingHealth > 0;

        bool canHealOne =
            damaged &&
            coins >= healOneCost;

        bool canHealAll =
            damaged &&
            coins >= healAllCost;

        if (healOneText != null)
        {
            healOneText.text =
                damaged
                    ? "HEAL 1 POINT\n2 GOLD"
                    : "FULL HEALTH";
        }

        if (healAllText != null)
        {
            healAllText.text =
                damaged
                    ? $"HEAL TO FULL\n{healAllCost} GOLD"
                    : "FULL HEALTH";
        }

        if (healOneButton != null)
            healOneButton.interactable = canHealOne;

        if (healAllButton != null)
            healAllButton.interactable = canHealAll;
    }

    private void HealOne()
    {
        if (!PartyManager.IsValid)
            return;

        Unit selected =
            PartyManager.Instance.SelectedUnit;

        if (selected == null)
            return;

        PlayerStats stats =
            selected.GetComponent<PlayerStats>();

        HealthComponent health =
            selected.GetComponent<HealthComponent>();

        if (stats == null || health == null)
            return;

        // Already full.
        if (stats.currentHealth >= stats.maxHealth)
            return;

        const int healCost = 2;

        if (CurrencyManager.Instance == null)
            return;

        if (!CurrencyManager.Instance.SpendCoins(healCost))
        {
            Debug.Log(
                "[VendingMachineUI] Not enough gold to heal.");

            return;
        }

        int newHealth =
            Mathf.Min(
                stats.currentHealth + 1,
                stats.maxHealth);

        health.SetHealth(newHealth);

        RefreshHealingSection(selected);
        RefreshStoreCards(selected);

        Debug.Log(
            $"[VendingMachineUI] Healed {selected.name} for 1 HP.");
    }

    private void HealAll()
    {
        if (!PartyManager.IsValid)
            return;

        Unit selected =
            PartyManager.Instance.SelectedUnit;

        if (selected == null)
            return;

        PlayerStats stats =
            selected.GetComponent<PlayerStats>();

        HealthComponent health =
            selected.GetComponent<HealthComponent>();

        if (stats == null || health == null)
            return;

        int missingHealth =
            Mathf.Max(
                0,
                stats.maxHealth - stats.currentHealth);

        if (missingHealth <= 0)
            return;

        int totalCost =
            missingHealth * 2;

        if (CurrencyManager.Instance == null)
            return;

        if (!CurrencyManager.Instance.SpendCoins(totalCost))
        {
            Debug.Log(
                $"[VendingMachineUI] Need {totalCost} gold to heal fully.");

            return;
        }

        health.SetHealth(stats.maxHealth);

        RefreshHealingSection(selected);
        RefreshStoreCards(selected);

        Debug.Log(
            $"[VendingMachineUI] Fully healed {selected.name} for {totalCost} gold.");
    }


    private string FormatEffect(
    ItemEffectType type,
    float value)
    {
        switch (type)
        {
            case ItemEffectType.Damage:
                return $"+{value:0} DAMAGE";

            case ItemEffectType.MaxHealth:
                return $"+{value:0} HEALTH";

            case ItemEffectType.MaxStamina:
                return $"+{value:0} STAMINA";

            case ItemEffectType.DamageReduction:
                return $"{value:0}% REDUCTION";

            case ItemEffectType.AttackStaminaCost:
                return $"{value:0} COST";

            default:
                return value.ToString("0");
        }
    }
}

