using UnityEngine;

public static class DropOffFinder
{
    public static IDropOff FindNearest(Vector3 from, ResourceType type)
    {
        IDropOff best = null;
        float bestSqr = float.MaxValue;

        var list = DropOffRegistry.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null || !d.Accepts(type)) continue;
            var c = (d as Component);
            if (!c) continue;
            float sq = (c.transform.position - from).sqrMagnitude;
            if (sq < bestSqr) { bestSqr = sq; best = d; }
        }
        return best;
    }

}
