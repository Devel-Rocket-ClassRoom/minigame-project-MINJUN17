using System.Collections.Generic;
using UnityEngine;

public class SalesTracker : MonoBehaviour
{
    public static SalesTracker Instance;

    private readonly Dictionary<MenuData, int> _monthlySales = new();

    public IReadOnlyDictionary<MenuData, int> MonthlySales => _monthlySales;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RecordSale(MenuData menu)
    {
        if (menu == null) return;
        _monthlySales.TryGetValue(menu, out int count);
        _monthlySales[menu] = count + 1;
    }

    public long CalculateMaterialCost()
    {
        long total = 0;
        foreach (var kv in _monthlySales)
            total += (long)kv.Key.cost * kv.Value;
        return total;
    }

    public void ResetMonthly() => _monthlySales.Clear();
}