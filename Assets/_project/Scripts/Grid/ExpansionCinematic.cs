using UnityEngine;

/// <summary>
/// ExpansionStage 해금 시 연출.
/// - 2층(홀) 해금  : 카메라 2층 즉시 이동 + 연기 펑 + 사운드
/// - 화장실 해금   : 카메라 2층 즉시 이동 + 반짝이 + (다른) 사운드
/// - DT 해금       : 이펙트만 (카메라는 DTSystem이 처리 → 건드리지 않음)
/// ExpansionManager.OnExpanded 구독 (해금 시점엔 셀이 이미 활성 → 영역 계산 가능).
/// </summary>
public class ExpansionCinematic : MonoBehaviour
{
    [Header("스테이지")]
    [SerializeField] private ExpansionStageData floor2HallStage;   // 2층(홀) 해금
    [SerializeField] private ExpansionStageData toiletStage;       // 화장실 해금
    [SerializeField] private ExpansionStageData dtStage;           // DT 해금

    [Header("연기 (2층 해금)")]
    [SerializeField] private GameObject smokePrefab;
    [Tooltip("연기 생성 위치들. 여러 개 넣으면 동시에 다 터짐. 비우면 2층 활성 영역 중앙 한 곳.")]
    [SerializeField] private Transform[] smokeSpawnPoints;
    [Tooltip("연기 프리팹 자동 파괴까지 시간(초). 프리팹이 스스로 파괴되면 0.")]
    [SerializeField] private float smokeLifetime = 1.5f;

    [Header("반짝이 (화장실 해금)")]
    [SerializeField] private GameObject toiletEffectPrefab;
    [Tooltip("반짝이 생성 위치들. 여러 개 넣으면 동시에 다 나옴. 비우면 2층 활성 영역 중앙 한 곳.")]
    [SerializeField] private Transform[] toiletEffectSpawnPoints;
    [Tooltip("반짝이 프리팹 자동 파괴까지 시간(초). 프리팹이 스스로 파괴되면 0.")]
    [SerializeField] private float toiletEffectLifetime = 1.5f;

    [Header("이펙트 (DT 해금 — 카메라는 DTSystem이 처리)")]
    [SerializeField] private GameObject dtEffectPrefab;
    [Tooltip("DT 이펙트 생성 위치들. 여러 개 넣으면 동시에 다 터짐. 비우면 1층 활성 영역 중앙 한 곳.")]
    [SerializeField] private Transform[] dtEffectSpawnPoints;
    [Tooltip("DT 이펙트 프리팹 자동 파괴까지 시간(초). 프리팹이 스스로 파괴되면 0.")]
    [SerializeField] private float dtEffectLifetime = 1.5f;

    [Header("사운드")]
    [SerializeField] private SfxId floor2Sfx = SfxId.Floor2Unlock;   // 2층 — 펑
    [SerializeField] private SfxId toiletSfx = SfxId.ToiletUnlock;   // 화장실 — 뾰로롱
    [SerializeField] private SfxId dtSfx = SfxId.DTUnlock;           // DT 해금

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

        if (stage == floor2HallStage)
        {
            MoveCameraToFloor2();
            SpawnEffect(smokePrefab, smokeSpawnPoints, smokeLifetime, FloorIndex.Floor2);
            SoundManager.Instance?.PlaySfx(floor2Sfx);
        }
        else if (stage == toiletStage)
        {
            MoveCameraToFloor2();
            SpawnEffect(toiletEffectPrefab, toiletEffectSpawnPoints, toiletEffectLifetime, FloorIndex.Floor2);
            SoundManager.Instance?.PlaySfx(toiletSfx);
        }
        else if (stage == dtStage)
        {
            // 카메라는 DTSystem이 알아서 처리 → 이펙트 + 사운드만
            SpawnEffect(dtEffectPrefab, dtEffectSpawnPoints, dtEffectLifetime, FloorIndex.Floor1);
            SoundManager.Instance?.PlaySfx(dtSfx);
        }
    }

    private void MoveCameraToFloor2()
    {
        if (CameraController.Instance != null)
            CameraController.Instance.SetFloor(FloorIndex.Floor2);   // 즉시 점프
    }

    /// <summary>지정 위치들마다 동시에 이펙트 생성. 위치 미지정 시 fallbackFloor 영역 중앙 한 곳.</summary>
    private void SpawnEffect(GameObject prefab, Transform[] points, float lifetime, FloorIndex fallbackFloor)
    {
        if (prefab == null) return;

        if (points != null && points.Length > 0)
        {
            foreach (var p in points)
                if (p != null) SpawnAt(prefab, p.position, lifetime);
        }
        else
        {
            Bounds? boundsOpt = GridManager.Instance != null
                ? GridManager.Instance.GetActiveBoundsForFloor(fallbackFloor)
                : null;
            SpawnAt(prefab, boundsOpt.HasValue ? boundsOpt.Value.center : transform.position, lifetime);
        }
    }

    private void SpawnAt(GameObject prefab, Vector3 pos, float lifetime)
    {
        var go = Instantiate(prefab, pos, Quaternion.identity);
        if (lifetime > 0f) Destroy(go, lifetime);
    }
}
