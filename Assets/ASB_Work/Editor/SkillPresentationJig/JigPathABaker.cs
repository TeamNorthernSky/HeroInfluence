using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// 스킬 연출 + 캐릭터 프리팹 → Path A 런타임 Variant TimelineAsset을 굽는다(지시서 §9).
    ///
    /// - 캐릭터의 주 스킬 클립을 Animation Track에 넣는다(단일 클립 — 저작자가 Split/속도/블렌드로 다듬는다).
    /// - 스킬의 각 Cue를 <see cref="PresentationSignalMarker"/>(Kind=Cue, CueName 채움)로 미리 놓는다.
    /// - 투사체가 있으면 Projectile 마커도 하나 미리 놓는다.
    /// - <see cref="SkillPresentationData.SkillTimelines"/>에 연결하고 AnimationRail=Timeline으로 바꾼다.
    ///
    /// ★런타임 타입(PresentationSignalMarker)만 쓴다 — 에디터 전용 CueMarker는 빌드에서 깨지므로 굽지 않는다.
    /// ★결과물은 런타임 폴더(Editor 아님)에 저장해 빌드에 포함되게 한다.
    /// </summary>
    public static class JigPathABaker
    {
        private const string RuntimeFolderParent = "Assets/ASB_Work/Skills";
        private const string RuntimeFolderName = "Timelines";
        private const string RuntimeFolder = RuntimeFolderParent + "/" + RuntimeFolderName;

        public static TimelineAsset Bake(SkillPresentationData data, GameObject characterPrefab, out string error)
        {
            error = null;
            if (data == null) { error = "연출 자산이 비어 있습니다."; return null; }
            if (characterPrefab == null) { error = "캐릭터 프리팹이 비어 있습니다."; return null; }

            // 1) 캐릭터 키(unitName) + Animator
            string characterKey = ResolveCharacterKey(characterPrefab);
            Animator animator = characterPrefab.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                error = "캐릭터 프리팹에 Animator/AnimatorController가 없습니다.";
                return null;
            }

            // 2) 주 스킬 상태 → 클립 해석(에디터 전용 JigStateResolver — 굽는 시점에만 실행)
            string stateName = ResolvePrimaryStateName(data);
            if (string.IsNullOrWhiteSpace(stateName))
            {
                error = "스킬의 AnimationStateName을 찾지 못했습니다(Attack.Beats[0].AnimationStateName 또는 AnimationStateName).";
                return null;
            }

            JigStateResolution res = JigStateResolver.Resolve(animator.runtimeAnimatorController, stateName);
            if (!res.Success || res.Clip == null)
            {
                error = $"'{stateName}' 클립 해석 실패: {res.FailureReason}";
                return null;
            }
            AnimationClip clip = res.Clip;
            float clipLen = clip.length > 0f ? clip.length : 1f;

            // 3) 런타임 폴더 + 결정적 경로. GenerateUniqueAssetPath로 같은 스킬 Variant를 계속 복제하지 않는다.
            EnsureFolder();
            string safeKey = string.IsNullOrEmpty(characterKey) ? "Any" : SanitizeFileName(characterKey);
            string path = $"{RuntimeFolder}/Skill{data.SkillIndex}_{safeKey}.playable";
            TimelineAsset existing = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (existing != null)
            {
                error = $"이미 Runtime Timeline Variant가 있습니다: {path}\n기존 Variant를 열어 편집하거나 명시적으로 이름을 바꾼 뒤 다시 시도하세요.";
                return null;
            }

            // 4) TimelineAsset + Animation Track + 클립
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);

            var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Animation");
            TimelineClip tlClip = animTrack.CreateClip(clip);
            tlClip.start = 0;
            tlClip.duration = clipLen;

            // 5) Cue 마커 미리 배치(PresentationSignalMarker)
            timeline.CreateMarkerTrack();
            MarkerTrack markerTrack = timeline.markerTrack;

            // 전체 Timeline의 기본 공격 구간. 구간 시간의 원본은 이 Marker 둘뿐이다.
            var sectionStart = markerTrack.CreateMarker<PresentationSectionMarker>(0d);
            sectionStart.Configure("Attack", PresentationSectionBoundary.Start);
            var sectionEnd = markerTrack.CreateMarker<PresentationSectionMarker>(clipLen);
            sectionEnd.Configure("Attack", PresentationSectionBoundary.End);

            if (data.EnsureCueIds()) EditorUtility.SetDirty(data);

            var cues = new List<CueBinding>();
            data.CollectAllCues(cues);
            int placed = 0;
            var summary = new StringBuilder();
            for (int i = 0; i < cues.Count; i++)
            {
                CueBinding cue = cues[i];
                if (cue == null || string.IsNullOrWhiteSpace(cue.NormalizedCueName)) continue;

                // 근사 위치: 저장된 Time(정규화)이 있으면 그 지점, 없으면 순서대로 분산(저작자가 드래그로 확정).
                double t = cue.Time > 0f ? cue.Time * clipLen : clipLen * (0.25 + 0.15 * placed);
                t = System.Math.Min(t, clipLen * 0.95);

                var marker = markerTrack.CreateMarker<PresentationSignalMarker>(t);
                marker.Configure(PresentationSignalKind.Cue, cue.NormalizedCueName, cue.CueId);
                EditorUtility.SetDirty(marker);
                summary.Append($"  Cue '{cue.NormalizedCueName}' @ {t:F2}s\n");
                placed++;
            }

            // 투사체가 있으면 Projectile 마커도 하나(발사 시점은 저작자가 조정)
            if (data.GetProjectileVisual() != null)
            {
                var pm = markerTrack.CreateMarker<PresentationSignalMarker>(clipLen * 0.6);
                pm.Configure(PresentationSignalKind.Projectile, null, null);
                EditorUtility.SetDirty(pm);
                summary.Append($"  Projectile @ {clipLen * 0.6:F2}s\n");
                data.PresentationArchetype = PresentationArchetype.Projectile;
            }
            else
            {
                var impact = markerTrack.CreateMarker<PresentationSignalMarker>(clipLen * 0.6);
                impact.Configure(PresentationSignalKind.Impact, null, null);
                EditorUtility.SetDirty(impact);
                summary.Append($"  Impact @ {clipLen * 0.6:F2}s\n");
            }

            EditorUtility.SetDirty(markerTrack);
            EditorUtility.SetDirty(timeline);

            // 6) 스킬에 연결: Rail=Timeline + SkillTimelines 갱신
            ConnectToSkill(data, characterKey, timeline);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            Debug.Log(
                $"[JigPathABaker] 구움: {path}\n  캐릭터: '{characterKey}' / 클립: {clip.name} ({clipLen:F2}s)\n" +
                $"  Cue 마커 {placed}개 + 구간 1개 + 전달 마커 1개\n{summary}" +
                "  → 저작자: 클립 Split/속도/블렌드, 마커 위치, (투사체면) 발사 시점을 Timeline에서 다듬으세요.",
                timeline);

            return timeline;
        }

        private static string ResolveCharacterKey(GameObject characterPrefab)
        {
            var bc = characterPrefab.GetComponentInChildren<BattleCharactor>();
            return bc != null ? bc.UnitName : characterPrefab.name;
        }

        private static string ResolvePrimaryStateName(SkillPresentationData data)
        {
            if (data.Attack?.Beats != null)
            {
                for (int i = 0; i < data.Attack.Beats.Count; i++)
                {
                    string s = data.Attack.Beats[i]?.AnimationStateName;
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            return data.AnimationStateName;
        }

        private static void ConnectToSkill(SkillPresentationData data, string characterKey, TimelineAsset timeline)
        {
            Undo.RecordObject(data, "Path A Variant 연결");

            data.AnimationRail = AnimationRail.Timeline;
            if (data.SkillTimelines == null) data.SkillTimelines = new List<SkillTimelineBinding>();

            // 같은 캐릭터 키 항목이 있으면 갱신, 없으면 추가.
            SkillTimelineBinding entry = null;
            for (int i = 0; i < data.SkillTimelines.Count; i++)
            {
                SkillTimelineBinding b = data.SkillTimelines[i];
                if (b != null && string.Equals(b.CharacterKey, characterKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    entry = b;
                    break;
                }
            }
            if (entry == null)
            {
                entry = new SkillTimelineBinding();
                data.SkillTimelines.Add(entry);
            }
            entry.CharacterKey = characterKey;
            entry.Timeline = timeline;

            EditorUtility.SetDirty(data);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(RuntimeFolder))
            {
                AssetDatabase.CreateFolder(RuntimeFolderParent, RuntimeFolderName);
            }
        }

        private static string SanitizeFileName(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.ToString();
        }
    }
}
