using System.Collections.Generic;
using UnityEngine;

public class ExpansionManager : MonoBehaviour
{
    public static ExpansionManager Instance;

    [SerializeField] private List<ExpansionStageData> stages;  // order 순으로 정렬
    [SerializeField] private GridManager gridManager;
    private int _currentStage = 0;

    public bool CanExpand => _currentStage < stages.Count;
    public ExpansionStageData NextStage => CanExpand ? stages[_currentStage] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // UI Button.OnClick에서 직접 호출하거나 TestDebugPanel.ExpandMap 경유
    public bool TryExpand()
    {
        if (!CanExpand) return false;
        var stage = stages[_currentStage];
        if (!MoneySystem.Instance.CanAfford(stage.unlockCost)) return false;

        MoneySystem.Instance.Spend(stage.unlockCost);
        gridManager.ActivateCells(stage);  // GridManager 싱글톤이 아니면 SerializeField로 참조
        _currentStage++;
        return true;
    }
}