using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// "새로운 손님 등장!" 팝업. 새로 해금된 손님이 처음 방문하면(CustomerManager.OnNewCustomerIntroduced)
/// 손님 아이콘 + 이름을 보여준다. 확인 버튼으로 닫고, 대기 중인 다음 손님이 있으면 이어서 표시.
///
/// 배치: 이 스크립트는 "항상 켜져 있는" 오브젝트(예: UI 캔버스 루트)에 두고,
///       실제 팝업 비주얼(PopupPanel)은 자식으로 두어 토글한다.
///       (비활성 오브젝트는 Start가 안 돌아 이벤트 구독이 안 되므로)
/// </summary>
public class NewCustomerPopup : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PopupPanel popup;            // 열기/닫기 애니메이션
    [SerializeField] private Image iconImage;             // 손님 아이콘
    [SerializeField] private TextMeshProUGUI nameText;    // 손님 이름
    [SerializeField] private Button confirmButton;        // 확인 버튼

    private readonly Queue<CustomerData> _queue = new();
    private CustomerData _current;
    private LocalizedString _boundName;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
    }

    private void Start()
    {
        // CustomerManager는 게임씬 Awake에서 Instance 세팅됨 → Start 시점엔 준비 완료.
        if (CustomerManager.Instance != null)
            CustomerManager.Instance.OnNewCustomerIntroduced += Enqueue;
    }

    private void OnDestroy()
    {
        if (CustomerManager.Instance != null)
            CustomerManager.Instance.OnNewCustomerIntroduced -= Enqueue;
        UnbindName();
    }

    private void Enqueue(CustomerData data)
    {
        if (data == null) return;
        _queue.Enqueue(data);
        if (_current == null) ShowNext();   // 표시 중이 아니면 즉시 시작
    }

    private void ShowNext()
    {
        if (_queue.Count == 0) { _current = null; popup?.Close(); return; }

        _current = _queue.Dequeue();

        if (iconImage != null)
        {
            iconImage.sprite  = _current.icon;
            iconImage.enabled = _current.icon != null;
        }

        BindName(_current);

        popup?.Open();

        SoundManager.Get()?.PlaySfx(SfxId.CustomerPopup);
    }

    private void OnConfirm()
    {
        // 다음 대기 손님이 있으면 내용 교체하며 이어서 표시, 없으면 닫힘
        ShowNext();
    }

    // ─── LocalizedString 이름 바인딩 (로케일 변경 시 자동 갱신) ───
    private void BindName(CustomerData data)
    {
        UnbindName();
        if (nameText == null) return;

        if (data.customerName != null && !data.customerName.IsEmpty)
        {
            _boundName = data.customerName;
            _boundName.StringChanged += OnNameChanged;
            _boundName.RefreshString();
        }
        else
        {
            nameText.text = data.name;   // 폴백: 에셋 이름
        }
    }

    private void OnNameChanged(string s)
    {
        if (nameText != null) nameText.text = s;
    }

    private void UnbindName()
    {
        if (_boundName != null) _boundName.StringChanged -= OnNameChanged;
        _boundName = null;
    }
}
