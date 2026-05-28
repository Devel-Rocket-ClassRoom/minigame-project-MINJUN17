using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프로젝트 내 모든 프리팹/씬의 TMP 텍스트 컴포넌트의 fontAsset 을 일괄 교체.
/// 메뉴: Tools/Fonts/Bulk Replace TMP Font Asset
///
/// 사용법:
///   1) Old: 현재 사용 중인 폰트 (예: Galmuri9 SDF)
///   2) New: 바꿀 폰트
///   3) Replace All 클릭
/// 결과: 모든 Prefab + 빌드 세팅에 등록된 Scene 의 TMP_Text / TextMeshProUGUI 의 fontAsset 이 새 것으로 교체됨.
/// </summary>
public class BulkReplaceFontAsset : EditorWindow
{
    private TMP_FontAsset _oldFont;
    private TMP_FontAsset _newFont;
    private bool _includePrefabs = true;
    private bool _includeOpenScene = true;
    private bool _includeBuildScenes = false;
    private bool _alsoUpdateFallbacks = false;

    [MenuItem("Tools/Fonts/Bulk Replace TMP Font Asset")]
    public static void Open()
    {
        GetWindow<BulkReplaceFontAsset>("Replace TMP Font").minSize = new Vector2(360, 220);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("TMP Font Asset 일괄 교체", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _oldFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Old (현재)", _oldFont, typeof(TMP_FontAsset), false);
        _newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("New (바꿀 것)", _newFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();
        _includePrefabs    = EditorGUILayout.Toggle("Prefab 전체 처리", _includePrefabs);
        _includeOpenScene  = EditorGUILayout.Toggle("현재 열린 Scene 처리", _includeOpenScene);
        _includeBuildScenes = EditorGUILayout.Toggle("Build Settings 의 모든 Scene", _includeBuildScenes);
        _alsoUpdateFallbacks = EditorGUILayout.Toggle("fallbackFontAssetTable 도 갱신", _alsoUpdateFallbacks);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_oldFont == null || _newFont == null || _oldFont == _newFont))
        {
            if (GUILayout.Button("Replace All", GUILayout.Height(32)))
            {
                Run();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "주의:\n" +
            "• 작업 전 git 으로 변경 사항을 커밋해두면 안전.\n" +
            "• Prefab 안의 텍스트 + 열린 Scene 의 텍스트만 처리.\n" +
            "• Build Settings 옵션은 해당 Scene 들을 한 번씩 열어 처리(주의: 작업 중인 Scene 변경 사항이 저장될 수 있음).",
            MessageType.Info);
    }

    private void Run()
    {
        int prefabChanged = 0, sceneChanged = 0;

        if (_includePrefabs)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Replace TMP Font", $"Prefab {i + 1}/{guids.Length}\n{path}", (float)i / guids.Length);

                var root = PrefabUtility.LoadPrefabContents(path);
                bool changed = ReplaceInRoot(root);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabChanged++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            EditorUtility.ClearProgressBar();
        }

        if (_includeOpenScene)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                bool changed = ReplaceInScene(scene);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sceneChanged++;
                }
            }
        }

        if (_includeBuildScenes)
        {
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (!s.enabled || string.IsNullOrEmpty(s.path)) continue;
                var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                bool changed = ReplaceInScene(scene);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sceneChanged++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BulkReplaceFontAsset] 완료. Prefab 변경: {prefabChanged}, Scene 변경: {sceneChanged}  ({_oldFont.name} → {_newFont.name})");
    }

    private bool ReplaceInScene(Scene scene)
    {
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
            if (ReplaceInRoot(root)) changed = true;
        return changed;
    }

    private bool ReplaceInRoot(GameObject root)
    {
        bool changed = false;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null) continue;
            if (t.font == _oldFont)
            {
                Undo.RecordObject(t, "Replace TMP Font");
                t.font = _newFont;
                EditorUtility.SetDirty(t);
                changed = true;
            }
            if (_alsoUpdateFallbacks && t.font != null && t.font.fallbackFontAssetTable != null)
            {
                var list = t.font.fallbackFontAssetTable;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == _oldFont)
                    {
                        list[i] = _newFont;
                        EditorUtility.SetDirty(t.font);
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }
}
