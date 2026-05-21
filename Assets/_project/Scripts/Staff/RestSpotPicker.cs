using System.Collections.Generic;
using UnityEngine;

public static class RestSpotPicker
{
    // candidates 중에서 occupiers와 blockRadius 안에 들어가지 않은 후보 중
    // from 위치에서 가장 가까운 것을 반환.
    // 모든 후보가 점유 상태면 from에서 가장 가까운 후보로 fallback.
    public static Vector3 PickClosestFree(
        Vector3 from,
        IList<Vector3> candidates,
        IList<Vector3> occupiers,
        float blockRadius)
    {
        if (candidates == null || candidates.Count == 0) return Vector3.zero;

        Vector3 best = Vector3.zero;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var c in candidates)
        {
            if (IsBlocked(c, occupiers, blockRadius)) continue;
            float d = Vector3.Distance(from, c);
            if (d < bestDist) { bestDist = d; best = c; found = true; }
        }
        if (found) return best;

        bestDist = float.MaxValue;
        foreach (var c in candidates)
        {
            float d = Vector3.Distance(from, c);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    private static bool IsBlocked(Vector3 pos, IList<Vector3> occupiers, float blockRadius)
    {
        if (occupiers == null) return false;
        float sqr = blockRadius * blockRadius;
        foreach (var o in occupiers)
            if ((pos - o).sqrMagnitude < sqr) return true;
        return false;
    }
}