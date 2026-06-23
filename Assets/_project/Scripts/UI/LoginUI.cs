using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("버튼")] 
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signUpButton;

    [Header("표시")]
    [SerializeField] private TMP_Text messageText;

    [Header("패널 전환")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject nicknamePanel;

    [SerializeField] private GameObject authOverlay;                    // 로그인+닉네임+딤 묶은 부모
    [SerializeField] private StartSceneCameraController introController; // 인트로 연출 컨트롤러

    private void Start()
    {
        loginButton.onClick.AddListener(() => LoginAsync().Forget());
        signUpButton.onClick.AddListener(() => SignUpAsync().Forget());
    }

    private async UniTaskVoid LoginAsync()
    {
        SetMessage("로그인 중...");
        var (ok, err) = await AuthManager.Instance.SignInUserWithEmailAsync(
            emailInput.text, passwordInput.text);

        if (!ok) { SetMessage(err); return; }

        // 로그인 성공 → 닉네임이 이미 있나 확인
        var profile = await UserDataService.Instance.LoadProfileAsync();
        if (profile != null && !string.IsNullOrEmpty(profile.nickname))
            CompleteAuth();             // 닉네임 있음 → 오버레이 닫고 인트로 시작
        else
            ShowNicknamePanel();        // 닉네임 없음 → 닉네임 입력
    }

    private async UniTaskVoid SignUpAsync()
    {
        SetMessage("회원가입 중...");
        var (ok, err) = await AuthManager.Instance.CreateUserWithEmailAsync(
            emailInput.text, passwordInput.text);

        if (!ok) { SetMessage(err); return; }

        // 회원가입 성공 → 무조건 닉네임 입력으로
        ShowNicknamePanel();
    }

    private void ShowNicknamePanel()
    {
        loginPanel.SetActive(false);
        nicknamePanel.SetActive(true);
    }

    /// <summary>인증 완료 → 로그인 오버레이를 닫고 시작씬 인트로 연출을 시작한다.</summary>
    private void CompleteAuth()
    {
        if (authOverlay != null) authOverlay.SetActive(false);
        if (introController != null) introController.BeginIntro();
    }

    private void SetMessage(string msg)
    {
        if (messageText != null) messageText.text = msg;
        Debug.Log($"[LoginUI] {msg}");
    }

}
