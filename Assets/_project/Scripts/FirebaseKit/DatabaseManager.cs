using Cysharp.Threading.Tasks;
using Firebase.Database;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    private static DatabaseManager instance;
    public static DatabaseManager Instance => instance;

    private FirebaseDatabase database;
    private bool isInitialized = false;
    public bool IsInitialized => isInitialized;

    private readonly Dictionary<string, EventHandler<ValueChangedEventArgs>> listeners = new();

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
            Debug.LogError("[DB] Firebase 초기화 실패, DB 사용 불가");
            return;
        }
        database = FirebaseManager.Instance.Database;
        isInitialized = true;
    }

    public async UniTask<(bool success, string error)> SetAsync(string path, object data)
    {
        try
        {
            string json = JsonConvert.SerializeObject(data);

            await database.RootReference.Child(path).SetRawJsonValueAsync(json).AsUniTask();
            Debug.Log($"[DB] 저장 성공: {path}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DB] 저장 실패({path}): {ex.Message}");
            return (false, ex.Message);
        }
    }
    public async UniTask<(bool success, T data, string error)> GetAsync<T>(string path)
    {
        try
        {
            DataSnapshot snapshot = await database.RootReference.Child(path).GetValueAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                Debug.Log($"[DB] 데이터 없음: {path}");
                return (true, default, null);
            }
            string json = snapshot.GetRawJsonValue();
            T result = JsonConvert.DeserializeObject<T>(json);

            Debug.Log($"[DB] 읽기 성공: {path}");
            return (true, result, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DB] 읽기 실패({path}): {ex.Message}");
            return (false, default, ex.Message);
        }
    }

    /// <summary>이미 JSON 문자열인 데이터를 직렬화 없이 그대로 저장 (세이브 등). SetAsync와 달리 재직렬화 안 함.</summary>
    public async UniTask<(bool success, string error)> SetRawAsync(string path, string json)
    {
        try
        {
            await database.RootReference.Child(path).SetRawJsonValueAsync(json).AsUniTask();
            Debug.Log($"[DB] Raw 저장 성공: {path}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DB] Raw 저장 실패({path}): {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>경로의 데이터를 JSON 문자열 그대로 읽음 (세이브 등). 데이터 없으면 json은 null.</summary>
    public async UniTask<(bool success, string json, string error)> GetRawAsync(string path)
    {
        try
        {
            DataSnapshot snapshot = await database.RootReference.Child(path).GetValueAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                Debug.Log($"[DB] 데이터 없음: {path}");
                return (true, null, null);
            }

            string json = snapshot.GetRawJsonValue();
            Debug.Log($"[DB] Raw 읽기 성공: {path}");
            return (true, json, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DB] Raw 읽기 실패({path}): {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async UniTask<(bool success, string error)> DeleteAsync(string path)
    {
        try
        {
            await database.RootReference.Child(path).RemoveValueAsync().AsUniTask();
            Debug.Log($"[DB] 삭제 성공: {path}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DB] 삭제 실패({path}): {ex.Message}");
            return (false, ex.Message);
        }
    }
    public async UniTask<(bool success, string error)> UpdateAsync(string path, Dictionary<string, object> updates)
    {
        try
        {
            await database.RootReference.Child(path).UpdateChildrenAsync(updates).AsUniTask();
            Debug.Log($"[DB] 수정 성공: {path}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DB] 수정 실패({path}): {ex.Message}");
            return (false, ex.Message);
        }
    }

    public void Listen<T>(string path, Action<T> onChanged)
    {
        if (listeners.ContainsKey(path))
        {
            StopListen(path);
        }
        var reference = database.RootReference.Child(path);

        EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
        {
            if (args.DatabaseError != null)
            {
                Debug.LogError($"[DB] 리스너 오류({path}): {args.DatabaseError.Message}");
                return;
            }

            if (!args.Snapshot.Exists)
            {
                onChanged?.Invoke(default); 
                return;
            }

            string json = args.Snapshot.GetRawJsonValue();
            T result = JsonConvert.DeserializeObject<T>(json);
            onChanged?.Invoke(result);  
        };

        reference.ValueChanged += handler;
        listeners[path] = handler;

        Debug.Log($"[DB] 구독 시작: {path}");

    }
    public void StopListen(string path)
    {
        if (!listeners.TryGetValue(path, out var handler))
            return;

        database.RootReference.Child(path).ValueChanged -= handler;
        listeners.Remove(path);

        Debug.Log($"[DB] 구독 해제: {path}");
    }
    private void OnDestroy()
    {
        foreach (var kvp in listeners)
        {
            database.RootReference.Child(kvp.Key).ValueChanged -= kvp.Value;
        }
        listeners.Clear();
        if (instance == this)
        {
            instance = null;
        }
    }
}