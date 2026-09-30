using System.Linq;
using UnityEditor;
using UnityEngine;

// 새 FBX만 기존 배치의 축/100배 모델 단위에 맞춥니다. 원본 에셋은 처리하지 않습니다.
public sealed class RemainingBuildingsJcImporter : AssetPostprocessor
{
    const string ModelDirectory = "Assets/_ProtoType_Merge/DH/AlphaAsset/AIAsset/Building/JC_Mod/";
    static readonly string[] ModelNames = { "BGHigh001_JC.fbx", "BGHigh002_JC.fbx", "BGStore001_JC.fbx", "BGStore002_JC.fbx", "BGStore004_JC.fbx", "Bank_JC.fbx", "HeroAssociation_JC.fbx", "VillainUnion_JC.fbx" };
    public override uint GetVersion() => 1;

    void OnPostprocessModel(GameObject root)
    {
        if (!ModelNames.Any(name => assetPath == ModelDirectory + name)) return;
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 1 || filters[0].transform != root.transform)
        {
            context.LogImportError("JC 수정 건물은 루트에 결합된 메시 하나를 사용해야 합니다.");
            return;
        }
        bool unitRoot = assetPath.EndsWith("/Bank_JC.fbx") || assetPath.EndsWith("/HeroAssociation_JC.fbx");
        var rotation = new Quaternion(-0.7071068f, 0f, 0f, 0.7071067f);
        if (unitRoot) rotation = Quaternion.identity;
        var scale = Vector3.one * (unitRoot ? 1f : 100f);
        var correction = Matrix4x4.TRS(Vector3.zero, rotation, scale).inverse * root.transform.localToWorldMatrix;
        var mesh = filters[0].sharedMesh;
        mesh.vertices = mesh.vertices.Select(correction.MultiplyPoint3x4).ToArray();
        var normalMatrix = correction.inverse.transpose;
        mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
        mesh.tangents = mesh.tangents.Select(t =>
        {
            var v = correction.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
            return new Vector4(v.x, v.y, v.z, t.w);
        }).ToArray();
        mesh.RecalculateBounds();
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = rotation;
        root.transform.localScale = scale;
    }
}
