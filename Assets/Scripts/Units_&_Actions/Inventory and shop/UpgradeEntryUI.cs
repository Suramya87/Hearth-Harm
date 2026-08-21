using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private Button button;

    private OwnedItem ownedItem;
    private ItemData itemData;

    public OwnedItem OwnedItem => ownedItem;
    public ItemData ItemData => itemData;

    public event Action<UpgradeEntryUI> OnClicked;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Setup(
        OwnedItem owned,
        ItemData data,
        float currentEffect)
    {
        ownedItem = owned;
        itemData = data;

        if (itemNameText != null)
            itemNameText.text = data.displayName;

        if (effectText != null)
            effectText.text =
                FormatEffect(data.effectType, currentEffect);
    }

    private string FormatEffect(
        ItemEffectType type,
        float value)
    {
        switch (type)
        {
            case ItemEffectType.Damage:
                return $"+{value:0} Damage";

            case ItemEffectType.MaxHealth:
                return $"+{value:0} Health";

            case ItemEffectType.MaxStamina:
                return $"+{value:0} Stamina";

            case ItemEffectType.DamageReduction:
                return $"{value:0}% Damage Reduction";

            case ItemEffectType.AttackStaminaCost:
                return $"{value:0} Attack Cost";

            default:
                return value.ToString("0");
        }
    }
}