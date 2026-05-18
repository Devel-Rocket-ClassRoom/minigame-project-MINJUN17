using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CounterManager : MonoBehaviour
{
    private List<Counter> counters = new();
    [SerializeField] private GameObject counterPrefab;
    [SerializeField] private Transform[] startSpawnPoints;
    

    public int CounterCount => counters.Count;
    public IReadOnlyList<Counter> Counters => counters;
    

    private void Awake()
    {
        SpawnInitialCounters();
    }

    private void SpawnInitialCounters()
    {
        if (counterPrefab == null || startSpawnPoints == null) return;

        foreach (var point in startSpawnPoints)
        {
            if (point == null) continue;
            var go = Instantiate(counterPrefab, point.position, point.rotation);
            if (go.TryGetComponent(out Counter counter))
                RegisterCounter(counter);
        }
    }

    public Counter GetReadyCounter()
    {
        foreach (var counter in counters)
        {
            if (!counter.IsEmpty && !counter.IsOccupied)
            {
                return counter;
            }
        }
        return null;
    }
    public Counter GetFirstEmptyCounter()
    {
        return counters.FirstOrDefault(c => c.IsEmpty);
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
