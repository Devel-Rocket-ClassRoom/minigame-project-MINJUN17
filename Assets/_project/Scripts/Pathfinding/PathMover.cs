using System.Collections.Generic;
using UnityEngine;

// Customer/Staff에 붙여서 경로 따라 이동하는 헬퍼.
// FSM이 SetDestination(world) 호출 → 매 프레임 Step(speed) 호출.
public class PathMover : MonoBehaviour
{
    [SerializeField] private PathRole role = PathRole.Customer;

    private List<Vector2Int> _path;
    private int _pathIndex;
    private Vector3 _finalDest;
    private Vector2Int _currentDestCell;
    private bool _hasDest;

    private const float CELL_ARRIVE = 0.05f;
    private const float FINAL_ARRIVE = 0.1f;

    public PathRole Role { get => role; set => role = value; }

    // 외부에서 목적지 설정. 같은 셀이면 재계산 안 함.
    public void SetDestination(Vector3 worldPos)
    {
        Vector2Int destCell = GridManager.Instance.WorldToCell(worldPos);

        // 이미 같은 셀로 향하는 중이면 재계산 X
        if (_hasDest && destCell == _currentDestCell)
        {
            _finalDest = worldPos; // 최종 미세 좌표만 갱신
            return;
        }

        _finalDest = worldPos;
        _currentDestCell = destCell;
        _hasDest = true;

        Vector2Int startCell = GridManager.Instance.WorldToCell(transform.position);
        _path = Pathfinder.FindPath(startCell, destCell, role);
        _pathIndex = 0;

        // 경로 못 찾으면 직진 fallback (가구 사이에 갇혔거나)
    }

    // 매 프레임 호출. 경로 따라 한 스텝 이동.
    public void Step(float speed)
    {
        if (!_hasDest) return;

        // 경로 없으면 최종 좌표로 직선 이동 (fallback)
        if (_path == null || _path.Count == 0)
        {
            transform.position = Vector3.MoveTowards(transform.position, _finalDest, speed * Time.deltaTime);
            return;
        }

        // 모든 셀 다 지났으면 최종 좌표 미세 정렬
        if (_pathIndex >= _path.Count)
        {
            transform.position = Vector3.MoveTowards(transform.position, _finalDest, speed * Time.deltaTime);
            return;
        }

        Vector3 next = GridManager.Instance.CellToWorld(_path[_pathIndex]);
        // 마지막 셀은 최종 좌표를 대신 사용 (셀 중심 말고 실제 목적지)
        if (_pathIndex == _path.Count - 1) next = _finalDest;

        transform.position = Vector3.MoveTowards(transform.position, next, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, next) <= CELL_ARRIVE)
            _pathIndex++;
    }

    public bool HasArrived()
    {
        if (!_hasDest) return true;
        return Vector3.Distance(transform.position, _finalDest) <= FINAL_ARRIVE;
    }

    public void Clear()
    {
        _hasDest = false;
        _path = null;
        _pathIndex = 0;
    }
}
