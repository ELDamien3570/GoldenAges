using UnityEngine;
using System;

public class TownEconomy : MonoBehaviour
{
    public int Wood;
    public int Food;
    public int Stone;

    public event Action<ResourceType, int> OnResourceAdded;

    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        switch (type)
        {
            case ResourceType.Wood: Wood += amount; break;
            case ResourceType.Food: Food += amount; break;
            case ResourceType.Stone: Stone += amount; break;
        }
        OnResourceAdded?.Invoke(type, amount);
    }
}
