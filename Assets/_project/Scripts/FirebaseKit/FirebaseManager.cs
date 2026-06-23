using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    private static FirebaseManager instance;
    public static FirebaseManager Instance => instance;
    public FirebaseApp App { get; private set; }
    public FirebaseAuth Auth { get; private set; }
    public FirebaseDatabase Database { get; private set; }

    public enum InitState
    {
        Pending,
        Ready,
        Failed
    }

    public InitState State { get; private set; } = InitState.Pending;
    public bool IsReady => State == InitState.Ready;

    public string LastError { get; private set; }


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
        InitializeFirebaseAsync().Forget();
    }

    private async UniTaskVoid InitializeFirebaseAsync()
    {
        Debug.Log("[Firebase] 초기화 시작...");

        try
        {
            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask();

            if (status != DependencyStatus.Available)
            {
                Fail($"의존성 오류: {status}");
                return;
            }

            App = FirebaseApp.DefaultInstance;

            Database = GetDatabase(App);
            Auth = FirebaseAuth.GetAuth(App);

            State = InitState.Ready;
            Debug.Log($"[Firebase] 초기화 성공 (app={App.Name})");
        }
        catch (System.Exception ex)
        {
            Fail(ex.Message);
        }
    }

    private FirebaseDatabase GetDatabase(FirebaseApp app)
    {
        FirebaseConfig config = Resources.Load<FirebaseConfig>("FirebaseConfig");
        if (config != null && !string.IsNullOrEmpty(config.databaseUrl))
        {
            return FirebaseDatabase.GetInstance(app, config.databaseUrl);
        }
        return FirebaseDatabase.GetInstance(app);
    }
    private void Fail(string error)
    {
        LastError = error;
        State = InitState.Failed;
        Debug.LogError($"[Firebase] 초기화 실패: {error}");
    }

    public async UniTask<bool> WaitForInitializationAsync()
    {
        await UniTask.WaitUntil(() => State != InitState.Pending);
        return State == InitState.Ready;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
