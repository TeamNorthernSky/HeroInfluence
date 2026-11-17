using System.Collections.Generic;
using UnityEngine;
namespace JC.BattleTesting
{
    public sealed partial class JcIndoorEnvironment
    {
        // JC | 2026-11-17 | 네 시간대의 미저장 초안은 전환 중에도 보존하고 저장은 전문 인스펙터 버튼에서만 수행합니다.
        public JcIndoorPeriodProfiles PeriodProfiles { get; private set; }
        public int PeriodIndex { get; private set; }
        public bool FollowTime { get; set; } = true;
        JC.Environment.JcDayNightProfile periodClock;
        readonly Dictionary<Object,Object> periodDrafts = new Dictionary<Object,Object>();
        int ResolvePeriod()
        {
            // JC | 2026-11-17 | 고정 실내인 은행 테스트는 월드 시계를 켜지 않고 테스트의 네 시간대 선택을 따릅니다.
            var test=bankSettings ? JcStandaloneBattleTest.ForScene(gameObject.scene) : null;
            if(test && test.scenario==JcStandaloneBattleTest.Scenario.Bank) return Mathf.Clamp(test.period,0,3);
            if (!periodClock) return 0;
            periodClock.SegmentPair(JC.Environment.JcDayNightController.CurrentHours,out int index,out _,out _);
            return index;
        }
        public IEnumerable<(Object source,Object draft)> DraftPairs()
        {
            if (Source && Draft) yield return (Source,Draft);
            if (BossMaterials) {
                if(BossMaterials.GlassProfile && BossMaterials.GlassDraft) yield return (BossMaterials.GlassProfile,BossMaterials.GlassDraft);
                if(BossMaterials.SurfaceSource && BossMaterials.SurfaceDraft) yield return (BossMaterials.SurfaceSource,BossMaterials.SurfaceDraft);
            }
            if(BankMaterials && BankMaterials.Source && BankMaterials.Draft) yield return (BankMaterials.Source,BankMaterials.Draft);
            if(Toon && Toon.Source && Toon.Draft) yield return (Toon.Source,Toon.Draft);
            if(Contact && Contact.Source && Contact.Draft) yield return (Contact.Source,Contact.Draft);
        }
        public static void CopyDraft(Object source,Object destination)
        {
            if(source is Material material && destination is Material target) JcBossBackgroundMaterials.CopyGlassValues(material,target);
            else JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source),destination);
        }
        static Object CloneDraft(Object source)
        {
            var clone=Instantiate(source);clone.hideFlags=HideFlags.DontSave;
            CopyDraft(source,clone);return clone;
        }
        public void RememberDrafts()
        {
            foreach(var pair in DraftPairs()) {
                if(!periodDrafts.TryGetValue(pair.source,out var snapshot) || !snapshot) {
                    snapshot=CloneDraft(pair.draft); periodDrafts[pair.source]=snapshot;
                } else CopyDraft(pair.draft,snapshot);
            }
        }
        public void SelectPeriod(int index,bool followTime=false)
        {
            if(index<0 || index>3) return;
            FollowTime=followTime;
            if(index==PeriodIndex || !PeriodProfiles) return;
            var period=PeriodProfiles.Get(index); if(period==null) return;
            RememberDrafts(); PeriodIndex=index;
            if(Boss) Boss.SelectProfile(period.bossLighting);
            if(Bank) Bank.SelectProfile(period.bankLighting);
            if(BossMaterials) { BossMaterials.SelectGlassProfile(period.glass); BossMaterials.SelectSurfaceProfile(period.surfaces); }
            if(BankMaterials) BankMaterials.SelectProfile(period.bankMaterials);
            if(Toon) Toon.SelectProfile(period.toon);
            if(Contact) Contact.SelectProfile(period.contact);
            RestoreRememberedDrafts();
        }
        public void RestoreRememberedDrafts()
        {
            foreach(var pair in DraftPairs()) if(periodDrafts.TryGetValue(pair.source,out var snapshot) && snapshot) CopyDraft(snapshot,pair.draft);
            ApplyAllDrafts();
        }
        // JC | 2026-11-17 | 일괄 저장값 불러오기와 시간대 전환이 같은 실제 적용 경로를 사용합니다.
        public void ApplyAllDrafts()
        {
            if(BankMaterials) BankMaterials.ApplyDraft();
            if(BossMaterials) { BossMaterials.ApplyGlass(); BossMaterials.ApplySurfaces(); }
            ApplyDraft();
        }
#if UNITY_EDITOR
        // JC | 2026-11-17 | 씬 저장 전 미리보기 정리 후에도 초안을 보존하여 임시 재질이 씬에 직렬화되지 않게 합니다.
        public Dictionary<Object,Object> CaptureEditorDrafts()
        {
            RememberDrafts(); var result=new Dictionary<Object,Object>();
            foreach(var pair in periodDrafts) { var value=CloneDraft(pair.Value);result.Add(pair.Key,value); }
            return result;
        }
        public void RestoreEditorDrafts(Dictionary<Object,Object> snapshots)
        {
            if(snapshots==null)return;
            foreach(var pair in snapshots) if(pair.Key && pair.Value) {
                if(periodDrafts.TryGetValue(pair.Key,out var old) && old) DestroyImmediate(old);
                var value=CloneDraft(pair.Value);periodDrafts[pair.Key]=value;
            }
            RestoreRememberedDrafts();
        }
        public void ReleaseEditorPreview()
        {
            // 전문 적용기의 역순 정리를 기존 원경 미리보기와 동일하게 명시 호출합니다.
            if(Toon) Toon.ReleaseEditorPreview();
            if(Boss) EditorCleanup(Boss,"OnDisable");
            if(Bank) Bank.ReleaseEditorPreview();
            if(Contact) Contact.ReleaseEditorPreview();
            if(BossMaterials) EditorCleanup(BossMaterials,"OnDestroy");
            if(BankMaterials) BankMaterials.ReleaseEditorPreview();
            if(Boss) EditorCleanup(Boss,"OnDestroy");
            OnDestroy();
        }
        public static void EditorCleanup(MonoBehaviour target,string method)
        {
            var info=target.GetType().GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            if(info==null)throw new System.MissingMethodException(target.GetType().FullName,method);
            info.Invoke(target,null);
        }
#endif
        void ReleasePeriodDrafts()
        {
            foreach(var value in periodDrafts.Values) if(value) { if(Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
            periodDrafts.Clear();
        }
    }
}
