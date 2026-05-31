using UnityEngine;

/// <summary>
/// 이 RectTransform을 기기의 Safe Area(노치·펀치홀·둥근모서리 제외 영역)에 맞춰 자동 조정.
/// 사용: Canvas 바로 밑에 풀스크린 빈 UI 오브젝트(SafeArea)를 만들고 이 컴포넌트를 붙인 뒤,
///       HUD·버튼·패널 등 인터랙션 UI를 전부 그 안에 넣는다.
/// ※ 전체화면 배경/마스크/백드롭은 Safe Area 밖(Canvas 직속)에 둬서 화면을 꽉 채우게 한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeArea : MonoBehaviour
{
    [Tooltip("가로 방향 safe area 적용 (좌우 노치)")]
    [SerializeField] private bool conformX = true;
    [Tooltip("세로 방향 safe area 적용 (상단 노치/하단 인디케이터)")]
    [SerializeField] private bool conformY = true;

    private RectTransform _rt;
    private Rect _lastSafe = new Rect(0, 0, 0, 0);
    private Vector2Int _lastScreen = Vector2Int.zero;

    private void Awake() => _rt = GetComponent<RectTransform>();

    private void OnEnable() => Apply(force: true);

    private void Update()
    {
        // 회전/해상도 변경 시에만 재적용 (평소엔 early-out)
        if (Screen.safeArea != _lastSafe || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y)
            Apply(force: false);
    }

    private void Apply(bool force)
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (_rt == null || Screen.width <= 0 || Screen.height <= 0) return;

        Rect safe = Screen.safeArea;
        if (!force && safe == _lastSafe && Screen.width == _lastScreen.x && Screen.height == _lastScreen.y)
            return;

        _lastSafe = safe;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);

        // 픽셀 → 정규화 앵커 변환
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        if (!conformX) { min.x = 0f; max.x = 1f; }
        if (!conformY) { min.y = 0f; max.y = 1f; }

        _rt.anchorMin = min;
        _rt.anchorMax = max;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
