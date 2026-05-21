using System.Collections.Generic;
using UnityEngine;

// 라이더룸 zone 안의 가구 deliveryBonus 합산 + 라이더 휴식 슬롯 헬퍼
public class RiderRoomManager : MonoBehaviour
{
    public static RiderRoomManager Instance;

    [SerializeField] private float minDeliveryDuration = 5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 라이더룸 가구 deliveryBonus 합 (초 단위 감소량)
    public float TotalDeliveryBonus()
    {
        float total = 0f;
        var grid = GridManager.Instance;
        if (grid == null) return 0f;

        var seen = new HashSet<PlacedObject>();
        for (int x = 0; x < grid.GridWidth; x++)
        for (int y = 0; y < grid.GridHeight; y++)
        {
            var cell = grid.GetCell(new Vector2Int(x, y));
            if (cell == null || cell.zone != CellZone.RiderRoom) continue;
            var po = cell.placedObject;
            if (po == null || po.Data == null) continue;
            if (!seen.Add(po)) continue;
            total += po.Data.deliveryBonus;
        }
        return total;
    }

    // base에서 보너스를 뺀 실제 배달 소요 시간
    public float GetEffectiveDeliveryDuration(float baseDuration)
    {
        return Mathf.Max(minDeliveryDuration, baseDuration - TotalDeliveryBonus());
    }
    public bool HasRiderRoom()
    {
        var grid = GridManager.Instance;
        if (grid == null) return false;
        for (int x = 0; x < grid.GridWidth; x++)
        for (int y = 0; y < grid.GridHeight; y++)
        {
            var c = grid.GetCell(new Vector2Int(x, y));
            if (c != null && c.isActive && c.zone == CellZone.RiderRoom) return true;
        }
        return false;
    }
}
