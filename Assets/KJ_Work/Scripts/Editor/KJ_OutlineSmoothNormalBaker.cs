using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KJ_Work.Scripts.Editor
{
    public static class KJ_OutlineSmoothNormalBaker
    {
        private const string OutputRoot = "Assets/KJ_Work/Generated";
        private const string OutputFolder = OutputRoot + "/SmoothOutlineMeshes";
        private const float PositionTolerance = 0.0001f;

        [MenuItem("KJ Work/Outline/Bake Smooth Normals To UV2 (Selected)")]
        public static void BakeSelectedToUv2()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[KJ Outline] Select one or more GameObjects first.");
                return;
            }

            EnsureOutputFolder();

            int rendererCount = 0;
            var bakedMeshes = new Dictionary<Mesh, Mesh>();

            foreach (GameObject root in selectedObjects)
            {
                foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter.sharedMesh == null)
                        continue;

                    Undo.RecordObject(meshFilter, "Bake KJ Outline Smooth Normals");
                    meshFilter.sharedMesh = GetOrCreateBakedMesh(meshFilter.sharedMesh, bakedMeshes);
                    EditorUtility.SetDirty(meshFilter);
                    rendererCount++;
                }

                foreach (SkinnedMeshRenderer skinnedRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skinnedRenderer.sharedMesh == null)
                        continue;

                    Undo.RecordObject(skinnedRenderer, "Bake KJ Outline Smooth Normals");
                    skinnedRenderer.sharedMesh = GetOrCreateBakedMesh(skinnedRenderer.sharedMesh, bakedMeshes);
                    EditorUtility.SetDirty(skinnedRenderer);
                    rendererCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (GameObject selectedObject in selectedObjects)
            {
                if (selectedObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(selectedObject.scene);
            }

            Debug.Log($"[KJ Outline] Baked smooth normals to UV2 for {rendererCount} renderer(s). Created/updated {bakedMeshes.Count} mesh asset(s).");
        }

        [MenuItem("KJ Work/Outline/Bake Smooth Normals To UV2 (Selected)", true)]
        private static bool CanBakeSelectedToUv2()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("KJ Work/Outline/Set KJ Outline Material Normal Source To UV2")]
        public static void SetKjOutlineMaterialToUv2()
        {
            const string materialPath = "Assets/KJ_Work/Materials/KJ_OutlineMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Debug.LogWarning($"[KJ Outline] Material not found: {materialPath}");
                return;
            }

            Undo.RecordObject(material, "Set KJ Outline Material Normal Source To UV2");
            material.SetFloat("_NormalSource", 2f);
            material.DisableKeyword("_NORMALSOURCE_NORMAL");
            material.DisableKeyword("_NORMALSOURCE_COLOR");
            material.EnableKeyword("_NORMALSOURCE_UV2");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Debug.Log("[KJ Outline] KJ_OutlineMaterial now uses UV2 as its outline normal source.");
        }

        private static Mesh GetOrCreateBakedMesh(Mesh sourceMesh, Dictionary<Mesh, Mesh> bakedMeshes)
        {
            if (bakedMeshes.TryGetValue(sourceMesh, out Mesh bakedMesh))
                return bakedMesh;

            string sourcePath = AssetDatabase.GetAssetPath(sourceMesh);
            bool sourceIsGeneratedMesh = sourcePath.StartsWith(OutputFolder, StringComparison.OrdinalIgnoreCase);

            bakedMesh = sourceIsGeneratedMesh ? sourceMesh : UnityEngine.Object.Instantiate(sourceMesh);
            bakedMesh.name = GetBakedMeshName(sourceMesh.name);
            BakeSmoothNormalsToUv2(bakedMesh);

            if (!sourceIsGeneratedMesh)
            {
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{SanitizeFileName(bakedMesh.name)}.asset");
                AssetDatabase.CreateAsset(bakedMesh, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(bakedMesh);
            }

            bakedMeshes[sourceMesh] = bakedMesh;
            return bakedMesh;
        }

        private static void BakeSmoothNormalsToUv2(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;

            if (vertices == null || vertices.Length == 0)
                return;

            if (normals == null || normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            var normalSums = new Dictionary<PositionKey, Vector3>(vertices.Length);

            for (int i = 0; i < vertices.Length; i++)
            {
                var key = new PositionKey(vertices[i], PositionTolerance);
                normalSums.TryGetValue(key, out Vector3 normalSum);
                normalSums[key] = normalSum + normals[i];
            }

            var outlineNormals = new List<Vector4>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 smoothNormal = normalSums[new PositionKey(vertices[i], PositionTolerance)].normalized;
                if (smoothNormal.sqrMagnitude < 0.000001f)
                    smoothNormal = normals[i].normalized;

                outlineNormals.Add(new Vector4(smoothNormal.x, smoothNormal.y, smoothNormal.z, 0f));
            }

            mesh.SetUVs(1, outlineNormals);
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder(OutputRoot))
                AssetDatabase.CreateFolder("Assets/KJ_Work", "Generated");

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder(OutputRoot, "SmoothOutlineMeshes");
        }

        private static string GetBakedMeshName(string sourceName)
        {
            string cleanName = string.IsNullOrWhiteSpace(sourceName) ? "Mesh" : sourceName;
            const string suffix = "_OutlineSmoothUV2";
            return cleanName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? cleanName : cleanName + suffix;
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalidChar, '_');

            return fileName;
        }

        private readonly struct PositionKey : IEquatable<PositionKey>
        {
            private readonly int x;
            private readonly int y;
            private readonly int z;

            public PositionKey(Vector3 position, float tolerance)
            {
                x = Mathf.RoundToInt(position.x / tolerance);
                y = Mathf.RoundToInt(position.y / tolerance);
                z = Mathf.RoundToInt(position.z / tolerance);
            }

            public bool Equals(PositionKey other)
            {
                return x == other.x && y == other.y && z == other.z;
            }

            public override bool Equals(object obj)
            {
                return obj is PositionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = x;
                    hashCode = (hashCode * 397) ^ y;
                    hashCode = (hashCode * 397) ^ z;
                    return hashCode;
                }
            }
        }
    }
}
