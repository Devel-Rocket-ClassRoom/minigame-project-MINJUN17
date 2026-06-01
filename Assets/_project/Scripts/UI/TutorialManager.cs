using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선형 가이드 튜토리얼. steps 리스트를 0번부터 순서대로 진행.
/// 각 스텝 = 대사 + 하이라이트 타겟 + 진행 방식(탭 / 버튼클릭 / 게임이벤트).
/// - 신규게임(세이브 없음)일 때만 활성 (외부가 IsActive 참조해 시간정지/starter직원 스킵).
/// - 시작 시 만족도 지급(해금/모집 비용) → 끝나면 0.
/// - 마지막 스텝 후 Finish(): 저장 + 영업 시작.
/// </summary>
[DefaultExecutionOrder(-40)]
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }
    public static bool IsActive { get; private set; }

    public enum Advance { Tap, ClickTarget, GameEvent }
    public enum GameEventKind { None, Recruited, Hired, PlacementStarted, SeatsPlaced, GrillUnlocked, GrillPlaced, EggUnlocked }
    public enum EnterAction { None, SeedCandidates }

    [System.Serializable]
    public class TutorialStep
    {
        [TextArea] public string line;
        [Tooltip("하이라이트(마스크 구멍)+화살표 대상. 탭 단계는 비워도 됨")]
        public RectTransform target;
        public Advance advance = Advance.ClickTarget;
        [Tooltip("advance=GameEvent일 때 어떤 결과로 넘어갈지")]
        public GameEventKind eventKind = GameEventKind.None;
        [Tooltip("Hired=목표 인원수, SeatsPlaced=누적 좌석수")]
        public int eventCount = 1;
        [Tooltip("이 스텝 진입 시 부수효과")]
        public EnterAction onEnter = EnterAction.None;
        [Tooltip("이 스텝 동안 대화창을 아래로 내림 (주방 가림 방지)")]
        public bool lowerDialogue;
    }

    [Header("참조")]
    [SerializeField] private TutorialMask mask;
    [SerializeField] private DayCycleController dayCycle;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private DialogueBox dialogueBox;

    [Header("화살표 (타겟 가리킴)")]
    [SerializeField] private RectTransform arrow;
    [SerializeField] private Vector2 arrowOffset = new Vector2(0f, 60f);

    [Header("대화창 내림 (그릴 설치 등 주방 가릴 때)")]
    [SerializeField] private RectTransform dialogueBoxRect;     // 움직일 대화창 박스 (비우면 dialogueBox 루트 자동 사용)
    [SerializeField] private RectTransform dialogueLoweredAnchor; // 내렸을 때 이동할 위치(빈 RectTransform을 원하는 곳에 배치)
    private Vector3 _dialogueNormalPos;                         // 원위치(월드 좌표)
    private bool _dialoguePosCaptured;

    [Header("창 닫기 버튼 (선택 — 각 창의 닫기/취소 버튼)")]
    [SerializeField] private Button closeRecruitWindowButton;   // 모집 후 해금창 닫기
    [SerializeField] private Button closeHireWindowButton;      // 2명 고용 후 고용창 닫기
    [SerializeField] private Button placementCancelButton;      // 설치모드 취소 버튼 (설치 단계 중 잠금)

    [Header("완료 판정용 데이터")]
    [SerializeField] private FurnitureData seatData;
    [SerializeField] private FurnitureData grillData;
    [SerializeField] private MenuData      eggData;

    [Header("설정")]
    [SerializeField] private int satisfactionGrant = 100000;
    [Tooltip("튜토리얼 모집 단계에서 이 등급만 노출 (보통 일반)")]
    [SerializeField] private RecruitmentTier tutorialRecruitTier;

    [Header("단계 (대사는 채워둠 — 인스펙터에서 target만 연결)")]
    [SerializeField] private List<TutorialStep> steps = new()
    {
        new() { line = "먼저 직원부터 고용해볼게요", advance = Advance.Tap },
        new() { line = "메뉴 버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "고용 버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "이런 아직 지원자가 없어요. 모집하러가볼까요?", advance = Advance.Tap },
        new() { line = "일반 모집 버튼을 눌러보세요", advance = Advance.GameEvent, eventKind = GameEventKind.Recruited },
        new() { line = "다시 메뉴 버튼을 눌러보세요", advance = Advance.ClickTarget, onEnter = EnterAction.SeedCandidates },
        new() { line = "고용 버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "오! 지원자가 두명이나 생겼네요 원래는 1달후에 생긴답니다. 요리사와 서버를 각각 고용해보세요", advance = Advance.GameEvent, eventKind = GameEventKind.Hired, eventCount = 2 },
        new() { line = "다음은 좌석을 설치해봅시다 메뉴버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "설치버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "좌석을 누르고 체크버튼을 눌러 좌석을 설치해보세요", advance = Advance.GameEvent, eventKind = GameEventKind.SeatsPlaced, eventCount = 1 },
        new() { line = "2개 더 설치해보세요", advance = Advance.GameEvent, eventKind = GameEventKind.SeatsPlaced, eventCount = 3 },
        new() { line = "다음은 조리도구를 설치해볼거에요 메뉴버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "해금버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "상단에 가구버튼을 눌러 그릴을 해금해보세요", advance = Advance.GameEvent, eventKind = GameEventKind.GrillUnlocked },
        new() { line = "그릴이 해금되었어요 설치를 해야 사용 가능해요", advance = Advance.Tap },
        new() { line = "메뉴 버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "설치버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "상단에 주방 버튼을 눌러 그릴을 주방구역에 설치해보세요", advance = Advance.GameEvent, eventKind = GameEventKind.GrillPlaced },
        new() { line = "다음은 요리를 개발해볼거에요 메뉴버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "해금 버튼을 눌러보세요", advance = Advance.ClickTarget },
        new() { line = "메뉴 버튼을 눌러 계란후라이를 해금해보세요", advance = Advance.GameEvent, eventKind = GameEventKind.EggUnlocked },
        new() { line = "음식을 해금하면 새로운 손님이 등장할 수도 있답니다", advance = Advance.Tap },
    };

    private int _i = -1;
    private int _seatsPlaced;
    private Button _hookedButton;

    private TutorialStep Cur => (_i >= 0 && _i < steps.Count) ? steps[_i] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        bool hasSave = SaveLoadManager.Instance != null && SaveLoadManager.Instance.HasActiveSave;
        IsActive = !hasSave;
    }

    private void Start()
    {
        if (!IsActive)
        {
            if (mask != null) mask.Hide();
            if (dialogueBox != null) dialogueBox.Hide();
            if (arrow != null) arrow.gameObject.SetActive(false);
            enabled = false;   // ⚠ 이 컴포넌트만 끈다. gameObject는 TimeSystem/DayCycleController와 공유 → SetActive(false) 금지
            return;
        }

        if (dialogueBoxRect == null && dialogueBox != null)
            dialogueBoxRect = dialogueBox.RootRect;   // 미연결 시 대화창 루트 자동 사용

        SatisfactionSystem.Instance?.SetSatisfaction(satisfactionGrant);
        UnlockShopPanel.TutorialOnlyTier = tutorialRecruitTier;   // 모집은 일반 등급만 노출
        UnlockShopPanel.TutorialOnlyFurniture = grillData;        // 가구 해금은 그릴만 노출
        UnlockShopPanel.TutorialOnlyMenu = eggData;               // 음식 해금은 계란후라이만 노출

        if (StaffManager.Instance != null) StaffManager.Instance.OnRosterChanged += OnRosterChanged;
        if (StaffCandidatePool.Instance != null) StaffCandidatePool.Instance.OnPendingTicketsChanged += OnRecruited;
        if (CatalogManager.Instance != null)
        {
            CatalogManager.Instance.OnFurnitureUnlocked += OnFurnitureUnlocked;
            CatalogManager.Instance.OnMenuUnlocked += OnMenuUnlocked;
        }
        if (placementSystem != null)
        {
            placementSystem.OnFurniturePlaced += OnFurniturePlaced;
            placementSystem.OnEnterPlaceMode += OnEnterPlaceMode;
        }

        EnterStep(0);
    }

    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        UnhookClickTarget();
        if (StaffManager.Instance != null) StaffManager.Instance.OnRosterChanged -= OnRosterChanged;
        if (StaffCandidatePool.Instance != null) StaffCandidatePool.Instance.OnPendingTicketsChanged -= OnRecruited;
        if (CatalogManager.Instance != null)
        {
            CatalogManager.Instance.OnFurnitureUnlocked -= OnFurnitureUnlocked;
            CatalogManager.Instance.OnMenuUnlocked -= OnMenuUnlocked;
        }
        if (placementSystem != null)
        {
            placementSystem.OnFurniturePlaced -= OnFurniturePlaced;
            placementSystem.OnEnterPlaceMode -= OnEnterPlaceMode;
        }
    }

    // ─────────────────────────────────────── 진행
    private void EnterStep(int i)
    {
        UnhookClickTarget();
        _i = i;

        if (i >= steps.Count) { Finish(); return; }
        var step = steps[i];

        // 해금탭 안에서 진행하는 단계(모집/그릴해금/계란해금) 동안엔 닫기 버튼 잠금
        if (closeRecruitWindowButton != null)
        {
            bool unlockWindowStep = step.advance == Advance.GameEvent &&
                (step.eventKind == GameEventKind.Recruited
                 || step.eventKind == GameEventKind.GrillUnlocked
                 || step.eventKind == GameEventKind.EggUnlocked);
            closeRecruitWindowButton.interactable = !unlockWindowStep;
        }

        // 설치 관련 단계 동안엔 설치모드 취소 버튼 잠금
        if (placementCancelButton != null)
        {
            bool placeStep = step.advance == Advance.GameEvent &&
                (step.eventKind == GameEventKind.PlacementStarted
                 || step.eventKind == GameEventKind.SeatsPlaced
                 || step.eventKind == GameEventKind.GrillPlaced);
            placementCancelButton.interactable = !placeStep;
        }

        // 대화창 위치 (주방 가리는 단계만 지정 앵커 위치로 내림 — 월드 좌표라 부모/앵커 무관)
        if (dialogueBoxRect != null)
        {
            bool lower = step.lowerDialogue && dialogueLoweredAnchor != null;
            if (lower)
            {
                // 첫 내림 직전, 현재(원래) 위치를 캡처 — 이 시점엔 박스가 이미 표시돼 레이아웃이 잡혀있음
                if (!_dialoguePosCaptured) { _dialogueNormalPos = dialogueBoxRect.position; _dialoguePosCaptured = true; }
                dialogueBoxRect.position = dialogueLoweredAnchor.position;
            }
            else if (_dialoguePosCaptured)
            {
                dialogueBoxRect.position = _dialogueNormalPos;   // 원위치 복귀
            }
        }

        RunEnterAction(step.onEnter);

        bool isTap = step.advance == Advance.Tap;

        // 마스크: 탭(나레이션)이면 전체 가림, 아니면 타겟 구역만
        if (mask != null)
        {
            if (!isTap && step.target != null) mask.HighlightUI(step.target);
            else                               mask.CoverAll();
        }

        // 화살표: 행동 단계 타겟에만
        PointArrowAt(isTap ? null : step.target);

        // 대사: 탭은 Play(탭으로 진행), 그 외는 ShowStatic(탭 통과 → 타겟/이벤트로 진행)
        if (dialogueBox != null)
        {
            if (isTap) dialogueBox.Play(new List<string> { step.line }, AdvanceNext);
            else       dialogueBox.ShowStatic(step.line);
        }

        // 버튼클릭 단계: 그 버튼 누르면 진행 (버튼 없으면 안전하게 탭으로 폴백)
        if (step.advance == Advance.ClickTarget)
        {
            var btn = FindButton(step.target);
            if (btn != null) HookClickTarget(btn);
            else if (dialogueBox != null) dialogueBox.Play(new List<string> { step.line }, AdvanceNext);
        }
    }

    private void AdvanceNext()
    {
        UnhookClickTarget();
        EnterStep(_i + 1);
    }

    private void RunEnterAction(EnterAction a)
    {
        if (a == EnterAction.SeedCandidates)
        {
            var pool = StaffCandidatePool.Instance;
            if (pool != null)
            {
                pool.SeedApplicant(StaffRole.Cook, StaffType.Junior);
                pool.SeedApplicant(StaffRole.Server, StaffType.Junior);
                pool.CancelAllPendingTickets();
            }
        }
    }

    private void PointArrowAt(RectTransform target)
    {
        if (arrow == null) return;
        if (target == null) { arrow.gameObject.SetActive(false); return; }
        arrow.gameObject.SetActive(true);
        arrow.position = target.position + (Vector3)arrowOffset;
    }

    private static Button FindButton(RectTransform target)
    {
        if (target == null) return null;
        return target.GetComponent<Button>() ?? target.GetComponentInChildren<Button>();
    }

    private void HookClickTarget(Button btn)
    {
        _hookedButton = btn;
        btn.onClick.AddListener(AdvanceNext);
    }

    private void UnhookClickTarget()
    {
        if (_hookedButton != null) { _hookedButton.onClick.RemoveListener(AdvanceNext); _hookedButton = null; }
    }

    // ─────────────────────────────────────── 게임 이벤트 완료 감지
    private void OnRecruited()
    {
        var cur = Cur;
        if (cur != null && cur.advance == Advance.GameEvent && cur.eventKind == GameEventKind.Recruited)
        {
            if (closeRecruitWindowButton != null) closeRecruitWindowButton.onClick.Invoke();  // 해금창 닫기(내려감)
            AdvanceNext();
        }
    }

    private void OnRosterChanged()
    {
        var cur = Cur;
        if (cur != null && cur.advance == Advance.GameEvent && cur.eventKind == GameEventKind.Hired
            && StaffManager.Instance != null && StaffManager.Instance.TotalStaffCount >= cur.eventCount)
        {
            if (closeHireWindowButton != null) closeHireWindowButton.onClick.Invoke();   // 고용창 닫기
            StaffManager.Instance.OnBusinessOpen();   // 고용한 직원 즉시 출근
            AdvanceNext();
        }
    }

    private void OnEnterPlaceMode()
    {
        var cur = Cur;
        if (cur != null && cur.advance == Advance.GameEvent && cur.eventKind == GameEventKind.PlacementStarted)
            AdvanceNext();
    }

    private void OnFurniturePlaced(FurnitureData d)
    {
        var cur = Cur;
        if (cur == null || cur.advance != Advance.GameEvent) return;

        if (cur.eventKind == GameEventKind.SeatsPlaced && d == seatData)
        {
            _seatsPlaced++;
            if (_seatsPlaced >= cur.eventCount) AdvanceNext();
        }
        else if (cur.eventKind == GameEventKind.GrillPlaced && d == grillData)
        {
            AdvanceNext();
        }
    }

    private void OnFurnitureUnlocked(FurnitureData f)
    {
        var cur = Cur;
        if (cur != null && cur.advance == Advance.GameEvent && cur.eventKind == GameEventKind.GrillUnlocked && f == grillData)
        {
            if (closeRecruitWindowButton != null) closeRecruitWindowButton.onClick.Invoke();  // 해금탭 닫기
            AdvanceNext();
        }
    }

    private void OnMenuUnlocked(MenuData m)
    {
        var cur = Cur;
        if (cur != null && cur.advance == Advance.GameEvent && cur.eventKind == GameEventKind.EggUnlocked && m == eggData)
        {
            if (closeRecruitWindowButton != null) closeRecruitWindowButton.onClick.Invoke();  // 해금탭 닫기
            AdvanceNext();
        }
    }

    // ─────────────────────────────────────── 완료
    [ContextMenu("강제 완료 (테스트)")]
    public void Finish()
    {
        Unsubscribe();
        IsActive = false;
        UnlockShopPanel.TutorialOnlyTier = null;        // 모집 등급 필터 해제
        UnlockShopPanel.TutorialOnlyFurniture = null;   // 가구 필터 해제
        UnlockShopPanel.TutorialOnlyMenu = null;        // 음식 필터 해제
        if (closeRecruitWindowButton != null) closeRecruitWindowButton.interactable = true;   // 닫기 잠금 해제
        if (placementCancelButton != null) placementCancelButton.interactable = true;         // 취소 잠금 해제

        if (mask != null) mask.Hide();
        if (dialogueBox != null) dialogueBox.Hide();
        if (arrow != null) arrow.gameObject.SetActive(false);

        SatisfactionSystem.Instance?.SetSatisfaction(0);   // 지급분 회수
        if (dayCycle != null) dayCycle.StartBusiness();     // 영업 시작 (첫 손님 등장)
        SaveLoadManager.Instance?.Save();                   // 다음 실행부턴 튜토리얼 스킵

        enabled = false;   // ⚠ 이 컴포넌트만 끈다. gameObject는 TimeSystem/DayCycleController와 공유 → SetActive(false) 하면 시간/손님이 멈춤
    }
}
