using UnityEngine;

public interface IGatherable : IWorkProvider
{
    ResourceType YieldsType { get; }
    int YieldPerTick { get; }

    float TickInterval { get; }

    bool TryConsume(int amount, out int actual);
    bool IsDepleted { get; }

}
