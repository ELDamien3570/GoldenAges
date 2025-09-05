using UnityEngine;

public interface IDropOff
{
    bool Accepts(ResourceType type);
    Transform GetDropSpot(WorkerUnit worker);
    TownEconomy Economy { get; }

}
