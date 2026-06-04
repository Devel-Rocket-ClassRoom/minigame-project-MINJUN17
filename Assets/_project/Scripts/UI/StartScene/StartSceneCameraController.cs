using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// 시작씬 인트로 연출 + 메인 메뉴 컨트롤러 (단일 슬롯 모드).
/// 흐름:
///   1) 도시 위에서 vcamStart → vcamEnd 로 카메라가 부드럽게 내려옴 (Cinemachine Priority 스왑)
///   2) 로고 페이드인 + 살짝 오버슈트 스케일
///   3) 새 게임 / (세이브 있을 때만) 이어하기 / 설정 버튼이 차례로 페이드인
///   4) 인터랙션 활성화
/// 버튼 동작:
///   - 새 게임: 세이브 있으면 경고창 → 확인 시 삭제 후 새 게임. 없으면 바로 시작
///   - 이어하기: 슬롯 0 로드하고 게임씬으로
///   - 설정: TODO
/// 기타:
///   - 인트로 도중 화면 클릭 시 즉시 최종 상태로 스킵
///   - 단일 슬롯이지만 SaveLoadManager의 3슬롯 구조 그대로 활용 (슬롯 0만 사용)
/// </summary>
public class StartSceneCameraController : MonoBehaviour
{
    private const int Slot = 0;   // 단일 슬롯 모드: 슬롯 0만 사용

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera vcamStart;
    [SerializeField] private CinemachineCamera vcamEnd;
    [SerializeField] private int activePriority   = 20;
    [SerializeField] private int inactivePriority = 5;

    [Header("UI 요소 (CanvasGroup)")]
    [SerializeField] private CanvasGroup logo;
    [SerializeField] private CanvasGroup newGameButton;
    [SerializeField] private CanvasGroup continueButton;
    [SerializeField] private CanvasGroup settingsButton;

    [Header("타이밍")]
    [Tooltip("씬 진입 후 카메라가 움직이기 전 대기 시간")]
    [SerializeField] private float cameraDelay      = 0.3f;
    [Tooltip("카메라 블렌드 지속 시간 (CinemachineBrain Default Blend 시간과 동일해야 함)")]
    [SerializeField] private float cameraDuration   = 3f;
    [SerializeField] private float logoDuration     = 0.5f;
    [SerializeField] private float buttonDuration   = 0.35f;
    [SerializeField] private float buttonStagger    = 0.15f;

    [Header("스킵 설정")]
    [Tooltip("씬 진입 직후 이 시간 내에는 스킵 입력을 무시 (오클릭 방지)")]
    [SerializeField] private float skipGuardTime = 0.3f;

    [Header("새 게임 경고창")]
    [Tooltip("기존 세이브가 있을 때 새 게임 누르면 띄울 경고 패널 (초기엔 비활성).")]
    [SerializeField] private GameObject newGameConfirmDialog;
    [Tooltip("경고창의 '예' 버튼 — 누르면 세이브 삭제 후 새 게임 시작.")]
    [SerializeField] private Button confirmYesButton;
    [Tooltip("경고창의 '아니오' 버튼 — 누르면 경고창만 닫음.")]
    [SerializeField] private Button confirmNoButton;

    [Header("씬 전환")]
    [Tooltip("새 게임 / 이어하기 시 로드할 게임씬 이름. Build Settings에 등록 필요.")]
    [SerializeField] private string gameSceneName = "GameScene";

    private bool isSequenceRunning;
    private bool isSkipped;
    private float sequenceStartTime;

    // ──────────────────────────────────────────────
    private void Start()
    {
        InitializeState();
        StartCoroutine(PlayIntro());
    }

    private void Update()
    {
        // 스킵 입력 감지 — 단, 경고창이 떠있으면 스킵 입력으로 안 봄
        if (!isSequenceRunning || isSkipped) return;
        if (Time.time - sequenceStartTime < skipGuardTime) return;
        if (IsConfirmOpen()) return;

        bool pressed =
            Input.GetMouseButtonDown(0) ||
            Input.touchCount > 0 ||
            Input.anyKeyDown;

        if (pressed) SkipIntro();
    }

