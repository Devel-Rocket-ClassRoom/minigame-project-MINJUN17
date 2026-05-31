using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패널의 활성 상태에 맞춰 모달 막(backdrop)을 자동으로 켜고 끈다.
/// 패널이 어떤 경로로 닫히든(Close() / SetActive(false) / CloseWindow 등)
/// OnDisable에서 막도 같이 꺼지므로 "막만 남는" 문제가 없다.
///
/// 배치: 켜졌다 꺼졌다 하는 "패널 루트"(SetActive 토글 대상)에 붙인다.
/// - backdrop       : 이 패널 전용 풀스크린 막 (다른 버튼 앞 / 패널 뒤에 배치)
/// - backdropButton : 막 클릭 시 패널 닫기 (보통 backdrop에 붙인 Button)
/// </summary>
public class ModalBackdrop : MonoBehaviour
{
    [SerializeField] private GameObject backdrop;
    [SerializeField] private Button backdropButton;   // 옵션: 막 클릭 → 패널 닫기

    private void Awake()
    {
        if (backdropButton != null) backdropButton.onClick.AddListener(CloseSelf);
    }

    private void OnEnable()  { if (backdrop != null) backdrop.SetActive(true); }
    private void OnDisable() { if (backdrop != null) backdrop.SetActive(false); }

    private void CloseSelf() => gameObject.SetActive(false);
}
