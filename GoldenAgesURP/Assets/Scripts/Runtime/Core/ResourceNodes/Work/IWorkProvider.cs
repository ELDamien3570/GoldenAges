using UnityEngine;

public enum WorkType { Build = 1, Chop = 2, Farm = 3, Mine = 4 }

public interface IWorkProvider
{
    WorkType Type { get; }

    // Ask the node for a work spot; returns false if none available.
    bool TryReserve(WorkerUnit worker, out Transform spot);

    // Free your spot (called when canceling/leaving/destroying).
    void Release(WorkerUnit worker);

    // Where should the worker face while working (usually node center or an override).
    Vector3 GetLookAt(Transform reservedSpot);
}
