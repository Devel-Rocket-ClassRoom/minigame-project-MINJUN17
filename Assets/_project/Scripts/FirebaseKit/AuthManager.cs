using System;
using Cysharp.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    private static AuthManager instance;
    public static AuthManager Instance => instance;

    private FirebaseAuth auth;
    private FirebaseUser currentUser;

    private bool isInitialized = false;
    private bool lastNotifiedSignedIn = false;

    public FirebaseUser CurrentUser => currentUser;

    public bool IsLoggedIn => currentUser != null;
    public string UserId => currentUser?.UserId ?? string.Empty;
    public bool IsInitialized => isInitialized;

    public event Action<bool> LoginStateChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private async UniTaskVoid Start()
    {
        bool isReady = await FirebaseManager.Instance.WaitForInitializationAsync();

        if (!isReady)
        {
            Debug.LogError("[Auth] 파이어 베이스 초기화 실패 Auth 초기화 불가..");
            return;
        }

        auth = FirebaseManager.Instance.Auth;
        auth.StateChanged += OnAuthStateChanged;

        currentUser = auth.CurrentUser;
        Debug.Log(currentUser != null ? "[Auth] 이미 로그인 됨" : "[Auth] 로그인 필요");

        isInitialized = true;

        NotifyLoginState();
    }

    private void OnAuthStateChanged(object sender, EventArgs eventArgs)
    {
        NotifyLoginState();
    }

    private void NotifyLoginState()
    {
        bool signedIn = IsLoggedIn;
        if (signedIn == lastNotifiedSignedIn)
            return;

        lastNotifiedSignedIn = signedIn;
        Debug.Log(signedIn ? $"[Auth] 로그인 상태: {UserId}" : "[Auth] 로그아웃 상태");
        LoginStateChanged?.Invoke(signedIn);
    }

    public async UniTask<(bool success, string error)> SignInAnonymouslyAsync()
    {
        try
        {
            Debug.Log("[Auth] 익명 로그인 시도...");

            AuthResult result = await auth.SignInAnonymouslyAsync();
            currentUser = result.User;
            NotifyLoginState();

            Debug.Log($"[Auth] 익명 로그인 성공: {currentUser.UserId}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.Log($"[Auth] 익명 로그인 실패: {ex.Message}");
            return (false, ParseFirebaseError(ex.Message));
        }
    }

    public async UniTask<(bool success, string error)> CreateUserWithEmailAsync(string email, string password)
    {
        try
        {
            Debug.Log("[Auth] 회원가입 시도...");

            AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            currentUser = result.User;
            NotifyLoginState();

            Debug.Log($"[Auth] 회원 가입 성공: {currentUser.UserId}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.Log($"[Auth] 회원 가입 실패: {ex.Message}");
            return (false, ParseFirebaseError(ex.Message));
        }
    }

    public async UniTask<(bool success, string error)> SignInUserWithEmailAsync(string email, string password)
    {
        try
        {
            Debug.Log("[Auth] 로그인 시도...");

            AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email, password);
            currentUser = result.User;
            NotifyLoginState();

            Debug.Log($"[Auth] 로그인 성공: {currentUser.UserId}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.Log($"[Auth] 로그인 실패: {ex.Message}");
            return (false, ParseFirebaseError(ex.Message));
        }
    }

    public void SignOut()
    {
        if (auth != null && currentUser != null)
        {
            Debug.Log($"[Auth] 로그아웃");
            auth.SignOut();
            currentUser = null;
            NotifyLoginState();
        }
    }

    private string ParseFirebaseError(string error)
    {
        Debug.LogWarning($"[Auth] Firebase 에러 원문: {error}");

        string lower = error.ToLowerInvariant();

        if (lower.Contains("already in use") || lower.Contains("email-already"))
        {
            return "이미 사용 중인 이메일입니다.";
        }
        if (lower.Contains("at least 6") || lower.Contains("weak") || lower.Contains("password is invalid"))
        {
            return "비밀번호는 6자 이상이어야 합니다.";
        }
        if (lower.Contains("badly formatted") || lower.Contains("invalid-email"))
        {
            return "이메일 형식이 올바르지 않습니다.";
        }
        if (lower.Contains("network"))
        {
            return "네트워크 연결을 확인해주세요.";
        }

        return "이메일 또는 비밀번호를 확인해주세요.";
    }
}
