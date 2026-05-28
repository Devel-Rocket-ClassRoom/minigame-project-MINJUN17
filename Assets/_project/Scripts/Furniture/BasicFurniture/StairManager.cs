using System.Collections.Generic;
using UnityEngine;

public class StairManager : MonoBehaviour
{
    public static StairManager Instance { get; private set; }
    private readonly List<Stair> _stairs = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(Stair s)
    {
        if (s == null) return;
        if (!_stairs.Contains(s)) _stairs.Add(s);
    }

    public void Unregister(Stair s) => _stairs.Remove(s);

    /// <summary>특정 floor에 있는, 페어가 연결된 stair 중 from과 가장 가까운 것.</summary>
    public Stair FindNearestStairOnFloor(FloorIndex floor, Vector3 from)
    {
        Stair best = null;
        float bestSq = float.MaxValue;
        foreach (var s in _stairs)
        {
            if (s == null || s.Floor != floor || !s.HasPair) continue;
            float d = (s.transform.position - from).sqrMagnitude;
            if (d < bestSq) { bestSq = d; best = s; }
        }
        return best;
    }
}
