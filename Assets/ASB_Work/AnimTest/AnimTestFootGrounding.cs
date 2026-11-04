using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the planted feet in the animation test scene on its flat floor.
/// Attach this to the sample wrapper, outside the animated model hierarchy.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class AnimTestFootGrounding : MonoBehaviour
{
    [SerializeField] private float floorHeight;
    [SerializeField] private SkinnedMeshRenderer characterRenderer;

    private Mesh bakedMesh;
    private readonly List<Vector3> bakedVertices = new List<Vector3>();
    private int[] footVertices;

    private void Awake()
    {
        if (characterRenderer == null)
            characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        if (characterRenderer == null || characterRenderer.sharedMesh == null)
            return;

        Transform[] bones = characterRenderer.bones;
        int leftFoot = System.Array.FindIndex(bones, bone => bone != null && bone.name == "L_Foot");
        int rightFoot = System.Array.FindIndex(bones, bone => bone != null && bone.name == "R_Foot");
        if (leftFoot < 0 || rightFoot < 0)
            return;

        BoneWeight[] weights = characterRenderer.sharedMesh.boneWeights;
        var indices = new List<int>();
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight weight = weights[i];
            float footWeight = WeightForBone(weight, leftFoot) + WeightForBone(weight, rightFoot);
            if (footWeight > 0.5f)
                indices.Add(i);
        }

        if (indices.Count == 0)
            return;

        footVertices = indices.ToArray();
        bakedMesh = new Mesh { name = "AnimTestFootGrounding_BakedMesh" };
    }

    private void LateUpdate()
    {
        if (bakedMesh == null || characterRenderer == null)
            return;

        characterRenderer.BakeMesh(bakedMesh);
        bakedMesh.GetVertices(bakedVertices);
        if (bakedVertices.Count != characterRenderer.sharedMesh.vertexCount)
            return;

        // BakeMesh includes the transform scale, so only its position and rotation
        // are needed to turn the baked vertices into world positions.
        Matrix4x4 toWorld = Matrix4x4.TRS(
            characterRenderer.transform.position,
            characterRenderer.transform.rotation,
            Vector3.one);

        float soleHeight = float.PositiveInfinity;
        foreach (int index in footVertices)
            soleHeight = Mathf.Min(soleHeight, toWorld.MultiplyPoint3x4(bakedVertices[index]).y);

        float correction = floorHeight - soleHeight;
        if (Mathf.Abs(correction) > 0.001f)
            transform.position += Vector3.up * correction;
    }

    private void OnDestroy()
    {
        if (bakedMesh != null)
            Destroy(bakedMesh);
    }

    private static float WeightForBone(BoneWeight weight, int bone)
    {
        float total = 0f;
        if (weight.boneIndex0 == bone) total += weight.weight0;
        if (weight.boneIndex1 == bone) total += weight.weight1;
        if (weight.boneIndex2 == bone) total += weight.weight2;
        if (weight.boneIndex3 == bone) total += weight.weight3;
        return total;
    }
}
