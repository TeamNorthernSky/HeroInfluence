using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-11 | 두 원본의 초안만 소유하는 모든 전투 공통 조절기입니다. 카메라는 기존 공용 연출기만 씁니다.
    [DisallowMultipleComponent]
    public sealed class JcBlackBulletCameraControls : MonoBehaviour
    {
        [InspectorName("공통 저장 프로필"), Tooltip("모든 전투가 공유합니다. 저장 전까지 원본을 변경하지 않습니다.")]
        public JcBlackBulletCameraProfile profile;
        JcBlackBulletCameraProfile draft, draftSource;
        JcBankSkillCameraProfile authoredDraft, authoredSource;
        [HideInInspector] public int editingSkill, editingTarget;
        // JC | 2026-11-16 | 공통 Inspector에서 보스 기본뷰 상대 변환과 공유 저장 범위를 안내하기 위해 전장 연결을 읽습니다.
        public bool UsesBossBasis
        {
            get
            {
                foreach(var root in gameObject.scene.GetRootGameObjects())
                {
                    if(root.GetComponentInChildren<JcBossCamera>(true)) return true;
                    var test=root.GetComponentInChildren<JcStandaloneBattleTest>(true);
                    if(test && test.scenario==JcStandaloneBattleTest.Scenario.Boss)return true;
                }
                return false;
            }
        }
        public string Phase { get; private set; } = "대기 · 블랙불릿 공통 카메라";
        public JcBlackBulletCameraProfile Draft
        {
            get
            {
                if (draftSource != profile) ReleaseDraft();
                if (!draft && profile)
                {
                    draft = Instantiate(profile);
                    draft.name = profile.name + " (임시 조절값)";
                    draftSource = profile;
                }
                if (draft) draft.hideFlags = HideFlags.DontSave;
                return draft;
            }
        }
        public JcBankSkillCameraProfile AuthoredDraft
        {
            get
            {
                var source = Draft ? Draft.authoredProfile : null;
                if (source != authoredSource) { Release(authoredDraft); authoredDraft = null; authoredSource = source; }
                if (!authoredDraft && source)
                {
                    authoredDraft = Instantiate(source);
                    authoredDraft.name = source.name + " (정본1 임시 조절값)";
                    authoredDraft.hideFlags = HideFlags.DontSave;
                }
                return authoredDraft;
            }
        }
        public JcBankSkillCameraProfile.Shot EditingShot => AuthoredDraft ? AuthoredDraft.GetSlot(editingSkill, editingTarget) : null;
        public void SetPhase(string phase) => Phase = phase;
        public void ReloadProfile()
        {
            if (profile && Draft) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), draft);
            if (AuthoredDraft && authoredSource) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(authoredSource), authoredDraft);
            Reapply();
        }
        // JC | 2026-11-11 | 두 Inspector가 같은 갱신 진입점을 사용합니다. 정지 중에도 현재 진행률만 재적용하고 시간을 적립하지 않습니다.
        public void Reapply()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var presentation in root.GetComponentsInChildren<BattlePresentationController>(true))
                    if (presentation.isActiveAndEnabled && presentation.blackBulletCameraControls == this)
                        presentation.RefreshBlackBulletCameraPreview();
        }
        void OnDestroy() => ReleaseDraft();
        void ReleaseDraft()
        {
            Release(draft); Release(authoredDraft);
            draft = null; draftSource = null; authoredDraft = null; authoredSource = null;
        }
        static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
