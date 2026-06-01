using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 아래 "현재 활성 중인 마케팅" 아이콘 표시.
/// 활성(_active) 마케팅 슬롯만 켜고, 부모의 HorizontalLayoutGroup(왼쪽 정렬)이
/// 꺼진 슬롯을 빼고 남은 아이콘을 자동으로 왼쪽 정렬한다. 마케팅 최대 3종(픽스).
/// MarketingManager.OnActiveChanged 시 갱신.
/// </summary>
public class MarketingIndicatorView : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        [Tooltip("이 슬롯이 나타내는 마케팅 데이터")]
        public MarketingData data;
        [Tooltip("켜고 끌 아이콘 오브젝트 (HorizontalLayoutGroup의 자식)")]
        public GameObject root;
        [Tooltip("(선택) 아이콘 Image — 비어있으면 data.icon 자동 적용")]
        public Image icon;
    }

    [SerializeField] private MarketingManager manager;
    [SerializeField] private List<Slot> slots = new();

    private bool _started;

    private void Start()
    {
        if (manager == null) manager = MarketingManager.Instance;
        FillIcons();
        EnsureSubscribed();
        _started = true;
        Refresh();
    }

    private void OnEnable()
    {
        if (_started) { EnsureSubscribed(); Refresh(); }
    }

    private void OnDisable()
    {
        if (manager != null) manager.OnActiveChanged -= Refresh;
    }

    private void EnsureSubscribed()
    {
        if (manager == null) return;
        manager.OnActiveChanged -= Refresh;   // 중복 구독 방지
        manager.OnActiveChanged += Refresh;
    }

    /// <summary>icon 칸이 비어 있으면 data.icon으로 자동 채움 (씬에서 이미 지정했으면 그대로).</summary>
    private void FillIcons()
    {
        foreach (var s in slots)
            if (s != null && s.icon != null && s.data != null && s.icon.sprite == null)
                s.icon.sprite = s.data.icon;
    }

    /// <summary>활성 마케팅 슬롯만 켬. 꺼진 슬롯은 레이아웃에서 빠져 나머지가 왼쪽 정렬됨.</summary>
    private void Refresh()
    {
        if (manager == null) return;
        foreach (var s in slots)
        {
            if (s == null || s.root == null) continue;
            s.root.SetActive(s.data != null && manager.IsActive(s.data));
        }
    }
}
