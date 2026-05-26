using System;
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
    public int CurrentStage => _currentStage;
    public IReadOnlyList<ExpansionStageData> Stages => stages;   // SaveIdRegistry용

    public event Action<ExpansionStageData> OnExpanded;

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
        OnExpanded?.Invoke(stage);
        return true;
    }

    // ─── Save / Load ───
    public ExpansionData ToData() => new ExpansionData { currentStage = _currentStage };

    public void FromData(ExpansionData data)
    {
        if (data == null) return;

        // 0 ~ (currentStage-1) 단계까지 다시 활성화 (그리드 셀 + 카메라)
        int target = Mathf.Min(data.currentStage, stages != null ? stages.Count : 0);
        for (int i = 0; i < target; i++)
        {
            if (stages[i] != null) gridManager.ActivateCells(stages[i]);
        }
        _currentStage = data.currentStage;
        // 이벤트 발화 X — 이미 적용된 상태 복원
    }
}