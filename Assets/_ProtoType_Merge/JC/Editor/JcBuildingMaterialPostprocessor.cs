using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DH 씬의 기존 FBX 내장 재질 참조를 유지하면서 JC 외부 재질의 외형을 반영합니다.
/// 씬이나 외부 재질을 저장하지 않고 모델 임포트 결과만 변경합니다.
/// </summary>
public sealed class JcBuildingMaterialPostprocessor : AssetPostprocessor
{
    private const string BuildingRoot = "Assets/_ProtoType_Merge/DH/AlphaAsset/AIAsset/Building/";

    public override uint GetVersion() => 1;

    private string SourceMaterialPath
    {
        get
        {
            switch (assetPath)
            {
                case BuildingRoot + "Original/BGHouse001.fbx":
                    return BuildingRoot + "Meterial/BGHouse001_material.mat";
                case BuildingRoot + "Original/BGStore001.fbx":
                    return BuildingRoot + "Meterial/BGStore001_material.mat";
                default:
                    return null;
            }
        }
    }

    private void OnPreprocessModel()
    {
        string sourcePath = SourceMaterialPath;
        if (sourcePath != null)
            context.DependsOnSourceAsset(sourcePath);
    }

    private void OnPostprocessModel(GameObject root)
    {
        string sourcePath = SourceMaterialPath;
        if (sourcePath == null)
            return;

        Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        if (source == null || source.shader == null)
        {
            context.LogImportError("건물 재질 원본 또는 셰이더를 찾을 수 없습니다: " + sourcePath);
            return;
        }

        var processed = new HashSet<Material>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                // 외부 에셋은 절대 수정하지 않습니다. 임포트 중 생성된 내장 재질만 처리합니다.
                if (material == null || !processed.Add(material))
                    continue;
                string materialPath = AssetDatabase.GetAssetPath(material);
                if (!string.IsNullOrEmpty(materialPath) && materialPath != assetPath)
                    continue;

                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }
        }
    }
}
