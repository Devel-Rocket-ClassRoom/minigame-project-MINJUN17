using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text messageText;

    [Header("완료 처리")]
    [SerializeField] private GameObject authOverlay;                    // 로그인+닉네임+딤 묶은 부모
    [SerializeField] private StartSceneCameraController introController; // 인트로 연출 컨트롤러

    private void Start()
    {
        confirmButton.onClick.AddListener(() => ConfirmAsync().Forget());
    }

    private async UniTaskVoid ConfirmAsync()
    {
        string nick = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nick))
        {
            SetMessage("닉네임을 입력하세요.");
            return;
        }

        SetMessage("저장 중...");
        bool ok = await UserDataService.Instance.SaveNicknameAsync(nick);

        if (ok)
        {
            // 저장 성공 → 오버레이 닫고 시작씬 인트로 시작
            if (authOverlay != null) authOverlay.SetActive(false);
            if (introController != null) introController.BeginIntro();
        }
        else SetMessage("저장 실패, 다시 시도하세요.");
    }

    private void SetMessage(string msg)
    {
        if (messageText != null) messageText.text = msg;
    }
}