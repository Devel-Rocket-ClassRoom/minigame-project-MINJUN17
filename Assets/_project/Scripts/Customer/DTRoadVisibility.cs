using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DT 해금 상태에 따라 DT 도로 타일맵 등의 GameObject 표시를 토글.
/// - 게임 시작 시 DTSystem.IsUnlocked 체크 → 도로 보이기/숨기기
/// - 게임 중 해금 시 DTSystem.OnUnlocked 이벤트 받아 자동 활성화
/// - 세이브/로드 후에도 IsUnlocked가 복원되면 Start에서 자동 적용
/// </summary>
public class DTRoadVisibility : MonoBehaviour
{
    [Header("DT 해금 시 보일 오브젝트들 (도로 타일맵, DT 가이드 마커 등)")]
    [SerializeField] private List<GameObject> dtVisuals = new();

    private void Start()
    {
        if (DTSystem.Instance != null)
        {
            DTSystem.Instance.OnUnlocked += HandleUnlocked;
            ApplyVisibility(DTSystem.Instance.IsUnlocked);
        }
        else
        {
            // DTSystem이 씬에 없으면 안전하게 숨김
            ApplyVisibility(false);
        }
    }

    private void OnDestroy()
    {
        if (DTSystem.Instance != null)
            DTSystem.Instance.OnUnlocked -= HandleUnlocked;
    }

    private void HandleUnlocked() => ApplyVisibility(true);

    private void ApplyVisibility(bool visible)
    {
        for (int i = 0; i < dtVisuals.Count; i++)
        {
            if (dtVisuals[i] != null)
                dtVisuals[i].SetActive(visible);
        }
    }
}
