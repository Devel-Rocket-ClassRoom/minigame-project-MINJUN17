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
        SetFloor(FloorIndex.Floor1, animated: false);

        // DT 해금 시 카메라 영역에 차로 bbox를 자동 포함시키기 위해 구독
        if (DTSystem.Instance != null)
            DTSystem.Instance.OnUnlocked += OnDTUnlocked;
    }

    private void OnDestroy()
    {
        if (DTSystem.Instance != null)
            DTSystem.Instance.OnUnlocked -= OnDTUnlocked;
    }

    private void OnDTUnlocked() => Refresh();

    // 외부에서 floor 전환 (UI 버튼 / 디버그용)
    public void SetFloor(FloorIndex floor, bool animated = true)
    {
        _currentFloor = floor;
        ApplyVisibility(floor);
        var target = ComputeCameraTarget(floor);
        if (!target.valid) return;
        if (_toggleCo != null) StopCoroutine(_toggleCo);
        if (animated && mainCamera != null && Application.isPlaying)
            _toggleCo = StartCoroutine(LerpTo(target.position, target.orthoSize, toggleDuration));
        else
            ApplyImmediate(target.position, target.orthoSize);
    }

    // 현재 floor에 따라 cullingMask에서 DT Layer를 토글
    private void ApplyVisibility(FloorIndex floor)
    {
        if (mainCamera == null) return;
        int dtLayer = LayerMask.NameToLayer(dtLayerName);
        if (dtLayer < 0) return; // Layer 미설정 시 패스

        int bit = 1 << dtLayer;
        if (floor == FloorIndex.Floor1)
            mainCamera.cullingMask |= bit;   // DT 보이게
        else
            mainCamera.cullingMask &= ~bit;  // DT 숨김
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
