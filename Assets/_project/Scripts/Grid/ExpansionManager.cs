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

    /// <summary>특정 stage가 이미 통과(해금)됐는지. 세이브 로드 후에도 정확.</summary>
    public bool IsStageCompleted(ExpansionStageData stage)
    {
        if (stage == null || stages == null) return false;
        int idx = stages.IndexOf(stage);
        return idx >= 0 && idx < _currentStage;
    }

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
        if (stage.stageType == ExpansionStageData.StageType.CellExpansion)
            gridManager.ActivateCells(stage);
        _currentStage++;
        OnExpanded?.Invoke(stage);   // UnlockOnly여도 발화 → CatalogManager가 unlockOnExpansion 가구 자동 해금
        ExpansionRevealable.RevealForStage(stage);   // 자동 노출 가구(화장실/세면대 등) 활성화
        return true;
    }

    // ─── Save / Load ───
    public ExpansionData ToData() => new ExpansionData { currentStage = _currentStage };

    public void FromData(ExpansionData data)
    {
        if (data == null) return;

        // 0 ~ (currentStage-1) 단계까지 다시 활성화 (그리드 셀 + 카메라)
        // UnlockOnly stage는 셀 변경 없음 — 해금 자체는 CatalogManager FromData가 처리
        int target = Mathf.Min(data.currentStage, stages != null ? stages.Count : 0);
        for (int i = 0; i < target; i++)
        {
            if (stages[i] == null) continue;
            if (stages[i].stageType == ExpansionStageData.StageType.CellExpansion)
                gridManager.ActivateCells(stages[i]);
            ExpansionRevealable.RevealForStage(stages[i]);
        }
        _currentStage = data.currentStage;
        // 이벤트 발화 X — 이미 적용된 상태 복원
    }
}