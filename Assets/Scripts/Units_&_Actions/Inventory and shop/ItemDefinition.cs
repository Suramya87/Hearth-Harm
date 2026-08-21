using System;
using UnityEngine;

[Serializable]
public class ItemDefinition
{
    public string id;
    public string character;
    public string displayName;
    public string slot;

    public int baseCost;

    public ItemEffectType effectType;
    public float effectValue;

    [TextArea]
    public string description;
}

