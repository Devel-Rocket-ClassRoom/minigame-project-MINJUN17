using System.Collections.Generic;
using UnityEngine;

public class ToiletManager : MonoBehaviour
{
    public static ToiletManager Instance;

    private readonly List<Toilet> _toilets = new();

    /// <summary>씬에 등록된 화장실 가구가 하나라도 있는지 (= 화장실 시설 존재 여부)</summary>
    public bool HasAny => _toilets.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(Toilet t)
    {
        if (t != null && !_toilets.Contains(t)) _toilets.Add(t);
    }

    public void Unregister(Toilet t)
    {
        _toilets.Remove(t);
    }

    /// <summary>비어있는 화장실 중 from에서 가장 가까운 것. 없으면 null.</summary>
    public Toilet FindNearestAvailable(Vector3 from)
    {
        Toilet best = null;
        float bestDist = float.MaxValue;
        foreach (var t in _toilets)
        {
            if (t == null || t.IsOccupied) continue;
            float d = Vector3.SqrMagnitude(t.transform.position - from);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    /// <summary>전체 화장실 중 만석 여부 (HasAny=true일 때만 의미)</summary>
    public bool AllOccupied()
    {
        if (_toilets.Count == 0) return false;
        foreach (var t in _toilets)
            if (t != null && !t.IsOccupied) return false;
        return true;
    }
}
