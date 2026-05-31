using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FloorIndex { Floor1 = 0, Floor2 = 1 }

[DefaultExecutionOrder(-50)] // GridManager 이후
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [SerializeField] private Camera mainCamera;
    [SerializeField] private UnityEngine.Rendering.Universal.PixelPerfectCamera pixelPerfectCamera;
    [SerializeField] private float cameraPadding = 1f;
    [SerializeField] private float toggleDuration = 0.4f;

    [Header("Floor 토글 시 cullingMask 제어")]
    [Tooltip("이 Layer는 Floor1 카메라에서만 보임 (Floor2에선 숨김). 보통 DT 차로/차들.")]
    [SerializeField] private string dtLayerName = "DT";
    [Tooltip("Floor1에 속한 visual Layer. Floor1 카메라에서만 보임.")]
    [SerializeField] private string floor1LayerName = "Floor1";
    [Tooltip("Floor2에 속한 visual Layer. Floor2 카메라에서만 보임.")]
    [SerializeField] private string floor2LayerName = "Floor2";

    [Header("디버그 키 (정식 UI 만들면 제거)")]
    [SerializeField] private Key debugToggleKey = Key.Space;

    private FloorIndex _currentFloor = FloorIndex.Floor1;
    private Coroutine _toggleCo;
    private int _baseRefX, _baseRefY, _baseAssetsPPU;

    public FloorIndex CurrentFloor => _currentFloor;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (mainCamera == null) mainCamera = Camera.main;

        if (pixelPerfectCamera == null && mainCamera != null)
            pixelPerfectCamera = mainCamera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();

        if (pixelPerfectCamera != null)
        {
            _baseRefX = pixelPerfectCamera.refResolutionX;
            _baseRefY = pixelPerfectCamera.refResolutionY;
            _baseAssetsPPU = pixelPerfectCamera.assetsPPU;
        }
    }

    private void Start()
    {
        Refresh();   // 초기 1층 fit + ortho 계산

        // DT 해금 시 카메라 영역에 차로 bbox를 자동 포함시키기 위해 구독
        if (DTSystem.Instance != null)
            DTSystem.Instance.OnUnlocked += OnDTUnlocked;
    }

    private void OnDestroy()
    {
        if (DTSystem.Instance != null)
            DTSystem.Instance.OnUnlocked -= OnDTUnlocked;
    }

    private void OnDTUnlocked()
    {
        // 1) DT까지 포함해 줌(orthoSize) + 가로중심 맞춤
        Refresh();
        // 2) 세로(Y)는 "2층 갔다 1층 온" 토글처럼 해당 층 건물 중심으로 재정렬
        //    (Refresh가 잡은 줌/가로는 SetFloor가 그대로 유지하고 Y만 건물 중심으로)
        SetFloor(_currentFloor);
    }

    // 외부에서 floor 전환 (UI 버튼 / 디버그용)
    // 줌(orthoSize)은 그대로 유지하고 y만 floor 중앙으로 즉시 이동.
    public void SetFloor(FloorIndex floor, bool animated = true)
    {
        _currentFloor = floor;
        ApplyVisibility(floor);

        if (GridManager.Instance == null || mainCamera == null)
        {
            Debug.LogWarning("[CameraController] SetFloor: GridManager/mainCamera 없음");
            return;
        }
        Bounds? boundsOpt = GridManager.Instance.GetActiveBoundsForFloor(floor);
        if (!boundsOpt.HasValue)
        {
            Debug.LogWarning($"[CameraController] SetFloor({floor}): 활성 셀 없음 — stage 확장됐는지 확인");
            return;
        }
        Bounds bounds = boundsOpt.Value;

        Vector3 currentPos = mainCamera.transform.position;
        Vector3 newPos = new Vector3(currentPos.x, bounds.center.y, currentPos.z);
        float ortho = GetCurrentOrthoSize();
        Debug.Log($"[CameraController] SetFloor({floor}): y {currentPos.y:F1}→{newPos.y:F1}, bounds y={bounds.center.y:F1}");

        if (_toggleCo != null) StopCoroutine(_toggleCo);
        ApplyImmediate(newPos, ortho);   // 애니메이션 X, 즉시 점프
    }

    // 현재 floor에 따라 cullingMask에서 DT / Floor1 / Floor2 Layer 토글
    private void ApplyVisibility(FloorIndex floor)
    {
        if (mainCamera == null) return;
        int dt = LayerMask.NameToLayer(dtLayerName);
        int f1 = LayerMask.NameToLayer(floor1LayerName);
        int f2 = LayerMask.NameToLayer(floor2LayerName);

        if (floor == FloorIndex.Floor1)
        {
            SetMask(dt, true);
            SetMask(f1, true);
            SetMask(f2, false);
        }
        else
        {
            SetMask(dt, false);
            SetMask(f1, false);
            SetMask(f2, true);
        }
    }

    private void SetMask(int layer, bool visible)
    {
        if (layer < 0) return;   // Layer 미등록 시 패스 (회귀 없음)
        int bit = 1 << layer;
        if (visible) mainCamera.cullingMask |= bit;
        else         mainCamera.cullingMask &= ~bit;
    }

    public void ToggleFloor()
        => SetFloor(_currentFloor == FloorIndex.Floor1 ? FloorIndex.Floor2 : FloorIndex.Floor1);

    private void Update()
    {
        if (debugToggleKey != Key.None
            && Keyboard.current != null
            && Keyboard.current[debugToggleKey].wasPressedThisFrame)
            ToggleFloor();
    }

    // 확장/배치 등으로 현재 floor 활성 영역이 변동된 직후 호출
    public void Refresh()
    {
        ApplyVisibility(_currentFloor);
        var target = ComputeCameraTarget(_currentFloor);
        if (!target.valid) return;
        if (_toggleCo != null) StopCoroutine(_toggleCo);
        ApplyImmediate(target.position, target.orthoSize);
    }

    private struct CamTarget { public Vector3 position; public float orthoSize; public bool valid; }

    private CamTarget ComputeCameraTarget(FloorIndex floor)
    {
        var result = default(CamTarget);
        if (mainCamera == null) return result;
        if (GridManager.Instance == null) return result;

        Bounds? boundsOpt = GridManager.Instance.GetActiveBoundsForFloor(floor);
        if (!boundsOpt.HasValue) return result;
        Bounds combined = boundsOpt.Value;

        // 1층 카메라일 때만 DTLane 영역도 포함 (DT는 1층 개념)
        if (floor == FloorIndex.Floor1
            && DTLane.Instance != null && DTLane.Instance.WaypointCount > 0
            && DTSystem.Instance != null && DTSystem.Instance.IsUnlocked)
            combined.Encapsulate(DTLane.Instance.GetVisibleBounds());

        Vector3 pos = mainCamera.transform.position;
        result.position = new Vector3(combined.center.x, combined.center.y, pos.z);

        if (mainCamera.orthographic)
        {
            float aspect = mainCamera.aspect > 0.01f ? mainCamera.aspect : 1f;
            float halfH = combined.extents.y;
            float halfW = combined.extents.x / aspect;
            result.orthoSize = Mathf.Max(halfH, halfW) + cameraPadding;
        }
        else
        {
            result.orthoSize = mainCamera.orthographicSize;
        }
        result.valid = true;
        return result;
    }

    private void ApplyImmediate(Vector3 position, float orthoSize)
    {
        mainCamera.transform.position = position;
        ApplyZoom(orthoSize);
    }

    // PixelPerfectCamera가 있으면 refResolution을 스케일링,
    // 없으면 orthographicSize를 직접 세팅.
    private void ApplyZoom(float orthoSize)
    {
        if (pixelPerfectCamera != null && pixelPerfectCamera.enabled)
        {
            if (_baseAssetsPPU <= 0 || _baseRefY <= 0) return;
            float baseSize = _baseRefY / (2f * _baseAssetsPPU);
            if (baseSize < 0.01f) return;
            float scale = orthoSize / baseSize;
            if (scale < 0.01f) scale = 0.01f;
            int newX = Mathf.Max(1, Mathf.RoundToInt(_baseRefX * scale));
            int newY = Mathf.Max(1, Mathf.RoundToInt(_baseRefY * scale));
            Debug.Log($"[CameraController] ApplyZoom via PPC: ortho={orthoSize:F2} scale={scale:F2} refRes={newX}x{newY}");
            pixelPerfectCamera.refResolutionX = newX;
            pixelPerfectCamera.refResolutionY = newY;
        }
        else if (mainCamera != null && mainCamera.orthographic)
        {
            Debug.Log($"[CameraController] ApplyZoom via orthographicSize: {orthoSize:F2} (PPC null or disabled)");
            mainCamera.orthographicSize = orthoSize;
        }
    }

    // PPC가 있으면 PPC 공식에서 역산, 없으면 카메라에서 직접 읽음.
    private float GetCurrentOrthoSize()
    {
        if (pixelPerfectCamera != null && pixelPerfectCamera.enabled && _baseAssetsPPU > 0)
            return pixelPerfectCamera.refResolutionY / (2f * pixelPerfectCamera.assetsPPU);
        return mainCamera != null && mainCamera.orthographic ? mainCamera.orthographicSize : 11f;
    }

    private IEnumerator LerpTo(Vector3 targetPos, float targetSize, float duration)
    {
        Vector3 startPos = mainCamera.transform.position;
        float startSize = GetCurrentOrthoSize();
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, u);
            ApplyZoom(Mathf.Lerp(startSize, targetSize, u));
            yield return null;
        }
        ApplyImmediate(targetPos, targetSize);
        _toggleCo = null;
    }
}
