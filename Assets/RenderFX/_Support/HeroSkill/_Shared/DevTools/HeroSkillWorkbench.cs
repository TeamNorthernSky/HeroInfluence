using System.Linq;
using UnityEngine;
using JC.VFX.Seam;

namespace JC.VFX
{
    /// <summary>씬의 편집 진입점. 값은 공용 부품·프리셋 에셋에 저장합니다.</summary>
    public sealed class HeroSkillWorkbench : MonoBehaviour
    {
        [Tooltip("실제 연출 연결과 동일한 부품 대장입니다.")]
        public JcSkillPartsManifest manifest;
        [Tooltip("이 작업대가 조절하는 기본 또는 강화 스킬 ID입니다.")]
        public int skillIndex;
        [Tooltip("실제 시전 경로의 스킬 선택 키입니다. 선택 후 Game 뷰에서 대상을 클릭합니다.")]
        public KeyCode key;
        [Tooltip("현재 테스트 씬의 스킬 입력 담당입니다.")]
        public HeroSkillPreviewRig rig;
        public JcSkillPartsManifest.Skill Skill => manifest?.skills.FirstOrDefault(s => s.skillIndex == skillIndex);
    }
}
