using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 지정한 폴더(또는 Project 창에서 선택한 폴더/스프라이트)의 모든 스프라이트
/// (Single + Multiple 내부 슬라이스 포함)의 pivot을 Bottom(0.5, 0)으로 일괄 설정.
///
/// 메뉴:
///   Tools/Sprites/Set Pivots to Bottom (Selected or Assets/Imported)
///   Tools/Sprites/Set Pivots to Bottom (Assets/Imported - All)
///
/// 동작:
///  - Project 창에서 폴더/png 선택돼있으면 그 선택만 처리
///  - 아무것도 선택 안 됐으면 Assets/Imported 전체 처리
///  - 이미 Bottom인 스프라이트는 스킵
///  - Multiple 모드는 SpriteDataProvider로 GUID/name 유지하면서 pivot만 수정
///    → prefab 레퍼런스 안 깨짐
/// </summary>
public static class SetSpritePivotsBottom
{
    private const string kDefaultFolder = "Assets/Imported";
    private static readonly Vector2 kBottomPivot = new Vector2(0.5f, 0f);

    [MenuItem("Tools/Sprites/Set Pivots to Bottom (Selected or Assets-Imported)")]
    public static void RunOnSelectionOrDefault()
    {
        string[] roots = GetSearchRoots(useSelectionIfAny: true);
        Process(roots);
    }

    [MenuItem("Tools/Sprites/Set Pivots to Bottom (Assets-Imported - All)")]
    public static void RunOnAllImported()
    {
        Process(new[] { kDefaultFolder });
    }

    private static string[] GetSearchRoots(bool useSelectionIfAny)
    {
        if (useSelectionIfAny)
        {
            var sel = Selection.assetGUIDs;
            if (sel != null && sel.Length > 0)
            {
                var list = new List<string>(sel.Length);
                foreach (var g in sel)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    if (!string.IsNullOrEmpty(p)) list.Add(p);
                }
                if (list.Count > 0) return list.ToArray();
            }
        }
        return new[] { kDefaultFolder };
    }

    private static void Process(string[] roots)
    {
        // 선택이 폴더면 FindAssets에 넘기고, 파일이면 그 파일만 직접 처리
        var texturePaths = new List<string>();
        var folderRoots = new List<string>();
        foreach (var r in roots)
        {
            if (AssetDatabase.IsValidFolder(r)) folderRoots.Add(r);
            else if (r.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".psd", System.StringComparison.OrdinalIgnoreCase) ||
                     r.EndsWith(".tga", System.StringComparison.OrdinalIgnoreCase))
            {
                texturePaths.Add(r);
            }
        }
        if (folderRoots.Count > 0)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", folderRoots.ToArray());
            foreach (var g in guids)
                texturePaths.Add(AssetDatabase.GUIDToAssetPath(g));
        }

        if (texturePaths.Count == 0)
        {
            Debug.LogWarning("[SetSpritePivotsBottom] 대상 텍스처가 없음. 선택을 확인하거나 Assets/Imported 경로를 확인해.");
            return;
        }

        int changedFiles = 0;
        int changedSprites = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string path = texturePaths[i];
                EditorUtility.DisplayProgressBar(
                    "Set Sprite Pivots to Bottom",
                    $"{i + 1}/{texturePaths.Count}  {path}",
                    (float)i / Mathf.Max(1, texturePaths.Count));

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite) continue;

                bool fileChanged = false;

                if (importer.spriteImportMode == SpriteImportMode.Single)
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if (settings.spriteAlignment != (int)SpriteAlignment.BottomCenter ||
                        settings.spritePivot != kBottomPivot)
                    {
                        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                        settings.spritePivot = kBottomPivot;
                        importer.SetTextureSettings(settings);
                        fileChanged = true;
                        changedSprites++;
                    }
                }
                else if (importer.spriteImportMode == SpriteImportMode.Multiple)
                {
                    var factory = new SpriteDataProviderFactories();
                    factory.Init();
                    var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                    if (provider == null) continue;
                    provider.InitSpriteEditorDataProvider();

                    var rects = provider.GetSpriteRects();
                    bool anyChanged = false;
                    for (int r = 0; r < rects.Length; r++)
                    {
                        if (rects[r].alignment != SpriteAlignment.BottomCenter ||
                            rects[r].pivot != kBottomPivot)
                        {
                            rects[r].alignment = SpriteAlignment.BottomCenter;
                            rects[r].pivot = kBottomPivot;
                            anyChanged = true;
                            changedSprites++;
                        }
                    }
                    if (anyChanged)
                    {
                        provider.SetSpriteRects(rects);
                        provider.Apply();
                        fileChanged = true;
                    }
                }

                if (fileChanged)
                {
                    importer.SaveAndReimport();
                    changedFiles++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SetSpritePivotsBottom] 완료. 변경된 파일: {changedFiles}, 변경된 스프라이트(슬라이스 포함): {changedSprites}");
    }
}
