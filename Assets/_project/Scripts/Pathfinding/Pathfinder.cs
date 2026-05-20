using System.Collections.Generic;
using UnityEngine;

public enum PathRole { Customer, Cook, Server }

public static class Pathfinder
{
    // start, end 셀 기준 A* 경로 반환. 못 찾으면 null.
    // 경로의 마지막 원소가 end(목적지 셀).
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, PathRole role)
    {
        var grid = GridManager.Instance;
        if (grid == null) return null;
        if (!grid.IsInBounds(start) || !grid.IsInBounds(end)) return null;

        // 시작 == 끝
        if (start == end) return new List<Vector2Int> { end };

        var open = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        var fScore = new Dictionary<Vector2Int, int> { [start] = Heuristic(start, end) };

        while (open.Count > 0)
        {
            // open 중 fScore 최소
            Vector2Int current = open[0];
            int bestF = fScore[current];
            for (int i = 1; i < open.Count; i++)
            {
                int f = fScore[open[i]];
                if (f < bestF) { current = open[i]; bestF = f; }
            }

            if (current == end) return Reconstruct(cameFrom, current);

            open.Remove(current);

            foreach (var n in Neighbors(current))
            {
                if (!grid.IsInBounds(n)) continue;
                // 목적지 셀은 walkable이 아니어도 통과(도구/카운터 위 등 케이스)
                if (n != end && !grid.IsCellWalkable(n, role)) continue;

                int tentative = gScore[current] + 1;
                if (!gScore.TryGetValue(n, out int g) || tentative < g)
                {
                    cameFrom[n] = current;
                    gScore[n] = tentative;
                    fScore[n] = tentative + Heuristic(n, end);
                    if (!open.Contains(n)) open.Add(n);
                }
            }
        }
        return null;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        yield return new Vector2Int(c.x + 1, c.y);
        yield return new Vector2Int(c.x - 1, c.y);
        yield return new Vector2Int(c.x, c.y + 1);
        yield return new Vector2Int(c.x, c.y - 1);
    }

    private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int end)
    {
        var path = new List<Vector2Int> { end };
        while (cameFrom.TryGetValue(end, out var prev))
        {
            path.Insert(0, prev);
            end = prev;
        }
        return path;
    }
}
