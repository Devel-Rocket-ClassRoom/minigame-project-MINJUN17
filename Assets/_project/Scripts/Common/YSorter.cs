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

    [Tooltip("정렬 기준점 Y 보정. 스프라이트 중심이 아니라 '발밑/바닥 접점'으로 정렬하려면 음수로. " +
             "캐릭터=발 위치까지, 벽=앞쪽 밑변까지 내려주면 가림/안가림이 자연스러워진다")]
    [SerializeField] private float sortYOffset = 0f;

    [Tooltip("같은 Y에서 항상 앞/뒤로 두고 싶을 때의 고정 가산값 (+면 앞)")]
    [SerializeField] private int orderBias = 0;

    [Tooltip("고정 오브젝트면 체크 — 시작 시 1회만 계산하고 이후 갱신 안 함")]
    [SerializeField] private bool staticObject = false;

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
        float sortY = transform.position.y + sortYOffset;
        _renderer.sortingOrder = -Mathf.RoundToInt(sortY * precision) + orderBias;
    }
}
