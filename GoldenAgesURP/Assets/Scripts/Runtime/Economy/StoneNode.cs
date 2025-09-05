using UnityEngine;

public class StoneNode : ResourceNode, IGatherable  
{
    [Header("Yield")]
    [SerializeField] private int total = 200;
    [SerializeField] private int yieldPerTick = 1;
    [SerializeField] private float tickInterval = 1.0f;

    public ResourceType YieldsType => ResourceType.Stone;
    public int YieldPerTick => yieldPerTick;
    public float TickInterval => tickInterval;
    public bool IsDepleted => total <= 0;

    public bool TryConsume(int amount, out int actual)
    {
        actual = Mathf.Clamp(amount, 0, Mathf.Max(0, total));
        total -= actual;
        return actual > 0;
    }
}
