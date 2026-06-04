using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패널의 활성 상태에 맞춰 모달 막(backdrop)을 자동으로 켜고 끈다.
/// 패널이 어떤 경로로 닫히든(Close() / SetActive(false) / CloseWindow 등)
/// OnDisable에서 막도 같이 꺼지므로 "막만 남는" 문제가 없다.
///
/// 배치: 켜졌다 꺼졌다 하는 "패널 루트"(SetActive 토글 대상)에 붙이는 게 원칙이다.
/// - backdrop       : 이 패널 전용 풀스크린 막 (다른 버튼 앞 / 패널 뒤에 배치)
/// - backdropButton : 막 클릭 시 패널 닫기 (보통 backdrop에 붙인 Button)
/// - closeTarget    : 막 클릭으로 끌 대상(옵션). 비우면 조상 PopupPanel(창 루트)을, 그것도 없으면 자기 자신을 끈다.
/// </summary>
public class ModalBackdrop : MonoBehaviour
{
    [SerializeField] private GameObject backdrop;
    [SerializeField] private Button backdropButton;   // 옵션: 막 클릭 → 패널 닫기

    [Tooltip("막 클릭으로 닫을 때 SetActive(false)할 대상(옵션). 비우면: 조상에 PopupPanel이 있으면 그 창 루트를, 없으면 자기 자신을 끔.")]
    [SerializeField] private GameObject closeTarget;

    private void Awake()
    {
        if (backdropButton != null) backdropButton.onClick.AddListener(CloseSelf);
    }

    private void OnEnable()  { if (backdrop != null) backdrop.SetActive(true); }
    private void OnDisable() { if (backdrop != null) backdrop.SetActive(false); }

    private void CloseSelf() => ResolveCloseTarget().SetActive(false);

    /// <summary>
    /// 막 클릭 시 끌 대상 결정. 명시 지정(closeTarget)이 최우선.
    /// 없으면, 이 컴포넌트가 '여닫히는 창 루트'가 아니라 그 자식(예: ScrollView Content)에
    /// 붙어 있는 경우를 대비해 조상의 PopupPanel(실제 토글 루트)을 찾아 끈다.
    /// 그래야 다음에 부모 창을 다시 열 때 이 자식의 OnEnable이 정상 발화한다.
    /// (자식만 SetActive(false)로 끄면 부모는 계속 활성이라, 재오픈 시 자식이 비활성으로 남아
    ///  OnEnable/Refresh가 안 돌고 상점 슬롯이 비어 보이던 버그 방지.)
    /// </summary>
    private GameObject ResolveCloseTarget()
    {
        if (closeTarget != null) return closeTarget;
        var popup = GetComponentInParent<PopupPanel>(true);
        return popup != null ? popup.gameObject : gameObject;
    }
}
