using System;
using UnityEngine;

[Serializable]
public class ItemData
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [TextArea]
    public string description;

    [Header("Class")]
    public PlayerClass playerClass;

    [Header("Item Category")]
    public string slot;

    [Header("Shop")]
    public int baseCost;

    [Header("Effect")]
    public ItemEffectType effectType;
    public float baseEffectValue;
}

public enum ItemEffectType
{
    Damage,
    MaxHealth,
    MaxStamina,
    DamageReduction,
    AttackStaminaCost
}