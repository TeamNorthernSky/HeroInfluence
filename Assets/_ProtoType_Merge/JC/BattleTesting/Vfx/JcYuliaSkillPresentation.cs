using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace JC.BattleTesting.Vfx
{
    // 대응: SkillPresentationDirector.RunSkillSequenceCore/RunAoESkillSequence + ResolveHitAction.
    // 애니메이터가 없는 증폭기/굴렁쇠와 정지된 장막용 연출 레일입니다.
    // 율리아 본체와 히어로는 원본 Timeline/Cue 경로를 그대로 사용합니다.
    public static class JcYuliaSkillPresentation
    {
        public static bool Supports(BattleCharactor actor, SkillData skill)
            =>actor!=null&&skill!=null&&(actor.TemplateIndex=="40002"||actor.TemplateIndex=="40003"||actor.TemplateIndex=="40004"||actor.TemplateIndex=="40005");

        public static IEnumerator Run(JcBattleManager battle,BattleCharactor actor,BattleCharactor target,SkillData skill,IReadOnlyList<BattleCharactor> targets,Action applyHit)
        {
            var director=UnityEngine.Object.FindFirstObjectByType<JcYuliaVfxDirector>();
            if(director==null||director.preset==null){applyHit?.Invoke();yield break;}
            // 특수 외형 레일도 본게임의 동일 Timeline SFX 트랙을 읽습니다.
            // 애니메이션/피해 마커는 실행하지 않아 기존 테스트 판정을 보존합니다.
            var presentation = battle.VisualDirector?.GetPresentation(skill.skillIndex);
            var timeline = presentation?.ResolveTimeline(actor.TemplateIndex, actor.UnitName);
            if (timeline != null && presentation.IsTimelineRail &&
                !(battle.PresentationCatalog?.ForceAnimatorRail ?? false))
                battle.StartCoroutine(PlayTimelineSfx(battle, actor, timeline));
            var context=new SkillEffectContext{Caster=actor,PrimaryTarget=target,Targets=targets,SocketTransform=actor.GetComponent<JcYuliaUnitVfx>()?.OrbAnchor??actor.transform,PlaybackSpeed=battle.CurrentBattleSpeed};
            bool wheel=actor.TemplateIndex=="40005",veil=actor.TemplateIndex=="40004";
            Vector3 home=actor.transform.position;
            TrailRenderer trail=null;bool hitDelivered=false;
            director.Show(veil?JcYuliaEffectKind.Recover:JcYuliaEffectKind.Charge,context.SocketTransform.position);
            yield return Wait(battle,.35f);
            try
            {
                if(wheel)
                {
                    var preset = actor.GetComponent<HoopWheelVisual>()?.Preset;
                    if (preset != null) UnitSfxProfile.PlayKey(DHAudioClipCatalog.NormalizeKey(preset.dashSfxKey));
                    Vector3 end=target!=null?target.transform.position:home;end.y=home.y;
                    trail=director.WheelTrail(home+Vector3.up*.72f);
                    float elapsed=0;
                    while(elapsed<.4f&&actor!=null)
                    {
                        elapsed+=Time.deltaTime*battle.CurrentBattleSpeed;actor.transform.position=Vector3.Lerp(home,end,Mathf.Clamp01(elapsed/.4f));
                        trail.time=.28f/Mathf.Max(.01f,battle.CurrentBattleSpeed);trail.transform.position=actor.transform.position+Vector3.up*.72f;yield return null;
                    }
                }
                else if(!veil) director.PlayAttack(context,actor.TemplateIndex=="40002");
                yield return Wait(battle,.08f);
                applyHit?.Invoke();
                hitDelivered=true;
                yield return Wait(battle,wheel?.3f:veil?.35f:.8f);
            }
            finally
            {
                if(trail!=null){trail.emitting=false;UnityEngine.Object.Destroy(trail.gameObject,trail.time+.05f);}
                // 400051은 SelfDestructRowAoEHandler가 반격 처리 후 사망시킵니다.
                // 논리 셀은 그대로 두고 도착 위치를 유지하여 파괴 VFX가 원래 칸으로 순간 이동하지 않게 합니다.
                // 시전 취소·다른 스킬이면 기존 접근 연출과 동일하게 원위치를 복구합니다.
                if(actor!=null&&(!wheel||skill.skillIndex!=400051||!hitDelivered))actor.transform.position=home;
            }
        }
        private static IEnumerator Wait(JcBattleManager battle,float seconds)
        {for(float t=0;t<seconds;t+=Time.deltaTime*Mathf.Max(.01f,battle.CurrentBattleSpeed))yield return null;}

        private static IEnumerator PlayTimelineSfx(JcBattleManager battle, BattleCharactor actor, TimelineAsset timeline)
        {
            var markers = timeline.GetOutputTracks().OfType<PresentationMarkerTrack>()
                .Where(track => !track.mutedInHierarchy && track.Role == PresentationMarkerTrackRole.Sfx)
                .SelectMany(track => track.GetMarkers().OfType<PresentationSfxMarker>())
                .OrderBy(marker => marker.time).ToArray();
            float elapsed = 0f;
            int index = 0;
            while (index < markers.Length && actor != null && actor.gameObject.activeInHierarchy && !actor.IsDead)
            {
                while (index < markers.Length && markers[index].time <= elapsed)
                {
                    var marker = markers[index++];
                    if (string.IsNullOrEmpty(marker.SfxKey)) continue;
                    if (marker.DurationSeconds > 0f) PresentationSfxPlayer.PlayForDuration(marker.SfxKey, marker.DurationSeconds);
                    else PresentationSfxPlayer.Play(marker.SfxKey);
                }
                yield return null;
                elapsed += Time.deltaTime * Mathf.Max(.01f, battle.CurrentBattleSpeed);
            }
        }
    }
}
