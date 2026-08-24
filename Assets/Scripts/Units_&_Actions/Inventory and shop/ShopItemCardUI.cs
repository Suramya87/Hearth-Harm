using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemCardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    private ItemData item;

    public ItemData Item => item;

    public event Action<ItemData> OnClicked;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    public void SetItem(ItemData newItem)
    {
        item = newItem;

        if (item == null)
        {
            Clear();
            return;
        }

        if (itemNameText != null)
            itemNameText.text = item.displayName;

        if (descriptionText != null)
            descriptionText.text = item.description;

        if (effectText != null)
            effectText.text = FormatEffect(item);

        if (priceText != null)
            priceText.text = $"{item.baseCost} GOLD";

        if (button != null)
            button.interactable = true;

        gameObject.SetActive(true);
    }

    public void Clear()
    {
        item = null;

        if (itemNameText != null)
            itemNameText.text = "---";

        if (descriptionText != null)
            descriptionText.text = "";

        if (effectText != null)
            effectText.text = "";

        if (priceText != null)
            priceText.text = "";

        if (button != null)
            button.interactable = false;
    }

    private void HandleClick()
    {
        if (item == null)
            return;

        OnClicked?.Invoke(item);
    }

    private string FormatEffect(ItemData data)
    {
        switch (data.effectType)
        {
            case ItemEffectType.Damage:
                return $"+{data.baseEffectValue:0} DAMAGE";

            case ItemEffectType.MaxHealth:
                return $"+{data.baseEffectValue:0} HEALTH";

            case ItemEffectType.MaxStamina:
                return $"+{data.baseEffectValue:0} STAMINA";

            case ItemEffectType.DamageReduction:
                return $"{data.baseEffectValue:0}% DAMAGE REDUCTION";

            case ItemEffectType.AttackStaminaCost:
                return $"{data.baseEffectValue:0} ATTACK COST";

            default:
                return "";
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    public void SetStatusText(string text)
    {
        if (priceText != null)
            priceText.text = text;
    }
}