using UnityEngine;

/// <summary>
/// Renderer의 sortingOrder를 Y 위치로 계산해 깊이 정렬.
/// Y(정렬 기준점)가 낮을수록(아래) sortingOrder가 커져 앞에 그려진다.
///
/// SpriteRenderer / TilemapRenderer 등 Renderer를 상속한 모든 컴포넌트에 동작한다.
/// (Tilemap에도 붙일 수 있음 — 단 타일맵 하나는 sortingOrder 하나뿐이라 통째로 정렬됨.
///  벽을 행별로 가렸다/안가렸다 하려면 벽 타일맵을 행 단위로 나눠 각각 붙여야 한다.)
///
/// - 움직이는 오브젝트(손님/직원): staticObject = false
/// - 고정 오브젝트(벽/가구):        staticObject = true
/// </summary>
public class YSorter : MonoBehaviour
{
    [Tooltip("Y 1유닛당 sortingOrder 차이. 클수록 미세한 Y차도 구분")]
    [SerializeField] private int precision = 100;

    [Tooltip("발밑 미세 보정 — 기본 0이면 스프라이트 bounds의 바닥 Y를 사용해서 pivot/크기 무관하게 자동 정렬. " +
             "예외적으로 끌어올리고(뒤로) 싶으면 양수, 더 내리고(앞으로) 싶으면 음수")]
    [SerializeField] private float sortYOffset = 0f;

    [Tooltip("같은 Y에서 항상 앞/뒤로 두고 싶을 때의 고정 가산값 (+면 앞)")]
    [SerializeField] private int orderBias = 0;

    [Tooltip("고정 오브젝트면 체크 — 시작 시 1회만 계산하고 이후 갱신 안 함")]
    [SerializeField] private bool staticObject = false;

    [Tooltip("체크 해제 시 transform.position.y 기준 (옛 방식). 기본은 bounds.min.y 기준 — pivot 무관하게 자동 정렬")]
    [SerializeField] private bool useSpriteBottom = true;

    private Renderer _renderer;

    private void Awake() => _renderer = GetComponent<Renderer>();

    private void Start() => Apply();

    private void LateUpdate()
    {
        if (!staticObject) Apply();
    }

    private void Apply()
    {
        if (_renderer == null) return;
        // 핵심: 스프라이트가 실제로 그려지는 월드 공간 바닥 Y를 기준으로
        // → pivot이 중심이든 발밑이든 머리든, "그려지는 모양의 밑변"이 자동으로 정렬 기준이 됨
        float baseY = useSpriteBottom ? _renderer.bounds.min.y : transform.position.y;
        float sortY = baseY + sortYOffset;
        _renderer.sortingOrder = -Mathf.RoundToInt(sortY * precision) + orderBias;
    }
}
