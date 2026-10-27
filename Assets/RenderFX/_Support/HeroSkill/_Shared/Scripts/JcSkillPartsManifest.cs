using System;
using UnityEngine;

namespace JC.VFX
{
    [CreateAssetMenu(menuName = "JC/VFX/스킬별 부품 연결표")]
    public sealed class JcSkillPartsManifest : ScriptableObject
    {
        [Serializable] public sealed class Part
        {
            [Tooltip("부품 대장의 호출 이름입니다.")] public string cue;
            [Tooltip("이 스킬에서의 역할과 임시 표현 여부입니다.")] public string role;
            [Tooltip("독립 검사와 실제 조립에서 사용하는 동일 부품입니다.")] public GameObject prefab;
            [Tooltip("후속 신호가 필요한 부품이면 독립 검사에서 발사까지 진행합니다.")] public bool signal;
        }
        [Serializable] public sealed class Skill
        {
            [Tooltip("기본 또는 강화 스킬 ID입니다.")] public int skillIndex;
            [Tooltip("캐릭터·스킬·기본/강화 표시입니다.")] public string label;
            [Tooltip("실제 전투의 연출 연결 데이터입니다.")] public SkillPresentationData presentation;
            [Tooltip("이 스킬을 이루는 독립 부품 목록입니다.")] public Part[] parts;
        }
        [Tooltip("4캐릭터 16계열의 기본/강화 32개를 명시적으로 등록합니다.")]
        public Skill[] skills = Array.Empty<Skill>();
    }
}
