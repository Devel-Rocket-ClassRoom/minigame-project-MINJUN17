using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플로팅 텍스트 풀링 + 정적 API. 씬에 1개만 두면 됨 (HUD Canvas 자식 권장).
///
/// 사용법:
///   FloatingTextSystem.SpawnMoney(worldPos, 100);            // [코인] +100
///   FloatingTextSystem.SpawnSatisfaction(worldPos, 5);       // [하트] +5
///   FloatingTextSystem.Spawn(worldPos, "text", Color.white); // 아이콘 없이
/// </summary>
public class FloatingTextSystem : MonoBehaviour
{
    public static FloatingTextSystem Instance { get; private set; }

    [Header("필수 참조")]
    [Tooltip("FloatingText 프리팹 (Image + TextMeshProUGUI + FloatingText 컴포넌트)")]
    [SerializeField] private FloatingText prefab;
    [Tooltip("이 시스템이 자식으로 붙을 ScreenSpace-Overlay Canvas의 RectTransform. 비우면 자기 부모 사용")]
    [SerializeField] private RectTransform parentCanvasRect;
    [Tooltip("월드 좌표 변환용. 비우면 Camera.main 사용")]
    [SerializeField] private Camera worldCamera;

    [Header("아이콘 스프라이트")]
    [Tooltip("결제 시 표시할 코인 아이콘")]
    [SerializeField] private Sprite coinIcon;
    [Tooltip("만족도 변동 시 표시할 하트 아이콘")]
    [SerializeField] private Sprite heartIcon;

    [Header("기본 색상")]
    [SerializeField] private Color moneyColor       = new Color(1f, 0.85f, 0.2f);   // 노랑
    [SerializeField] private Color satisfactionGain = new Color(1f, 0.4f, 0.5f);    // 핑크 (하트 톤)
    [SerializeField] private Color neutralColor     = Color.white;

    [Header("기본 수치")]
    [SerializeField] private float defaultDuration = 1.0f;

    private readonly Stack<FloatingText> _pool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (parentCanvasRect == null) parentCanvasRect = (RectTransform)transform;
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ===== Public API =====

    public static void Spawn(Vector3 worldPos, string text, Color color, Sprite icon = null, float duration = -1f)
    {
        if (Instance == null) return;
        Instance.SpawnInternal(worldPos, text, color, icon, duration > 0f ? duration : Instance.defaultDuration);
    }

    public static void SpawnMoney(Vector3 worldPos, long amount)
    {
        if (Instance == null) return;
        string sign = amount >= 0 ? "+" : "";
        Instance.SpawnInternal(worldPos, $"{sign}{amount:N0}", Instance.moneyColor, Instance.coinIcon, Instance.defaultDuration);
    }

    public static void SpawnSatisfaction(Vector3 worldPos, int amount)
    {
        if (Instance == null) return;
        if (amount == 0) return;
        string sign = amount > 0 ? "+" : "";
        Instance.SpawnInternal(worldPos, $"{sign}{amount}", Instance.satisfactionGain, Instance.heartIcon, Instance.defaultDuration);
    }

    // ===== Internal =====

    private void SpawnInternal(Vector3 worldPos, string text, Color color, Sprite icon, float duration)
    {
        if (prefab == null || worldCamera == null) return;

        Vector2 screenPos = worldCamera.WorldToScreenPoint(worldPos);

        FloatingText ft = _pool.Count > 0 ? _pool.Pop() : Instantiate(prefab, parentCanvasRect);
        if (!ft.gameObject.activeSelf) ft.gameObject.SetActive(true);
        ft.OnFinished = ReturnToPool;
        ft.Play(screenPos, text, color, icon, duration);
    }

    private void ReturnToPool(FloatingText ft)
    {
        if (ft == null) return;
        ft.gameObject.SetActive(false);
        _pool.Push(ft);
    }
}
