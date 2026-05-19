using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CounterManager : MonoBehaviour
{
    public static CounterManager Instance;
    private List<Counter> counters = new();
    [SerializeField] private GameObject counterPrefab;
    

    public int CounterCount => counters.Count;
    public IReadOnlyList<Counter> Counters => counters;
    

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public Counter GetReadyCounter()
    {
        foreach (var counter in counters)
        {
            if (!counter.IsOccupied) return counter;
        }
        return null;
    }

    public Counter GetCounterWithUnservedCustomer()
    {
        foreach (var counter in counters)
        {
            if (counter.WaitingCustomer != null && !counter.HasServicingServer)
                return counter;
        }
        return null;
    }

    public void RegisterCounter(Counter counter)
    {
        if (!counters.Contains(counter)) counters.Add(counter);
    }

    public void UnregisterCounter(Counter counter)
    {
        counters.Remove(counter);
    }
}
