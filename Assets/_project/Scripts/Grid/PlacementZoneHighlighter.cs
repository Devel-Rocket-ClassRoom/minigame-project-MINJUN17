using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치/이동 모드일 때 땅에 "설치 가능(초록)/불가(빨강)" 구역을 칠하는 런타임 오버레이.
/// 현재 floor의 활성 셀마다 반투명 스프라이트를 깔고, 가구가 속한 존만 초록으로.
/// 1x1 흰 스프라이트를 런타임 생성하므로 별도 에셋이 필요 없다.
/// PlacementSystem이 Show/Hide를 호출한다.
/// </summary>
public class PlacementZoneHighlighter : MonoBehaviour
{
    [SerializeField] private Color allowedColor = new Color(0f, 1f, 0f, 0.25f);
    [SerializeField] private Color blockedColor = new Color(1f, 0f, 0f, 0.25f);
    [SerializeField] private Color previewColor = new Color(0f, 0.5f, 1f, 0.4f);   // 설치중인 가구 밑 셀
    [Tooltip("가구/캐릭터에 가리면 올리고, 너무 위로 덮으면 낮춰서 조정")]
    [SerializeField] private int sortingOrder = 100;
    [Range(0.1f, 1f)]
    [Tooltip("셀 채움 비율. 1보다 작을수록 칸 사이 간격이 커짐 (기본 0.9)")]
    [SerializeField] private float cellFill = 0.9f;

    private readonly List<SpriteRenderer> _pool = new();
    private Sprite _cellSprite;
    private Transform _root;
    private bool _inited;

    private void EnsureInit()
    {
        if (_inited) return;
        _inited = true;

        // 1x1 흰 텍스처 → 1유닛 스프라이트 (pixelsPerUnit = 1)
        var tex = Texture2D.whiteTexture;
        _cellSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width);

        _root = new GameObject("ZoneHighlightPool").transform;
        _root.SetParent(transform, false);
        _root.gameObject.SetActive(false);
    }

    private CellZone _allowed;
    private FloorIndex _floor;
    private bool _wallMount;

    /// <summary>현재 floor에서 allowed 존은 초록, 나머지 활성 셀은 빨강으로 칠한다.</summary>
    public void Show(CellZone allowed, FloorIndex floor, bool wallMount = false)
    {
        EnsureInit();
        _allowed = allowed;
        _floor = floor;
        _wallMount = wallMount;
        _root.gameObject.SetActive(true);
        Refresh(default, 0, 0, hasPreview: false);
    }

    /// <summary>매 프레임 호출 — preview 가구 풋프린트(origin~+w/h) 셀을 파란색으로 덮는다.</summary>
    public void UpdatePreview(Vector2Int origin, int width, int height)
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        Refresh(origin, width, height, hasPreview: true);
    }

    private void Refresh(Vector2Int previewOrigin, int pw, int ph, bool hasPreview)
    {
        var grid = GridManager.Instance;
        if (grid == null) return;

        int used = 0;
        int w = grid.GridWidth, h = grid.GridHeight;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            var cell = grid.GetCell(new Vector2Int(x, y));
            if (cell == null || !cell.isActive) continue;
            if (!grid.IsCellOnFloor(y, _floor)) continue;

            // 존이 맞아도 점유/예약이면 못 놓음 → 빨강 (isWall은 CanPlace와 동일하게 검사하지 않음)
            bool placeable = !cell.isOccupied && (_wallMount || (GridManager.ZoneAccepts(_allowed, cell.zone) && !cell.isReserved));

            // 설치중인 가구가 덮는 셀이면 파란색
            bool underPreview = hasPreview
                && x >= previewOrigin.x && x < previewOrigin.x + pw
                && y >= previewOrigin.y && y < previewOrigin.y + ph;

            var sr = GetRenderer(used++);
            sr.transform.position = grid.CellToWorld(new Vector2Int(x, y));
            sr.transform.localScale = new Vector3(cellFill, cellFill, 1f);   // 칸 사이 간격
            sr.color = underPreview ? (placeable ? previewColor : blockedColor) : (placeable ? allowedColor : blockedColor);
            sr.gameObject.SetActive(true);
        }

        // 남는 풀은 끔
        for (int i = used; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);
    }

    public void Hide()
    {
        if (_root != null) _root.gameObject.SetActive(false);
    }

    private SpriteRenderer GetRenderer(int i)
    {
        while (_pool.Count <= i)
        {
            var go = new GameObject("ZoneCell");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _cellSprite;
            sr.sortingOrder = sortingOrder;
            _pool.Add(sr);
        }
        return _pool[i];
    }
}
