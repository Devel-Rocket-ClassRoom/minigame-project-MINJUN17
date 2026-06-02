using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 해금 시 튜토리얼 스타일로 "한 마디"만 띄우는 가벼운 안내.
/// 기존 TutorialMask + DialogueBox + 화살표를 재활용.
/// 대사 1줄 → 탭 한 번이면 닫힘. (2층/화장실 등 해금별로 한 줄씩)
/// </summary>
public class UnlockGuide : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public ExpansionStageData stage;
        [TextArea] public string line;
        [Tooltip("하이라이트(+화살표) 대상. 비우면 전체 가림 후 대사만.")]
        public RectTransform target;
        [Tooltip("연출(이펙트)이 끝나도록 이 초만큼 기다렸다 안내 표시.")]
        public float delay = 1.5f;
    }

    [Header("튜토리얼 UI 재사용 (씬의 것 그대로 연결)")]
    [SerializeField] private TutorialMask mask;
    [SerializeField] private DialogueBox dialogue;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private Vector2 arrowOffset = new Vector2(0f, 60f);

    [Header("해금별 한 마디")]
    [SerializeField] private List<Entry> entries = new();

    private void Start()
    {
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded += OnExpanded;
    }

    private void OnDestroy()
    {
        if (ExpansionManager.Instance != null)
            ExpansionManager.Instance.OnExpanded -= OnExpanded;
    }

    private void OnExpanded(ExpansionStageData stage)
    {
        if (stage == null) return;
        foreach (var e in entries)
        {
            if (e != null && e.stage == stage) { StartCoroutine(ShowAfterDelay(e)); return; }
        }
    }

    private IEnumerator ShowAfterDelay(Entry e)
    {
        if (e.delay > 0f) yield return new WaitForSecondsRealtime(e.delay);
        Show(e);
    }

    private void Show(Entry e)
    {
        if (mask != null)
        {
            if (e.target != null) mask.HighlightUI(e.target);
            else                  mask.CoverAll();
        }
        PointArrow(e.target);

        if (dialogue != null)
            dialogue.Play(new List<string> { e.line }, Dismiss);   // 한 줄 → 탭하면 Dismiss
        else
            Dismiss();
    }

    private void PointArrow(RectTransform target)
    {
        if (arrow == null) return;
        if (target == null) { arrow.gameObject.SetActive(false); return; }
        arrow.gameObject.SetActive(true);
        arrow.position = target.position + (Vector3)arrowOffset;
    }

    private void Dismiss()
    {
        if (mask != null) mask.Hide();
        if (dialogue != null) dialogue.Hide();
        if (arrow != null) arrow.gameObject.SetActive(false);
    }
}
