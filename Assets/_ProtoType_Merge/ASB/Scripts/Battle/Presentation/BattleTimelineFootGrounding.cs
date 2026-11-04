using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grounds the stationary Nekoming 4020/4021 cast while its Timeline owns the pose.
/// The correction fades with the Animator's transition back to Idle.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class BattleTimelineFootGrounding : MonoBehaviour
{
    private const float FadeInSeconds = 0.12f;
    private const float FadeOutSeconds = 0.15f;

    private readonly List<Vector3> bakedVertices = new List<Vector3>();
    private SkinnedMeshRenderer characterRenderer;
    private Mesh bakedMesh;
    private int[] footVertices;
    private float floorHeight;
    private float baselineHeight;
    private float weight;
    private float fadeStartWeight;
    private float fadeElapsed;
    private bool releasing;

    public bool Begin(Animator animator, float groundHeight)
    {
        if (animator == null)
            return false;

        characterRenderer = animator.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (characterRenderer == null || characterRenderer.sharedMesh == null)
            return false;

        Transform[] bones = characterRenderer.bones;
        int leftFoot = System.Array.FindIndex(bones, bone => bone != null && bone.name == "L_Foot");
        int rightFoot = System.Array.FindIndex(bones, bone => bone != null && bone.name == "R_Foot");
        if (leftFoot < 0 || rightFoot < 0)
            return false;

        BoneWeight[] weights = characterRenderer.sharedMesh.boneWeights;
        var indices = new List<int>();
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight vertex = weights[i];
            if (WeightForBone(vertex, leftFoot) + WeightForBone(vertex, rightFoot) > 0.5f)
                indices.Add(i);
        }
        if (indices.Count == 0)
            return false;

        footVertices = indices.ToArray();
        if (bakedMesh == null)
            bakedMesh = new Mesh { name = "BattleTimelineFootGrounding_BakedMesh" };

        floorHeight = groundHeight;
        baselineHeight = transform.position.y;
        weight = 0f;
        fadeElapsed = 0f;
        releasing = false;
        return true;
    }

    public void Release()
    {
        if (releasing)
            return;
        releasing = true;
        fadeStartWeight = weight;
        fadeElapsed = 0f;
    }

    private void LateUpdate()
    {
        if (bakedMesh == null || characterRenderer == null)
            return;

        fadeElapsed += Time.unscaledDeltaTime;
        weight = releasing
            ? Mathf.Lerp(fadeStartWeight, 0f, Mathf.Clamp01(fadeElapsed / FadeOutSeconds))
            : Mathf.Clamp01(fadeElapsed / FadeInSeconds);

        if (releasing && weight <= 0f)
        {
            SetHeight(baselineHeight);
            Destroy(this);
            return;
        }

        characterRenderer.BakeMesh(bakedMesh);
        bakedMesh.GetVertices(bakedVertices);
        if (bakedVertices.Count != characterRenderer.sharedMesh.vertexCount)
            return;

        // BakeMesh already includes scale; applying it again would overcorrect.
        Matrix4x4 toWorld = Matrix4x4.TRS(
            characterRenderer.transform.position,
            characterRenderer.transform.rotation,
            Vector3.one);

        float soleHeight = float.PositiveInfinity;
        foreach (int index in footVertices)
            soleHeight = Mathf.Min(soleHeight, toWorld.MultiplyPoint3x4(bakedVertices[index]).y);

        float fullyGroundedHeight = transform.position.y + floorHeight - soleHeight;
        SetHeight(Mathf.Lerp(baselineHeight, fullyGroundedHeight, weight));
    }

    private void SetHeight(float height)
    {
        Vector3 position = transform.position;
        position.y = height;
        transform.position = position;
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
