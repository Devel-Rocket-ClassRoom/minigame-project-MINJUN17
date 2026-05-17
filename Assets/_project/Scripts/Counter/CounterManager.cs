using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CounterManager : MonoBehaviour
{
    [SerializeField] private List<Counter> counters = new();

    public int CounterCount => counters.Count;
    public IReadOnlyList<Counter> Counters => counters;

    private void Awake()
    {
        if (counters.Count == 0)
            counters.AddRange(FindObjectsByType<Counter>(FindObjectsSortMode.None));
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
