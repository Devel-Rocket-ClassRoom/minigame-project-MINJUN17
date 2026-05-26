using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세로 스크롤 ScrollView 안의 GridLayoutGroup 셀 크기를 자동 계산.
/// 사용처: 상점 슬롯, 해금 슬롯 등 동일 패턴의 그리드.
///
/// 부착 위치: ScrollView → Viewport → Content (GridLayoutGroup 이 붙어있는 GO).
/// 컬럼 수와 가로:세로 비율만 인스펙터에서 지정하면 끝.
/// ScrollView 너비가 바뀌어도 (해상도/회전/리사이즈) 알아서 재계산.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(GridLayoutGroup), typeof(RectTransform))]
public class ScrollGridCellSizer : MonoBehaviour
{
    [Tooltip("열 개수 (고정)")]
    [SerializeField, Min(1)] private int columns = 3;

    [Tooltip("셀 가로:세로 비율.\n1 = 정사각\n1.5 = 가로가 세로의 1.5배 (가로로 김)\n0.7 = 세로로 김")]
    [SerializeField, Min(0.01f)] private float aspect = 1f;

    private GridLayoutGroup _grid;
    private RectTransform _rt;

    private void Awake()  { Cache(); }
    private void OnEnable() { Resize(); }
    private void OnRectTransformDimensionsChange() { Resize(); }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Cache();
        // OnValidate 는 prefab 인스턴스화/씬 로드 중에도 호출되므로 한 프레임 늦춰서 안전하게
        if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += SafeResize;
        else Resize();
    }

    private void SafeResize()
    {
        if (this == null) return;     // 컴포넌트 제거된 경우
        Resize();
    }
#endif

    private void Cache()
    {
        if (_grid == null) _grid = GetComponent<GridLayoutGroup>();
        if (_rt   == null) _rt   = (RectTransform)transform;
    }

    private void Resize()
    {
        Cache();
        if (_grid == null || _rt == null) return;

        float w = _rt.rect.width
                  - _grid.padding.left - _grid.padding.right
                  - _grid.spacing.x * (columns - 1);
        if (w <= 0f) return;

        float cellW = w / columns;
        float cellH = cellW / aspect;

        // 세로 스크롤 표준 셋업 강제
        _grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        _grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        _grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.cellSize        = new Vector2(cellW, cellH);
    }
}
