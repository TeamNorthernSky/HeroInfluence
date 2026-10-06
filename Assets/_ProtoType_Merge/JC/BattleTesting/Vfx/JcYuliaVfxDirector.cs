using System.Collections;
using UnityEngine;

namespace JC.BattleTesting.Vfx
{
    // 대응: ASB BattleVisualDirector/UnitEffectPresenter, MadonnaSkillReceiveTimeline.PlayReceive.
    // 상태 이벤트와 VfxEffect 사이의 JC 씬 전용 연결입니다. 원본 프리팹·전투 저장소는 수정하지 않습니다.
    public sealed class JcYuliaVfxDirector : MonoBehaviour
    {
        [Tooltip("율리아의 상태·소환·장막 연출 설정입니다. JC 전용 .asset으로 보존합니다.")]
        public JcYuliaVfxPreset preset;
        [Tooltip("증폭기 공격에 사용하는 기존 폭풍 프리팹의 읽기 참조입니다. 원본 에셋은 수정하지 않습니다.")]
        public GameObject storm;
        [Tooltip("증폭기 공격에 사용하는 관통 번개 프리팹입니다. 피해 판정은 전투 매니저가 별도로 확정합니다.")]
        public GameObject beam;
        private Transform effects;
        private JcBattleTestSession session;
        private JcEncounterBlackboard subscribed;

        public void Begin(JcBattleTestSession owner)
        {
            if (subscribed != null) subscribed.YuliaSkillReserved -= Supply;
            session = owner; subscribed = owner.blackboard;
            subscribed.YuliaSkillReserved += Supply;
            if (effects == null) { effects = new GameObject("JC_YuliaTransientVfx").transform; effects.SetParent(transform,false); }
        }
        public void ClearEffects()
        {
            if (effects == null) return;
            foreach (Transform child in effects) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        public void Attach(BattleCharactor actor)
        {
            if (preset == null || actor == null) return;
            var state = actor.gameObject.AddComponent<JcYuliaUnitVfx>(); state.Configure(actor,this);
        }
        // 대응: EncounterBlackboard 에너지 축적 / JC 기획 보완 ChargeStack. 조회만 하고 스택을 변경하지 않습니다.
        public int VisualCharge(BattleCharactor actor)
        {
            if (actor == null) return 0;
            if (session?.Rules != null && session.Rules.Phase == 3 && (actor.TemplateIndex == "40002" || actor.TemplateIndex == "40003"))
                return session.Rules.GetCharge(int.Parse(actor.TemplateIndex));
            return actor.EnergyStack;
        }
        public JcYuliaVfxEffect Show(JcYuliaEffectKind kind, Vector3 position, Transform parent = null, bool loop = false, Transform from = null, Transform to = null)
        {
            if (preset == null) return null;
            var go = new GameObject("JC_Yulia_"+kind); go.transform.SetParent(parent != null ? parent : effects, true); go.transform.position=position;
            var effect = go.AddComponent<JcYuliaVfxEffect>(); effect.preset=preset; effect.kind=kind; effect.loop=loop;
            effect.PlaybackSpeed = session?.execution != null ? session.execution.CurrentBattleSpeed : 1;
            effect.Play(from,to); return effect;
        }
        public void PlayAttack(SkillEffectContext context, bool isStorm)
        {
            var prefab = isStorm ? storm : beam;
            if (prefab == null) return;
            var go=Instantiate(prefab,context.Caster.transform.position,Quaternion.identity,effects);
            go.GetComponent<ISkillEffectBehaviour>()?.Play(context);
        }
        // 대응: HeroSkill의 투사체 TrailRenderer 수명 관리. 소환체 돌진의 이동 이력만 그립니다.
        public TrailRenderer WheelTrail(Vector3 position)
        {
            var go=new GameObject("JC_WheelDashTrail");go.transform.SetParent(effects,true);go.transform.position=position;
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=preset.energyMaterial;
            trail.time=.28f/Mathf.Max(.01f,session.execution.CurrentBattleSpeed);trail.minVertexDistance=.03f;
            trail.widthMultiplier=preset.lineWidth*2;trail.numCapVertices=4;
            trail.startColor=preset.core;var end=preset.energy;end.a=0;trail.endColor=end;
            trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
            return trail;
        }
        private void Supply(int socket, BattleCharactor source)
        {
            if (source == null || session == null || session.Rules.Phase >= 3) return;
            foreach (var unit in session.flow.Participants)
                if (unit != null && unit.TemplateIndex == "40001" && !unit.IsDead)
                {
                    var from = source.GetComponent<JcYuliaUnitVfx>()?.OrbAnchor ?? source.transform;
                    Show(JcYuliaEffectKind.Supply,from.position,from:from,to:unit.transform);
                    Show(JcYuliaEffectKind.Charge,unit.transform.position+Vector3.up*.85f);
                    return;
                }
        }
        public IEnumerator PhaseEntry(int phase, BattleCharactor boss)
        {
            if (boss == null || preset == null) yield break;
            Show(JcYuliaEffectKind.Recover,boss.transform.position);
            if (phase == 3)
                foreach (var amp in session.flow.Participants)
                    if (amp != null && !amp.IsDead && (amp.TemplateIndex=="40002"||amp.TemplateIndex=="40003"))
                        Show(JcYuliaEffectKind.Supply,amp.transform.position,from:amp.GetComponent<JcYuliaUnitVfx>()?.OrbAnchor??amp.transform,to:boss.transform);
            float elapsed=0;
            while(elapsed<preset.phaseSeconds) { elapsed+=Time.deltaTime*Mathf.Max(.01f,session.execution.CurrentBattleSpeed);yield return null; }
        }
        private void LateUpdate()
        {
            if(session?.execution==null||effects==null)return;
            foreach(var fx in effects.GetComponentsInChildren<JC.VFX.VfxEffect>()) fx.PlaybackSpeed=session.execution.CurrentBattleSpeed;
        }
        private void OnDisable() { if(subscribed!=null)subscribed.YuliaSkillReserved-=Supply; }
    }
}
