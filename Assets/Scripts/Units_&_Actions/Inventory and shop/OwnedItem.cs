using System;

[Serializable]
public class OwnedItem
{
    public string itemID;
    public int upgradeLevel;

    public OwnedItem(string itemID)
    {
        this.itemID = itemID;
        upgradeLevel = 0;
    }
}