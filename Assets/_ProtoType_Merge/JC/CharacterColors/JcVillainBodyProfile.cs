using System;
using UnityEngine;

namespace JC.CharacterColors
{
    [Serializable]
    public struct JcVillainBodySurface
    {
        [Tooltip("검은 몸체 영역의 반사광·광택·메탈릭 조절을 사용합니다. 끄면 기존 재질 반응을 복원합니다. 기본색 베이크에는 포함하지 않고 프로필에 별도로 저장합니다.")]
        public bool enabled;
        [Range(0,4), Tooltip("빛과 환경의 반사광 강도 배율입니다. 0은 반사광 제거, 1은 기본 강도, 4는 강한 반사입니다. 발광은 추가하지 않습니다.")]
        public float reflection;
        [Range(0,1), Tooltip("표면 매끄러움입니다. 0은 넓고 거친 하이라이트, 1은 작고 선명한 하이라이트입니다. 원본 텍스처의 검은 영역에만 적용합니다.")]
        public float smoothness;
        [Range(0,1), Tooltip("금속성입니다. 0은 옷감·플라스틱 같은 비금속, 1은 기본색을 반사광 색으로 사용하는 금속입니다. 검은 금속은 주변 반사가 약하면 더 어두워질 수 있습니다.")]
        public float metallic;
        public static JcVillainBodySurface Default => new JcVillainBodySurface{enabled=true,reflection=1,smoothness=.5f,metallic=0};
    }
    [Serializable]
    public struct JcVillainBodyTuning
    {
        [Tooltip("검은 옷의 기준색입니다. 원본 주름과 옷감의 명암 차이를 남기고 이 색감으로 보정합니다. 알파는 원본 그대로 유지합니다.")]
        public Color tint;
        [Range(0,1), Tooltip("색상 적용 강도입니다. 0은 원본, 1은 선택된 검은 범위를 최대로 보정합니다.")]
        public float strength;
        [Range(.05f,.45f), Tooltip("원본의 sRGB 최대 성분이 이 값 이하인 어두운 영역을 선택합니다. 유색 영역은 별도의 채도 제한으로 제외합니다.")]
        public float blackThreshold;
        [Range(.005f,.15f), Tooltip("검은 영역의 밝기 경계를 부드럽게 연결하는 폭입니다. 0에 가까울수록 경계가 선명합니다.")]
        public float feather;
        [Range(.1f,.3f), Tooltip("저채도 옷만 선택할 채도 상한입니다. 빨강·보라 장식 보호를 위해 0.3을 넘지 않습니다.")]
        public float saturationLimit;
        [Range(.05f,.4f), Tooltip("원본 기준 명도입니다. 이 밝기를 보정색 명도에 맞추고 주변의 명암 차이를 보존합니다.")]
        public float sourceLightness;
        [Range(.25f,1.5f), Tooltip("옷감 주름·명암 차이의 보존 배율입니다. 1은 원본의 지각 명도 차이를 유지합니다.")]
        public float detail;
        public static JcVillainBodyTuning Default => new JcVillainBodyTuning {
            tint=new Color(.14f,.17f,.18f,1),strength=.65f,blackThreshold=.3f,feather=.07f,
            saturationLimit=.25f,sourceLightness=.2f,detail=1
        };
        public int Signature { get { unchecked {
            int h=tint.GetHashCode();h=h*31+strength.GetHashCode();h=h*31+blackThreshold.GetHashCode();
            h=h*31+feather.GetHashCode();h=h*31+saturationLimit.GetHashCode();
            h=h*31+sourceLightness.GetHashCode();return h*31+detail.GetHashCode();
        } } }
    }

    [CreateAssetMenu(menuName="JC/캐릭터 색상/일반 빌런 몸체 프로필")]
    public sealed class JcVillainBodyProfile : ScriptableObject
    {
        public const string ResourceName="JC_VillainBody_Default";
        [Tooltip("플레이 시작 시 불러올 저장값입니다. 조절기에서는 임시 복사본을 편집합니다.")]
        public JcVillainBodyTuning tuning=JcVillainBodyTuning.Default;
        [Tooltip("검은 몸체 영역의 실시간 재질 반응입니다. 탐사·튜토리얼·일반 전투가 색상 프로필과 함께 공유합니다.")]
        public JcVillainBodySurface surface=JcVillainBodySurface.Default;
        [Tooltip("기존 건물 색상 시스템의 GPU 색상 처리 셰이더입니다. 캐릭터 재질 셰이더는 바꾸지 않습니다.")]
        public Shader processorShader;
        [Tooltip("허용한 일반 빌런의 원본 몸체 텍스처입니다. 무기 텍스처는 등록하지 않습니다.")]
        public Texture2D source;
        [Tooltip("대상으로 확인된 몸체 메시 목록입니다. 원본 텍스처와 메시가 모두 일치해야 적용합니다.")]
        public Mesh[] bodyMeshes=Array.Empty<Mesh>();
        [Tooltip("저장된 베이크가 현재 조절값과 일치하면 사용합니다. 값이 다르면 실시간 미리보기로 전환합니다.")]
        public bool useBaked;
        [Tooltip("별도 파일로 저장한 보정 기본색 텍스처입니다. 원본을 덮어쓰지 않습니다.")]
        public Texture2D bakedTexture;
        [HideInInspector] public string bakedTuning;
        // JC | 2026-11-04 | 저장 색상과 베이크가 일치할 때만 PNG를 재사용하며 임시 조절값은 명시적으로 저장합니다.
        public bool BakeMatches => bakedTexture && bakedTuning==JsonUtility.ToJson(tuning);
    }
}
