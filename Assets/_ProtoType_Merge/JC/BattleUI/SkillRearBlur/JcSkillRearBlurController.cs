using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace JC.BattleUI
{
    // JC | 2026-11-11 | 전투의 고정 진형 기준으로 월드 후방 평면을 갱신합니다. 전투 판정·카메라 Transform·기존 탐사 Volume은 수정하지 않습니다.
    [ExecuteAlways, DefaultExecutionOrder(1000), DisallowMultipleComponent]
    public sealed class JcSkillRearBlurController : MonoBehaviour
    {
        [InspectorName("공통 저장 프로필"), Tooltip("두 전투씬이 공유합니다. 플레이 중에는 초안만 편집하고 저장 버튼으로 보존합니다.")]
        public JcSkillRearBlurProfile profile;
        [Tooltip("이 씬의 기존 공통 스킬 카메라 담당자입니다.")]
        public BattlePresentationController presentation;
        [InspectorName("강제 미리보기"), Tooltip("스킬 밖에서도 연결된 게임 카메라로 비교합니다. 점령·은행·보스 제외는 유지합니다. 프로필에는 저장하지 않습니다.")]
        public bool forcePreview;
        // JC | 2026-11-11 | 초안만 유지하며 개체/이펙트 Renderer 목록과 카메라 기준 깊이 캐시는 제거합니다.
        JcSkillRearBlurProfile draft, source;
        BattleCharactor caster;
        BattleManager battleClock;
        PlayableDirector timeline;
        double highWater;
        int clockFrame = -1;
        bool active, returning, timelineActive;
        float weight;
        static readonly List<JcSkillRearBlurController> owners = new List<JcSkillRearBlurController>();
        public float Weight => forcePreview ? 1 : weight;
        public Vector4 RearPlane { get; private set; }
        public bool HasRearReference { get; private set; }
        public Vector4 CurrentDistances { get; private set; }
        public string Status { get; private set; } = "대기";
        public Camera TargetCamera => presentation ? presentation.previewCamera : null;
        public JcSkillRearBlurProfile Draft
        {
            get
            {
                if (source != profile) ReleaseDraft();
                if (!draft && profile) { draft = Instantiate(profile); draft.hideFlags = HideFlags.DontSave; source = profile; }
                return draft;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOwners() => owners.Clear();
        void OnEnable() { if (!owners.Contains(this)) owners.Add(this); }
        void OnDisable() { owners.Remove(this); Cancel(); }
        void OnDestroy() => ReleaseDraft();
        void ReleaseDraft()
        {
            if (draft) { if (Application.isPlaying) Destroy(draft); else DestroyImmediate(draft); }
            draft = null; source = null;
        }
        public void ReloadProfile()
        {
            if (profile && Draft) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), draft);
            RefreshNow();
        }
        public bool EnvironmentAllowed
        {
            get
            {
                if (!presentation || presentation.BankBackground || presentation.BossCamera) return false;
                var context = CombatContext.Instance;
                if (!context) return true;
                if (context.IsTutorial && context.TutorialBattleKey == "TUT_02") return false;
                var type = context.CombatEnemy?.SourceType;
                return type != CombatEnemySourceType.OutpostDefender && type != CombatEnemySourceType.VillainUnionDefender;
            }
        }
        public void BeginAttack(BattleCharactor actor, BattleCharactor target, SkillData skill)
        {
            Cancel();
            if (!isActiveAndEnabled || !EnvironmentAllowed || !Draft || !Draft.useBlur || !actor || !target || skill == null ||
                (skill.skillKey?.StartsWith("HCS", StringComparison.Ordinal) ?? false) || BattleManager.IsMeleeSkillRange(actor, skill)) return;
            caster = actor; clockFrame = -1;
            battleClock = presentation.battle ? presentation.battle.GetComponent<BattleManager>() : null;
            if (!battleClock)
                foreach (var root in gameObject.scene.GetRootGameObjects()) { battleClock = root.GetComponentInChildren<BattleManager>(); if (battleClock) break; }
            active = true;
            weight = Draft.enterSeconds <= 0 ? 1 : 0;
            RefreshNow();
        }
        public void BeginReturn() { returning = true; timeline = null; timelineActive = false; }
        public void Cancel()
        {
            active = returning = timelineActive = false; timeline = null; caster = null; weight = 0;
            Status = "대기";
        }
        // JC | 2026-11-11 | 기존 Timeline 시작/종료 알림을 읽기만 합니다. 배속을 중복 적용하거나 Director를 움직이지 않습니다.
        public void BeginTimeline(BattleCharactor actor, PlayableDirector director, double time)
        {
            if (!active || actor != caster) return;
            timeline=director;highWater=time;timelineActive=true;
        }
        public void EndTimeline(BattleCharactor actor, PlayableDirector director)
        {
            if (!active || actor != caster || timeline != director) return;
            Advance(ReadDelta()); timeline=null;timelineActive=false;
        }
        float ReadDelta()
        {
            // JC | 2026-11-11 | Timeline 종료와 LateUpdate가 같은 프레임에 와도 시간을 두 번 누적하지 않습니다.
            if (clockFrame == Time.frameCount) return 0;
            clockFrame = Time.frameCount;
            bool running=Time.timeScale>0 && (!battleClock || battleClock.CurrentBattleSpeed>0);
            if (timelineActive)
            {
                double time=timeline?timeline.time:highWater;
                float delta=(float)Math.Max(0,time-highWater);highWater=Math.Max(highWater,time);
                return running?delta:0;
            }
            float hitStop=caster && caster.Anim!=null && !returning?caster.Anim.HitStopSpeedMultiplier:1;
            return running?Time.deltaTime*(battleClock?Mathf.Max(0,battleClock.CurrentBattleSpeed):1)*Mathf.Max(0,hitStop):0;
        }
        void LateUpdate()
        {
            if (!owners.Contains(this)) owners.Add(this); // JC | 2026-11-11 | 도메인 리로드를 끈 플레이에서도 등록을 복구합니다.
            if (active && Application.isPlaying) Advance(ReadDelta());
            RefreshNow();
        }
        public void Advance(float delta)
        {
            if (!active || !Draft) return;
            float duration=returning?Draft.exitSeconds:Draft.enterSeconds;
            weight=duration<=0?(returning?0:1):Mathf.MoveTowards(weight,returning?0:1,Mathf.Max(0,delta)/duration);
        }
        public void RefreshNow()
        {
            HasRearReference = false;
            if (!Draft || !TargetCamera) { Status="프로필·전투 카메라 연결 필요";return; }
            if (!EnvironmentAllowed) { Status="점령·은행·보스 제외";return; }
            // JC | 2026-11-11 | 캐릭터/카메라 이동과 무관한 진형 중심 평면입니다. 입력 연결이 없으면 미리보기도 실행하지 않습니다.
            HasRearReference = presentation.playerPlace && presentation.enemyPlace;
            if (!HasRearReference) { Status = "아군·적군 진형 기준 연결 필요"; return; }
            Vector3 direction = Vector3.ProjectOnPlane(presentation.enemyPlace.position - presentation.playerPlace.position, Vector3.up);
            HasRearReference = direction.sqrMagnitude > .0001f;
            if (!HasRearReference) { Status = "아군·적군 진형 기준이 겹칩니다"; return; }
            direction.Normalize();
            RearPlane = new Vector4(direction.x, direction.y, direction.z, -Vector3.Dot(direction, presentation.enemyPlace.position));
            CurrentDistances = Draft.Distances();
            Status=!Draft.useBlur?"사용 안 함":forcePreview?"강제 미리보기":active?(returning?"카메라 복귀 · 해제":"원거리 스킬 · 후방 흐림"):"대기";
        }
        public static JcSkillRearBlurController ForCamera(Camera camera)
        {
            foreach(var owner in owners)
                if (owner && owner.isActiveAndEnabled && owner.TargetCamera==camera && owner.EnvironmentAllowed && owner.Draft &&
                    owner.Draft.useBlur && (owner.active || owner.forcePreview) && owner.Weight>0) { owner.RefreshNow(); if (owner.HasRearReference) return owner; }
            return null;
        }
    }
}