    // ──────────────────────────────────────────────
    private void InitializeState()
    {
        // Cinemachine 초기 상태: 시작 vcam이 활성
        if (vcamStart != null) vcamStart.Priority = activePriority;
        if (vcamEnd   != null) vcamEnd.Priority   = inactivePriority;

        // UI 다 숨김
        SetAlpha(logo, 0f);
        SetAlpha(newGameButton, 0f);
        SetAlpha(continueButton, 0f);
        SetAlpha(settingsButton, 0f);

        SetInteractable(false);

        // 로고는 살짝 작게 시작 (등장 시 오버슈트용)
        if (logo != null) logo.transform.localScale = Vector3.zero;

        // 세이브가 없으면 이어하기 버튼 자체를 숨김
        bool hasSave = SaveLoadManager.HasSave(Slot);
        if (continueButton != null)
            continueButton.gameObject.SetActive(hasSave);

        // 경고창 초기 비활성 + 버튼 이벤트 연결
        if (newGameConfirmDialog != null) newGameConfirmDialog.SetActive(false);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton  != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
    }

    // ──────────────────────────────────────────────
    private IEnumerator PlayIntro()
    {
        isSequenceRunning = true;
        sequenceStartTime = Time.time;

        // 1) 카메라 이동 시작 전 대기
        yield return WaitOrSkip(cameraDelay);
        if (isSkipped) yield break;

        // 2) Cinemachine Priority 스왑 → CinemachineBrain이 자동 블렌드
        if (vcamStart != null) vcamStart.Priority = inactivePriority;
        if (vcamEnd   != null) vcamEnd.Priority   = activePriority;

        // 3) 카메라 블렌드 끝까지 대기
        yield return WaitOrSkip(cameraDuration);
        if (isSkipped) yield break;

        // 4) 로고 등장 (페이드 + 오버슈트 스케일)
        yield return StartCoroutine(LogoAppear());
        if (isSkipped) yield break;

        // 5) 버튼 차례로 등장
        StartCoroutine(FadeIn(newGameButton, buttonDuration));
        yield return WaitOrSkip(buttonStagger);
        if (isSkipped) yield break;

        if (continueButton != null && continueButton.gameObject.activeSelf)
        {
            StartCoroutine(FadeIn(continueButton, buttonDuration));
            yield return WaitOrSkip(buttonStagger);
            if (isSkipped) yield break;
        }

        StartCoroutine(FadeIn(settingsButton, buttonDuration));
        yield return WaitOrSkip(buttonDuration);

        // 6) 완료 → 인터랙션 활성화
        FinishSequence();
    }

