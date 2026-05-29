using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 특정 ExpansionStage가 해금되면 자기 GameObject를 자동 활성화한다.
/// 시작 시 미해금이면 Awake에서 SetActive(false).
/// 플레이어가 직접 설치/이동/삭제하지 않는 가구(화장실/세면대 등)용.
/// </summary>
public class ExpansionRevealable : MonoBehaviour
{
    [Tooltip("이 stage가 해금되면 자동으로 GameObject가 활성화됨")]
    [SerializeField] private ExpansionStageData revealOnStage;
    public ExpansionStageData Stage => revealOnStage;

    private static readonly List<ExpansionRevealable> _all = new();

    private void Awake()
    {
        _all.Add(this);
        if (revealOnStage == null) return;
        bool unlocked = ExpansionManager.Instance != null
                        && ExpansionManager.Instance.IsStageCompleted(revealOnStage);
        if (!unlocked) gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _all.Remove(this);
    }

    /// <summary>해당 stage에 속한 모든 revealable을 활성화. ExpansionManager가 호출.</summary>
    public static void RevealForStage(ExpansionStageData stage)
    {
        if (stage == null) return;
        foreach (var r in _all)
        {
            if (r != null && r.Stage == stage)
                r.gameObject.SetActive(true);
        }
    }
}
