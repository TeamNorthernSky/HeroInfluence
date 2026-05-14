using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace KJ_Work.Scripts.Editor
{
    public class KJ_SmoothNormalBaker : EditorWindow
    {
        [MenuItem("Tools/KJ/Smooth Normal Baker")]
        public static void ShowWindow()
        {
            GetWindow<KJ_SmoothNormalBaker>("Smooth Normal Baker");
        }

        private GameObject targetObject;
        private bool bakeToColor = false;
        private bool bakeToUV2 = true;
        private bool bakeToUV3 = false;
        private bool preserveColorAlpha = true;

        void OnGUI()
        {
            GUILayout.Label("Smooth Normal Baker (Inverted Hull Outline)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
            
            EditorGUILayout.Space();
            GUILayout.Label("Bake Destination", EditorStyles.label);
            bakeToColor = EditorGUILayout.Toggle("Vertex Color (RGB)", bakeToColor);
            if (bakeToColor)
            {
                EditorGUI.indentLevel++;
                preserveColorAlpha = EditorGUILayout.Toggle("Preserve Color Alpha", preserveColorAlpha);
                EditorGUI.indentLevel--;
            }
            bakeToUV2 = EditorGUILayout.Toggle("UV2", bakeToUV2);
            bakeToUV3 = EditorGUILayout.Toggle("UV3", bakeToUV3);

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake Smooth Normals"))
            {
                if (targetObject == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please select a Target Object.", "OK");
                    return;
                }

                BakeSmoothNormals(targetObject);
            }
        }

        private void BakeSmoothNormals(GameObject root)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            SkinnedMeshRenderer[] skinners = root.GetComponentsInChildren<SkinnedMeshRenderer>();

            int processedCount = 0;

            foreach (var mf in filters)
            {
                if (mf.sharedMesh != null)
                {
                    ProcessMesh(mf.sharedMesh);
                    processedCount++;
                }
            }

            foreach (var smr in skinners)
            {
                if (smr.sharedMesh != null)
                {
                    ProcessMesh(smr.sharedMesh);
                    processedCount++;
                }
            }

            EditorUtility.DisplayDialog("Complete", $"Baked Smooth Normals to {processedCount} meshes.\n\nPlease save your project to keep changes.", "OK");
        }

        private void ProcessMesh(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            
            if (vertices.Length == 0) return;

            // Map shared vertices (same position)
            Dictionary<Vector3, Vector3> averageNormals = new Dictionary<Vector3, Vector3>();
            
            for (int i = 0; i < vertices.Length; i++)
            {
                // Rounding to avoid float precision issues when comparing positions
                Vector3 pos = new Vector3(
                    Mathf.Round(vertices[i].x * 1000f) / 1000f,
                    Mathf.Round(vertices[i].y * 1000f) / 1000f,
                    Mathf.Round(vertices[i].z * 1000f) / 1000f
                );

                if (!averageNormals.ContainsKey(pos))
                {
                    averageNormals[pos] = normals[i];
                }
                else
                {
                    averageNormals[pos] += normals[i];
                }
            }

            // Normalize all grouped normals
            List<Vector3> keys = new List<Vector3>(averageNormals.Keys);
            foreach (var key in keys)
            {
                averageNormals[key] = averageNormals[key].normalized;
            }

            // Assign smoothed normals to target channels
            Vector3[] smoothNormals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 pos = new Vector3(
                    Mathf.Round(vertices[i].x * 1000f) / 1000f,
                    Mathf.Round(vertices[i].y * 1000f) / 1000f,
                    Mathf.Round(vertices[i].z * 1000f) / 1000f
                );
                smoothNormals[i] = averageNormals[pos];
            }

            // Write to Mesh
            if (bakeToUV2)
            {
                mesh.SetUVs(1, smoothNormals); // Channel 1 = UV2
            }

            if (bakeToUV3)
            {
                mesh.SetUVs(2, smoothNormals); // Channel 2 = UV3
            }

            if (bakeToColor)
            {
                Color[] colors = mesh.colors;
                if (colors == null || colors.Length != vertices.Length)
                {
                    colors = new Color[vertices.Length];
                    for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                }

                for (int i = 0; i < smoothNormals.Length; i++)
                {
                    // Map -1..1 to 0..1
                    float r = smoothNormals[i].x * 0.5f + 0.5f;
                    float g = smoothNormals[i].y * 0.5f + 0.5f;
                    float b = smoothNormals[i].z * 0.5f + 0.5f;
                    
                    float a = preserveColorAlpha ? colors[i].a : 1.0f;
                    colors[i] = new Color(r, g, b, a);
                }
                mesh.colors = colors;
            }
            
            // Mark the mesh as dirty so Unity knows it was modified
            EditorUtility.SetDirty(mesh);
        }
    }
}