    // ──────────────────────────────────────────────
    private IEnumerator LogoAppear()
    {
        if (logo == null) yield break;

        const float overshoot = 1.1f;
        const float settleDuration = 0.15f;

        // 페이드인 + scale 0 → overshoot
        float t = 0f;
        while (t < logoDuration)
        {
            if (isSkipped) yield break;
            t += Time.deltaTime;
            float r = Mathf.Clamp01(t / logoDuration);
            logo.alpha = r;
            float s = Mathf.Lerp(0f, overshoot, EaseOutBack(r));
            logo.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        // 정착: overshoot → 1
        t = 0f;
        while (t < settleDuration)
        {
            if (isSkipped) yield break;
            t += Time.deltaTime;
            float r = Mathf.Clamp01(t / settleDuration);
            float s = Mathf.Lerp(overshoot, 1f, r);
            logo.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        logo.transform.localScale = Vector3.one;
    }

    private IEnumerator FadeIn(CanvasGroup group, float duration)
    {
        if (group == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            if (isSkipped) yield break;
            t += Time.deltaTime;
            group.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        group.alpha = 1f;
    }

    /// <summary>WaitForSeconds 대신, 스킵이 들어오면 즉시 빠져나옴.</summary>
    private IEnumerator WaitOrSkip(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (isSkipped) yield break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    // ──────────────────────────────────────────────
    private void SkipIntro()
    {
        if (isSkipped) return;
        isSkipped = true;

        StopAllCoroutines();

        // 카메라: 최종 vcam 활성
        if (vcamStart != null) vcamStart.Priority = inactivePriority;
        if (vcamEnd   != null) vcamEnd.Priority   = activePriority;
        // 즉시 스냅: CinemachineBrain의 ActiveBlend를 끊음
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null && brain.ActiveBlend != null)
            brain.ActiveBlend.Duration = 0f;

        // UI 최종 상태
        SetAlpha(logo, 1f);
        if (logo != null) logo.transform.localScale = Vector3.one;
        SetAlpha(newGameButton, 1f);
        if (continueButton != null && continueButton.gameObject.activeSelf)
            SetAlpha(continueButton, 1f);
        SetAlpha(settingsButton, 1f);

        FinishSequence();
    }

    private void FinishSequence()
    {
        isSequenceRunning = false;
        SetInteractable(true);
    }

    // ──────────────────────────────────────────────
    private void SetAlpha(CanvasGroup g, float a)
    {
        if (g != null) g.alpha = a;
    }

    private void SetInteractable(bool on)
    {
        SetInteractable(newGameButton, on);
        SetInteractable(continueButton, on);
        SetInteractable(settingsButton, on);
    }

    private void SetInteractable(CanvasGroup g, bool on)
    {
        if (g == null) return;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ──────────────────────────────────────────────
    // 버튼 클릭 핸들러 (Inspector OnClick에 연결)

    public void OnClick_NewGame()
    {
        // 인트로 도중이면 무시
        if (isSequenceRunning) return;

        if (SaveLoadManager.HasSave(Slot))
        {
            // 기존 세이브 있음 → 경고창 표시
            ShowNewGameConfirm();
        }
        else
        {
            // 깨끗한 상태 → 바로 시작
            StartNewGameAndLoadScene();
        }
    }

    public void OnClick_Continue()
    {
        if (isSequenceRunning) return;
        if (!SaveLoadManager.HasSave(Slot)) return; // 안전장치

        SaveLoadManager.ContinueGame(Slot);
        LoadGameScene();
    }

    public void OnClick_Settings()
    {
        if (isSequenceRunning) return;
        // TODO: 설정 팝업 열기
    }

    // ──────────────────────────────────────────────
    // 새 게임 경고창

    private void ShowNewGameConfirm()
    {
        if (newGameConfirmDialog == null)
        {
            // 경고창이 없으면 안전 fallback — 그냥 진행하지 말고 경고만
            Debug.LogWarning("[StartScene] newGameConfirmDialog 미연결 — 안전을 위해 새 게임 시작 중단.");
            return;
        }
        newGameConfirmDialog.SetActive(true);
    }

    private void HideNewGameConfirm()
    {
        if (newGameConfirmDialog != null) newGameConfirmDialog.SetActive(false);
    }

    private bool IsConfirmOpen()
    {
        return newGameConfirmDialog != null && newGameConfirmDialog.activeSelf;
    }

    private void OnConfirmYes()
    {
        HideNewGameConfirm();
        StartNewGameAndLoadScene();
    }

    private void OnConfirmNo()
    {
        HideNewGameConfirm();
    }

    // ──────────────────────────────────────────────
    // 씬 전환

    private void StartNewGameAndLoadScene()
    {
        SaveLoadManager.StartNewGame(Slot);   // 슬롯 0 세이브 삭제 + ActiveSlot 설정
        LoadGameScene();
    }

    private void LoadGameScene()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[StartScene] gameSceneName이 비어있음. Inspector에서 설정 필요.");
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }
}
