using UnityEngine;

/// <summary>
/// 2층이 해금(활성 셀 존재)됐을 때만 대상 오브젝트를 활성화. 계단 안내 화살표 등에 사용.
/// FloorToggleButton과 동일한 판단(ExpansionManager.OnExpanded + GridManager 활성 셀).
///
/// ⚠️ 이 컴포넌트가 붙은 오브젝트는 씬에서 "활성" 상태로 시작해야 Start가 돌아 구독됨.
///    (2층 미해금이면 Start에서 스스로 꺼지고, 해금되면 콜백으로 다시 켜짐)
/// </summary>
public class ShowWhenFloor2Unlocked : MonoBehaviour
{
    [Tooltip("켜고 끌 대상. 비우면 자기 자신.")]
    [SerializeField] private GameObject target;

    private void Awake()
    {
        if (target == null) target = gameObject;
    }

    private void Start()
    {
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded += OnExpanded;
        UpdateVisibility();   // 세이브 로드(실행순서 -50) 이후라 복원된 해금 상태 반영됨
    }

    private void OnDestroy()
    {
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded -= OnExpanded;
    }

    private void OnExpanded(ExpansionStageData _) => UpdateVisibility();

    private void UpdateVisibility()
    {
        bool unlocked = GridManager.Instance != null
                     && GridManager.Instance.GetActiveBoundsForFloor(FloorIndex.Floor2).HasValue;

        if (target != null && target.activeSelf != unlocked)
            target.SetActive(unlocked);
    }
}
