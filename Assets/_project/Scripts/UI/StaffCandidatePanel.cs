using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "직원모집하기" 패널. 현재 도착한 후보들을 슬롯으로 표시.
/// 후보가 0명이면 빈 메시지.
/// </summary>
public class StaffCandidatePanel : MonoBehaviour
{
    [Header("슬롯")]
    [SerializeField] private StaffCandidateSlot slotPrefab;
    [SerializeField] private RectTransform content;

    [Header("빈 상태 메시지")]
    [SerializeField] private GameObject emptyMessage;                  // "가능한 해금이 없습니다" 같은 빈 안내

    [Header("통화 아이콘")]
    [SerializeField] private Sprite moneyIcon;

    private readonly List<StaffCandidateSlot> _spawned = new();

    private void OnEnable()
    {
        SubscribeAll();
        StartCoroutine(RefreshNextFrame());
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        Refresh();
    }

    private void OnDisable() => UnsubscribeAll();

    private void SubscribeAll()
    {
        if (StaffCandidatePool.Instance != null)
        {
            StaffCandidatePool.Instance.OnApplicantsChanged += Refresh;
        }
        if (StaffManager.Instance != null)
        {
            // 채용/해고 → 정원 상태 바뀌어서 버튼 활성 여부 바뀜
            StaffManager.Instance.OnRosterChanged += Refresh;
        }
        if (MoneySystem.Instance != null)
        {
            MoneySystem.Instance.OnMoneyChanged += OnMoneyChanged;
        }
    }

    private void UnsubscribeAll()
    {
        if (StaffCandidatePool.Instance != null)
        {
            StaffCandidatePool.Instance.OnApplicantsChanged -= Refresh;
        }
        if (StaffManager.Instance != null)
        {
            StaffManager.Instance.OnRosterChanged -= Refresh;
        }
        if (MoneySystem.Instance != null)
        {
            MoneySystem.Instance.OnMoneyChanged -= OnMoneyChanged;
        }
    }

    private void OnMoneyChanged(long _)
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) _spawned[i].RefreshAffordability();
    }

    public void Refresh()
    {
        ClearSlots();

        var pool = StaffCandidatePool.Instance;

        // 후보 슬롯
        if (pool != null)
        {
            foreach (var c in pool.Applicants)
            {
                if (c == null) continue;
                var slot = Instantiate(slotPrefab, content);
                slot.Setup(c, moneyIcon);
                _spawned.Add(slot);
            }
        }

        // 빈 메시지
        if (emptyMessage != null) emptyMessage.SetActive(_spawned.Count == 0);
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();
    }
}
