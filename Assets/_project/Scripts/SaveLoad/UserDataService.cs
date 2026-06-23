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

        return ok ? data : null;
    }


    private string SlotOwnerKey(int slot) => $"save.owner.slot{slot}";

    private void ClaimSlotOwner(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid)) return;
        PlayerPrefs.SetString(SlotOwnerKey(slot), uid);
        PlayerPrefs.Save();
    }

    private void ReconcileLocalOwner(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid)) return;
        if (!SaveLoadManager.HasSave(slot)) return;

        string owner = PlayerPrefs.GetString(SlotOwnerKey(slot), null);
        if (owner != uid)
        {
            Debug.Log($"[User] 슬롯 {slot} 주인 불일치(local={owner}, now={uid}) - 로컬 세이브 정리");
            SaveLoadManager.DeleteSave(slot);
            PlayerPrefs.DeleteKey(SlotOwnerKey(slot));
        }
    }

    
    public async UniTask<bool> UploadSaveAsync(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError("[User] 로그인 안 됨 - 세이브 업로드 불가");
            return false;
        }
        string json = SaveLoadManager.ExportSlotJson(slot);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError($"[User] 슬롯 {slot} 세이브 없음 - 업로드 불가");
            return false;
        }

        var (ok, err) = await DatabaseManager.Instance.SetRawAsync($"users/{uid}/saves/slot{slot}", json);

        if (ok)
        {
            ClaimSlotOwner(slot);
            Debug.Log($"[User] 세이브 업로드 성공: slot{slot}");
        }
        else Debug.LogError($"[User] 세이브 업로드 실패: {err}");

        return ok;
    }

    public async UniTask<bool> DownloadSaveAsync(int slot)
    {
        string uid = AuthManager.Instance.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError("[User] 로그인 안 됨 - 세이브 다운로드 불가");
            return false;
        }

        ReconcileLocalOwner(slot);

        var (ok, json, err) = await DatabaseManager.Instance.GetRawAsync($"users/{uid}/saves/slot{slot}");
        if (!ok)
        {
            Debug.LogError($"[User] 세이브 다운로드 실패: {err}");
            return false;
        }
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log($"[User] 클라우드에 세이브 없음: slot{slot} - 로컬 슬롯 정리");
            SaveLoadManager.DeleteSave(slot);
            return false;
        }

        SaveLoadManager.ImportSlotJson(slot, json);  
        ClaimSlotOwner(slot);                 
        Debug.Log($"[User] 세이브 다운로드 성공: slot{slot}");
        return true;
    }
}
