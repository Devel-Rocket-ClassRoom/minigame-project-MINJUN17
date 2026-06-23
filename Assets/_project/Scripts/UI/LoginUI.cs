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
    [SerializeField] private Button guestButton;

    [Header("표시")]
    [SerializeField] private TMP_Text messageText;

    [Header("패널 전환")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject nicknamePanel;

    [SerializeField] private GameObject authOverlay;      
    [SerializeField] private StartSceneCameraController introController; 

    private void Start()
    {
        loginButton.onClick.AddListener(() => LoginAsync().Forget());
        signUpButton.onClick.AddListener(() => SignUpAsync().Forget());
        if (guestButton != null) guestButton.onClick.AddListener(() => GuestLoginAsync().Forget());
    }

    private async UniTaskVoid LoginAsync()
    {
        SetMessage("로그인 중...");
        var (ok, err) = await AuthManager.Instance.SignInUserWithEmailAsync(
            emailInput.text, passwordInput.text);

        if (!ok) { SetMessage(err); return; }

        var profile = await UserDataService.Instance.LoadProfileAsync();
        if (profile != null && !string.IsNullOrEmpty(profile.nickname))
            CompleteAuth();      
        else
            ShowNicknamePanel();
    }

    private async UniTaskVoid SignUpAsync()
    {
        SetMessage("회원가입 중...");
        var (ok, err) = await AuthManager.Instance.CreateUserWithEmailAsync(
            emailInput.text, passwordInput.text);

        if (!ok) { SetMessage(err); return; }

        ShowNicknamePanel();
    }

    private async UniTaskVoid GuestLoginAsync()
    {
        SetMessage("게스트 로그인 중...");
        var (ok, err) = await AuthManager.Instance.SignInAnonymouslyAsync();

        if (!ok) { SetMessage(err); return; }

        var profile = await UserDataService.Instance.LoadProfileAsync();
        if (profile != null && !string.IsNullOrEmpty(profile.nickname))
            CompleteAuth();
        else
            ShowNicknamePanel();
    }

    private void ShowNicknamePanel()
    {
        loginPanel.SetActive(false);
        nicknamePanel.SetActive(true);
    }

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
