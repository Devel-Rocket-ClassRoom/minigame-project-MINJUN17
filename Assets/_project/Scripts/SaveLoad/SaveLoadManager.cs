using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 세이브/로드 매니저. JSON으로 Application.persistentDataPath/save_{slot}.json에 저장.
///
/// 슬롯 시스템:
/// - 슬롯 0/1/2 — 시작씬에서 ActiveSlot 설정 후 게임씬 진입.
/// - 게임씬 진입 시 ActiveSlot의 세이브가 있으면 자동 로드.
/// - 시작씬 미구현 상태에선 ActiveSlot=0이 기본값 (기존 단일 세이브와 동일 동작).
///
/// API:
///   인스턴스(게임씬용): Save(), Load(), HasActiveSave
///   static(시작씬용): StartNewGame(slot), ContinueGame(slot), HasSave(slot), DeleteSave(slot), TryPeekSaveInfo(slot)
///
/// DefaultExecutionOrder(-50): 다른 매니저 Start보다 먼저 실행되어 자동 로드.
/// </summary>
[DefaultExecutionOrder(-50)]
public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance;

    /// <summary>현재 게임이 사용하는 슬롯 (시작씬 → 게임씬 전달용). 기본 0.</summary>
    public static int ActiveSlot { get; private set; } = 0;
    public const int SlotCount = 3;

    /// <summary>
    /// 세이브 데이터 버전. 필드 추가/변경 시 +1.
    /// v1: Phase 1~4
    /// v2: Phase 5 (catalog/marketing/candidatePool) + Phase 6 (ranking)
    /// v3: 누적 만족도(lifetimeSatisfaction) + 손님 해금(customers)
    /// </summary>
    private const int    CurrentVersion = 3;
    private const string FileFormat     = "save_{0}.json";
    private const string ActiveSlotPref = "save.activeSlot";

    [Header("싱글톤이 아닌 매니저 참조")]
    [SerializeField] private TimeSystem       _time;
    [SerializeField] private ExpansionManager _expansion;
    [SerializeField] private PlacementSystem  _placement;

    // JSON 옵션 — forward/backward 호환성 확보
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting            = Formatting.Indented,
        MissingMemberHandling = MissingMemberHandling.Ignore,   // 모르는 필드 무시 (구버전 코드가 신버전 세이브 읽을 때)
        NullValueHandling     = NullValueHandling.Ignore,        // null 필드 직렬화 생략 (파일 크기 + backward compat)
    };

    public bool HasActiveSave => HasSave(ActiveSlot);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 앱 재시작 시 마지막 슬롯 복원 (시작씬에서 명시적으로 SetActiveSlot 호출되면 덮어씀)
        if (PlayerPrefs.HasKey(ActiveSlotPref))
            ActiveSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotPref), 0, SlotCount - 1);
    }

    private void Start()
    {
        // 게임씬 진입 시 ActiveSlot에 세이브 있으면 자동 로드.
        if (HasActiveSave) Load();
    }

    // ─────────────────────────────────────── 시작씬용 static API

    /// <summary>슬롯 선택 + 세이브 삭제. 게임씬 로드 직전에 호출.</summary>
    public static void StartNewGame(int slot)
    {
        SetActiveSlot(slot);
        DeleteSave(slot);
    }

    /// <summary>슬롯 선택만. 게임씬에서 자동 로드됨.</summary>
    public static void ContinueGame(int slot)
    {
        SetActiveSlot(slot);
    }

    public static void SetActiveSlot(int slot)
    {
        ActiveSlot = Mathf.Clamp(slot, 0, SlotCount - 1);
        PlayerPrefs.SetInt(ActiveSlotPref, ActiveSlot);
        PlayerPrefs.Save();
    }

    public static bool HasSave(int slot) => File.Exists(FilePath(slot));

    public static void DeleteSave(int slot)
    {
        var path = FilePath(slot);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>슬롯의 세이브 헤더만 읽어옴. 시작씬 슬롯 선택 UI에서 미리보기 표시용.</summary>
    public static bool TryPeekSaveInfo(int slot, out SaveInfo info)
    {
        info = default;
        var path = FilePath(slot);
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<SaveData>(json, JsonSettings);
            if (data == null) return false;

            info = new SaveInfo
            {
                slot      = slot,
                version   = data.version,
                savedAt   = new DateTime(data.timestampTicks, DateTimeKind.Utc).ToLocalTime(),
                year      = data.time != null ? data.time.year  : 0,
                month     = data.time != null ? data.time.month : 0,
                money     = data.money != null ? data.money.money : 0,
            };
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    // ─────────────────────────────────────── Public API (인스턴스)

    [ContextMenu("Save")]
    public bool Save() => SaveToSlot(ActiveSlot);

    [ContextMenu("Load")]
    public bool Load() => LoadFromSlot(ActiveSlot);

    [ContextMenu("Delete Active Save")]
    public void DeleteActiveSave() => DeleteSave(ActiveSlot);

    public bool SaveToSlot(int slot)
    {
        try
        {
            var data = BuildSaveData();
            string json = JsonConvert.SerializeObject(data, JsonSettings);

            // Atomic write: 임시 파일에 먼저 쓰고 교체 (저장 중 크래시 시 원본 보존)
            var finalPath = FilePath(slot);
            var tmpPath   = finalPath + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(finalPath)) File.Replace(tmpPath, finalPath, null);
            else                         File.Move(tmpPath, finalPath);

            // 로컬 저장 성공 → 클라우드에도 자동 백업 (로그인 상태일 때만, 실패해도 게임엔 영향 없음)
            if (UserDataService.Instance != null && AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
                UserDataService.Instance.UploadSaveAsync(slot).Forget();

            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public bool LoadFromSlot(int slot)
    {
        var path = FilePath(slot);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<SaveData>(json, JsonSettings);
            if (data == null) return false;

            if (!TryMigrate(data)) return false;

            ApplySaveData(data);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    // ─────────────────────────────────────── 마이그레이션

    /// <summary>구버전 세이브 → 현재 버전으로 변환. 향후 필드 변경 시 여기에 분기 추가.</summary>
    private bool TryMigrate(SaveData data)
    {
        if (data.version == CurrentVersion) return true;

        if (data.version > CurrentVersion)
        {
            return false;
        }


        // 예시: v1 → v2 (현재는 새 필드 추가뿐이라 별도 처리 불필요 — null 필드는 FromData에서 안전 처리됨)
        // if (data.version < 2) { /* v1 → v2 변환 */ }
        // if (data.version < 3) { /* v2 → v3 변환 */ }

        data.version = CurrentVersion;
        return true;
    }

    // ─────────────────────────────────────── 파일 경로

    private static string FilePath(int slot) =>
        Path.Combine(Application.persistentDataPath, string.Format(FileFormat, slot));

    // ─────────────────────────────────────── 클라우드 세이브 연동용 (JSON 원문 입출력)

    /// <summary>슬롯의 세이브 JSON 원문을 읽음 (클라우드 업로드용). 파일 없으면 null.</summary>
    public static string ExportSlotJson(int slot)
    {
        var path = FilePath(slot);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>클라우드에서 받은 JSON을 슬롯 파일로 씀 (다운로드용).</summary>
    public static void ImportSlotJson(int slot, string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        File.WriteAllText(FilePath(slot), json);
    }

    // ─────────────────────────────────────── 내부 — Phase별로 여기에 매니저 추가

    private SaveData BuildSaveData()
    {
        return new SaveData
        {
            version        = CurrentVersion,
            timestampTicks = DateTime.UtcNow.Ticks,
            money          = MoneySystem.Instance?.ToData(),
            satisfaction   = SatisfactionSystem.Instance?.ToData(),
            time           = _time != null ? _time.ToData() : null,
            reputation     = ReputationSystem.Instance?.ToData(),
            sales          = SalesTracker.Instance?.ToData(),
            expansion      = _expansion != null ? _expansion.ToData() : null,
            placements     = _placement != null ? _placement.ToData() : null,
            staff          = StaffManager.Instance?.CollectSaveData(),
            catalog        = CatalogManager.Instance?.ToData(),
            marketing      = MarketingManager.Instance?.ToData(),
            candidatePool  = StaffCandidatePool.Instance?.ToData(),
            ranking        = RankingSystem.Instance?.ToData(),
            customers      = CustomerManager.Instance?.ToData(),
        };
    }

    private void ApplySaveData(SaveData data)
    {
        MoneySystem.Instance?.FromData(data.money);
        SatisfactionSystem.Instance?.FromData(data.satisfaction);
        _time?.FromData(data.time);
        ReputationSystem.Instance?.FromData(data.reputation);
        SalesTracker.Instance?.FromData(data.sales);
        CatalogManager.Instance?.FromData(data.catalog);             // 해금 상태 (가구/직원 복원보다 먼저)
        _expansion?.FromData(data.expansion);                        // 확장 셀 활성화 (가구 복원보다 먼저)
        _placement?.FromData(data.placements);                       // 가구 인스턴스화 (그리드 준비된 후)
        StaffManager.Instance?.RestoreFromData(data.staff);          // 직원 복원 (카운터 등 가구 복원 후)
        MarketingManager.Instance?.FromData(data.marketing);         // 마케팅 캠페인 (CustomerManager multiplier 반영)
        StaffCandidatePool.Instance?.FromData(data.candidatePool);   // 채용 후보/티켓 (StaffData 등록 후)
        RankingSystem.Instance?.FromData(data.ranking);              // 연말 결과
        CustomerManager.Instance?.FromData(data.customers);          // 손님 해금 목록 (SaveIdRegistry 등록 후)
    }
}

/// <summary>시작씬에서 슬롯 미리보기에 쓰는 헤더 정보.</summary>
public struct SaveInfo
{
    public int      slot;
    public int      version;
    public DateTime savedAt;
    public int      year;
    public int      month;
    public long     money;
}
