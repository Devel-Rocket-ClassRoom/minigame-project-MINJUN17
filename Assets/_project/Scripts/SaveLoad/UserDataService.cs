using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public class UserDataService : MonoBehaviour
{
    private static UserDataService instance;
    public static UserDataService Instance => instance;

    private void Awake()
    {
        if(instance == null)
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
    public async UniTask<bool> SaveNicknameAsync(string nickname)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError("[User] 로그인 안 됨 - 닉네임 저장 불가");
            return false;
        }

        var profile = new ProfileData()
        {
            nickname = nickname,
            email = AuthManager.Instance.CurrentUser?.Email,
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var (ok, err) = await DatabaseManager.Instance.SetAsync($"users/{uid}/profile", profile);

        if (ok) Debug.Log($"[User] 닉네임 저장 성공: {nickname}");
        else Debug.LogError($"[User] 닉네임 저장 실패: {err}");

        return ok;
    }

    /// <summary>현재 로그인한 유저의 프로필을 읽어온다. 없으면(신규 유저) null.</summary>
    public async UniTask<ProfileData> LoadProfileAsync()
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError("[User] 로그인 안 됨 - 프로필 로드 불가");
            return null;
        }

        var (ok, data, err) = await DatabaseManager.Instance.GetAsync<ProfileData>($"users/{uid}/profile");
        if (!ok) Debug.LogError($"[User] 프로필 로드 실패: {err}");

        return ok ? data : null;   // 신규 유저면 data가 null (정상)
    }

    // ─────────────────────────────────────── 로컬 슬롯 주인(uid) 관리

    // 로컬 슬롯 파일이 어느 계정 것인지 PlayerPrefs에 기록 → 네트워크 없이도 계정 섞임 방지
    private string SlotOwnerKey(int slot) => $"save.owner.slot{slot}";

    /// <summary>이 슬롯의 로컬 세이브 주인을 현재 로그인 uid로 표시.</summary>
    private void ClaimSlotOwner(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid)) return;
        PlayerPrefs.SetString(SlotOwnerKey(slot), uid);
        PlayerPrefs.Save();
    }

    /// <summary>로컬 슬롯의 주인이 현재 계정과 다르면 로컬 세이브를 삭제 (오프라인에서도 동작).</summary>
    private void ReconcileLocalOwner(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid)) return;
        if (!SaveLoadManager.HasSave(slot)) return;   // 로컬에 세이브 자체가 없으면 할 일 없음

        string owner = PlayerPrefs.GetString(SlotOwnerKey(slot), null);
        if (owner != uid)
        {
            // 다른 계정(또는 주인 불명)이 남긴 로컬 세이브 → 제거
            Debug.Log($"[User] 슬롯 {slot} 주인 불일치(local={owner}, now={uid}) - 로컬 세이브 정리");
            SaveLoadManager.DeleteSave(slot);
            PlayerPrefs.DeleteKey(SlotOwnerKey(slot));
        }
    }

    // ─────────────────────────────────────── 클라우드 세이브

    /// <summary>로컬 슬롯의 세이브 JSON을 클라우드에 업로드한다. (로컬 저장은 SaveLoadManager가 이미 끝낸 상태 가정)</summary>
    public async UniTask<bool> UploadSaveAsync(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError("[User] 로그인 안 됨 - 세이브 업로드 불가");
            return false;
        }

        // 슬롯 JSON 읽기
        string json = SaveLoadManager.ExportSlotJson(slot);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError($"[User] 슬롯 {slot} 세이브 없음 - 업로드 불가");
            return false;
        }

        // 3) 이미 JSON이므로 SetRawAsync로 그대로 업로드
        var (ok, err) = await DatabaseManager.Instance.SetRawAsync($"users/{uid}/saves/slot{slot}", json);

        if (ok)
        {
            ClaimSlotOwner(slot);   // 이 로컬 세이브 주인 = 현재 계정
            Debug.Log($"[User] 세이브 업로드 성공: slot{slot}");
        }
        else Debug.LogError($"[User] 세이브 업로드 실패: {err}");

        return ok;
    }

    /// <summary>클라우드에서 세이브 JSON을 받아 로컬 슬롯 파일에 쓴다. 이후 LoadFromSlot으로 게임에 반영.</summary>
    public async UniTask<bool> DownloadSaveAsync(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError("[User] 로그인 안 됨 - 세이브 다운로드 불가");
            return false;
        }

        // 네트워크와 무관하게 먼저 주인 검사 → 다른 계정이 남긴 로컬 세이브 제거
        ReconcileLocalOwner(slot);

        var (ok, json, err) = await DatabaseManager.Instance.GetRawAsync($"users/{uid}/saves/slot{slot}");
        if (!ok)
        {
            // 네트워크 등 오류 — 로컬은 건드리지 않음 (멀쩡한 세이브 보존)
            Debug.LogError($"[User] 세이브 다운로드 실패: {err}");
            return false;
        }
        if (string.IsNullOrEmpty(json))
        {
            // 이 계정엔 클라우드 세이브 없음 → 다른 계정이 남긴 로컬 세이브를 제거해 깨끗하게 시작
            Debug.Log($"[User] 클라우드에 세이브 없음: slot{slot} - 로컬 슬롯 정리");
            SaveLoadManager.DeleteSave(slot);
            return false;
        }

        SaveLoadManager.ImportSlotJson(slot, json);   // 로컬 파일로 씀 (클라우드 = 정답)
        ClaimSlotOwner(slot);                          // 받은 로컬 세이브 주인 = 현재 계정
        Debug.Log($"[User] 세이브 다운로드 성공: slot{slot}");
        return true;
    }

    [ContextMenu("테스트: 익명로그인 후 닉네임 저장")]
    private async void TestSave()
    {
        await AuthManager.Instance.SignInAnonymouslyAsync();   // 1) 익명 로그인
        await SaveNicknameAsync("민준식당");                     // 2) 닉네임 저장
    }
}
