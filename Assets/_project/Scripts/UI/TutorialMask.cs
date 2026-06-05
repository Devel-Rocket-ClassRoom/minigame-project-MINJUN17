using UnityEngine;

/// <summary>
/// 튜토리얼 마스크 — 지정 UI(target)만 남기고 나머지를 가리고 클릭 차단.
/// 상/하/좌/우 4패널을 "정규화 앵커(0~1)"로 배치 → 캔버스 스케일러/해상도 무관하게 정확.
/// target을 매 LateUpdate로 추적 → 패널이 열리며 레이아웃이 바뀌어도 구멍이 따라감.
///
/// ⚠️ 이 오브젝트(마스크 루트)는 Canvas 직속 + 풀스크린(stretch)이어야 함. (SafeArea 안에 넣지 말 것)
/// </summary>
public class TutorialMask : MonoBehaviour
{
    [Header("4방향 가림 패널 (검정 반투명, Raycast Target ON)")]
    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform bottom;
    [SerializeField] private RectTransform left;
    [SerializeField] private RectTransform right;

    private Canvas _canvas;
    private RectTransform _target;
    private float _pad = 2f;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        Hide();
    }

    /// <summary>전체 가림 (구멍 없음). 대사 단계용.</summary>
    public void CoverAll()
    {
        gameObject.SetActive(true);
        _target = null;
        ApplyHole(new Rect(0, 0, 0, 0));
    }

    /// <summary>target 영역만 뚫고 클릭 허용. 매 프레임 추적.</summary>
    public void HighlightUI(RectTransform target, float padding = 2f)
    {
        if (target == null) { CoverAll(); return; }
        gameObject.SetActive(true);
        _target = target;
        _pad = padding;
        UpdateHole();
    }

    public void Hide()
    {
        _target = null;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_target != null) UpdateHole();   // 레이아웃/애니 정착 후에도 따라감
    }

    private void UpdateHole()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _canvas.worldCamera : null;

        var c = new Vector3[4];
        _target.GetWorldCorners(c);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, c[2]);

        ApplyHole(Rect.MinMaxRect(min.x - _pad, min.y - _pad, max.x + _pad, max.y + _pad));
    }

    /// <summary>스크린 px 구멍을 정규화(0~1)해서 4패널 앵커로 배치.</summary>
    private void ApplyHole(Rect holePx)
    {
        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);
        float x0 = Mathf.Clamp01(holePx.xMin / w);
        float y0 = Mathf.Clamp01(holePx.yMin / h);
        float x1 = Mathf.Clamp01(holePx.xMax / w);
        float y1 = Mathf.Clamp01(holePx.yMax / h);

        SetAnchors(top,    0f, y1, 1f, 1f);   // 구멍 위
        SetAnchors(bottom, 0f, 0f, 1f, y0);   // 구멍 아래
        SetAnchors(left,   0f, y0, x0, y1);   // 구멍 왼쪽
        SetAnchors(right,  x1, y0, 1f, y1);   // 구멍 오른쪽
    }

    private static void SetAnchors(RectTransform p, float xMin, float yMin, float xMax, float yMax)
    {
        if (p == null) return;
        p.anchorMin = new Vector2(xMin, yMin);
        p.anchorMax = new Vector2(xMax, yMax);
        p.offsetMin = Vector2.zero;
        p.offsetMax = Vector2.zero;
    }
}
