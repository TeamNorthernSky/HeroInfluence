using UnityEngine;

namespace JC.VFX
{
    [CreateAssetMenu(menuName="JC VFX/LFL Merge Preset",fileName="L11_LFLMerge_Basic")]
    public sealed class LflMergePreset : ScriptableObject
    {
        [Header("수렴 / 합체")]
        [Tooltip("양손 오브가 합류점으로 모이는 시간(1배속 초)입니다.")]
        [Range(.05f,1f)] public float convergeTime=.25f;
        [Tooltip("합쳐진 구체의 유지 시간(1배속 초)입니다.")]
        [Range(.05f,1f)] public float holdTime=.25f;
        [Tooltip("합체 순간 섬광의 HDR 색상입니다.")]
        [ColorUsage(true,true)] public Color flashColor=new Color(.85f,.92f,1f);
        [Tooltip("합체 섬광 크기에 곱하는 배수입니다.")]
        [Range(.2f,3f)] public float flashSizeMul=.8f;
    }
}
