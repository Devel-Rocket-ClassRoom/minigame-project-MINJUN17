using System.Collections.Generic;
using UnityEngine;

public class SinkManager : MonoBehaviour
{
    public static SinkManager Instance;

    private readonly List<Sink> _sinks = new();

    public bool HasAny => _sinks.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(Sink s)
    {
        if (s != null && !_sinks.Contains(s)) _sinks.Add(s);
    }

    public void Unregister(Sink s)
    {
        _sinks.Remove(s);
    }

    public Sink FindNearestAvailable(Vector3 from)
    {
        Sink best = null;
        float bestDist = float.MaxValue;
        foreach (var s in _sinks)
        {
            if (s == null || s.IsOccupied) continue;
            float d = Vector3.SqrMagnitude(s.transform.position - from);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }
}
