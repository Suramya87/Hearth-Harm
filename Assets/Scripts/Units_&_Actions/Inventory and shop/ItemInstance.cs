using System;

[Serializable]
public class ItemInstance
{
    public string itemID;
    public int upgradeLevel;

    public ItemInstance(string itemID)
    {
        this.itemID = itemID;
        upgradeLevel = 0;
    }

    public float GetCurrentEffect(ItemDefinition definition)
    {
        if (definition == null)
            return 0f;

        switch (definition.effectType)
        {
            case ItemEffectType.AttackStaminaCost:
                return definition.effectValue - upgradeLevel;

            default:
                return definition.effectValue + upgradeLevel;
        }
    }
}