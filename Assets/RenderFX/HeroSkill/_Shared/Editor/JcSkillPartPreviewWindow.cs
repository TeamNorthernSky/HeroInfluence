using System.Linq;
using UnityEngine;
using UnityEditor;
using JC.VFX.Seam;
namespace JC.VFX.EditorTools
{
    public sealed class JcSkillPartPreviewWindow : EditorWindow
    {
        private JcSkillPartsManifest manifest;
        private int skill, part;
        private BattleCharactor caster, target;
        private GameObject instance;
        private float speed = 1f;
        private SkillEffectContext context;
        public void PlayPart(JcSkillPartsManifest.Part item, HeroSkillPreviewRig rig)
        {
            caster = rig != null ? rig.Actor : null;
            target = null;
            Play(item);
        }
        public void SignalPart()
        {
            if(instance!=null) foreach(var h in instance.GetComponents<MonoBehaviour>().OfType<ISkillEffectHandle>()) h.Signal(context);
        }
        public void StopPart() => Stop();
        [MenuItem("JC VFX/스킬 부품 검사")]
        public static void Open() => GetWindow<JcSkillPartPreviewWindow>("스킬 부품 검사");
        private void OnEnable()
        {
            manifest = AssetDatabase.LoadAssetAtPath<JcSkillPartsManifest>("Assets/RenderFX/_Seam/Data/Parts/JC_SkillParts.asset");
            EditorApplication.playModeStateChanged += OnPlayChanged;
        }
        private void OnDisable() { Stop(); EditorApplication.playModeStateChanged -= OnPlayChanged; }
        private void OnPlayChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Stop(); }
        private void OnGUI()
        {
            manifest = (JcSkillPartsManifest)EditorGUILayout.ObjectField(new GUIContent("연결표", "스킬별 기본/강화와 실제 부품 참조를 담은 에셋입니다."), manifest, typeof(JcSkillPartsManifest), false);
            if (manifest == null || manifest.skills.Length == 0) return;
            skill = EditorGUILayout.Popup(new GUIContent("스킬", "검사할 실제 스킬 ID입니다. 각 항목이 기본/강화를 명시합니다."), Mathf.Clamp(skill,0,manifest.skills.Length-1),manifest.skills.Select(s=>s.skillIndex+" "+s.label).ToArray());
            var selected=manifest.skills[skill];
            part = EditorGUILayout.Popup(new GUIContent("부품", "선택 부품에서 재생을 시작합니다. 부품에 연결된 후속 효과도 함께 호출될 수 있습니다."), Mathf.Clamp(part,0,selected.parts.Length-1), selected.parts.Select(p=>p.cue).ToArray());
            var item=selected.parts[part];
            EditorGUILayout.HelpBox(item.role, MessageType.Info);
            caster=(BattleCharactor)EditorGUILayout.ObjectField(new GUIContent("시전자", "테스트 씬의 시전자입니다. 비우면 현재 프리뷰 시전자를 찾습니다."), caster,typeof(BattleCharactor),true);
            target=(BattleCharactor)EditorGUILayout.ObjectField(new GUIContent("대상", "공격/회복/부활 부품의 목표 유닛입니다. 비우면 적 1명을 사용합니다."), target,typeof(BattleCharactor),true);
            speed=EditorGUILayout.Slider(new GUIContent("배속", "배속 주입을 지원하는 부품에 적용합니다. 기존 부품의 내부 파티클 시간은 별도이며, 전투 판정이나 씬 시간은 바꾸지 않습니다."),speed,.25f,2f);
            if(GUILayout.Button(new GUIContent("부품 에셋 선택", "Project 창에서 해당 부품을 선택합니다. 위치·크기·시간과 참조를 Inspector에서 조절할 수 있습니다."))) Selection.activeObject=item.prefab;
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if(GUILayout.Button(new GUIContent("선택 부품 재생", "Play 중 현재 부품 하나만 생성합니다. 피해·회복 판정은 실행하지 않습니다."))) Play(item);
                if(GUILayout.Button(new GUIContent("발사 신호", "차징 부품에 후속 발사 신호를 줍니다. 재생된 부품이 신호를 지원할 때만 동작합니다.")))
                    if(instance!=null) foreach(var h in instance.GetComponents<MonoBehaviour>().OfType<ISkillEffectHandle>())h.Signal(context);
                if(GUILayout.Button(new GUIContent("중단·정리", "검사에서 생성한 부품과 하위 효과를 정리합니다.")))Stop();
            }
            if(!EditorApplication.isPlaying)EditorGUILayout.HelpBox("캐릭터 테스트 씬 2~5 중 하나를 Play한 뒤 사용하세요.",MessageType.Info);
        }
        private void Play(JcSkillPartsManifest.Part part)
        {
            Stop();
            if (part.prefab == null) { Debug.LogWarning("[스킬 부품 검사] 부품 프리팹을 연결해 주세요."); return; }
            if(caster==null)caster=FindFirstObjectByType<HeroSkillPreviewRig>()?.Actor;
            if(caster==null)return;
            if(target==null)target=FindObjectsByType<BattleCharactor>(FindObjectsSortMode.None).FirstOrDefault(u=>u.IsPlayer!=caster.IsPlayer&&!u.IsDead);
            if(target==null)target=caster;
            bool atTarget=part.cue.Contains("impact")||part.cue.EndsWith("land")||part.cue.EndsWith("shock")||part.cue.EndsWith("revive");
            Vector3 position=(atTarget?target:caster).transform.position;
            if(part.prefab.GetComponent<FlareBombImpact>()!=null)position+=Vector3.up*.8f;
            instance=Instantiate(part.prefab,position,Quaternion.identity);
            context=new SkillEffectContext{Caster=caster,PrimaryTarget=target,ReviveTarget=target,SpawnPosition=position,TargetPosition=target.transform.position,SocketTransform=caster.transform,PlaybackSpeed=speed,Targets=FindObjectsByType<BattleCharactor>(FindObjectsSortMode.None).Where(u=>u.IsPlayer==target.IsPlayer&&!u.IsDead).ToArray()};
            var fx=instance.GetComponent<VfxEffect>();
            var behaviour=instance.GetComponents<MonoBehaviour>().OfType<ISkillEffectBehaviour>().FirstOrDefault();
            if(fx!=null){fx.PlaybackSpeed=speed;fx.SetTargets(context.Targets.Select(u=>VfxTarget.Of(u.transform)).ToArray());}
            if(behaviour!=null)behaviour.Play(context);else if(fx!=null)fx.Play(caster.transform,target.transform);
        }
        private void Stop()
        {
            if(instance==null)return;
            foreach(var h in instance.GetComponents<MonoBehaviour>().OfType<ISkillEffectHandle>())h.Stop();
            if(instance!=null){var fx=instance.GetComponent<VfxEffect>();if(fx!=null)fx.Stop();DestroyImmediate(instance);}instance=null;
        }
    }
}
