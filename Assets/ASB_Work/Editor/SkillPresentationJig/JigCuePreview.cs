using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>한 Cue의 지그 미리보기 발화 정보(경량 레코드). 마커 시각과 같은 시각에 수집한다.</summary>
    public sealed class JigCueFire
    {
        public double FireTime;
        public List<int> EffectIds;
        public List<int> SoundIds;
        public SpawnAnchor Anchor;
        public UnitSocket Socket;
        public string Label;
    }

    /// <summary>
    /// 지그 슬로우 재생 중 Cue 시각에 <b>사운드·소켓 이펙트를 근사로 발화</b>한다.
    ///
    /// <b>근사·에디터 전용·원본 SkillPresentationData 무영향.</b> 런타임 컨텍스트(타깃·방향·<c>Play(ctx)</c>)가 없으므로
    /// 투사체·타깃 의존 이펙트는 다루지 않는다(정확 확인은 PreviewScene). <c>JigPreviewPlayback</c>의 tick이 구동원이다.
    /// 정리는 <see cref="JigPreviewInstance.DestroyInstance"/> → <c>JigPreviewPlayback.Stop</c> → <see cref="CleanupAll"/>로 일원화한다.
    /// </summary>
    public static class JigCuePreview
    {
        // ── 토글 (창에서 조절) ──
        public static bool SoundEnabled = true;
        public static bool EffectEnabled = false;

        // ── 레지스트리 (창에서 선택, 프로젝트에 1개면 자동) ──
        public static EffectRegistry EffectRegistry;
        public static SoundRegistry SoundRegistry;

        /// <summary>안전 상한(초). IsAlive 기반 제거가 원칙이고 이건 비정상 프리팹 누수 방지용 — 일반 이펙트를 자르지 않을 만큼 길게.</summary>
        private const double EffectSafetyCapSeconds = 8d;

        /// <summary>스폰 직후 IsAlive가 아직 false일 수 있어, 이 유예 이전에는 IsAlive로 제거하지 않는다.</summary>
        private const double ReapGraceSeconds = 0.25d;

        private static readonly List<JigCueFire> _fires = new List<JigCueFire>();
        private static readonly HashSet<int> _fired = new HashSet<int>();

        private sealed class SpawnedFx
        {
            public GameObject Go;
            public double BornAt;
            public ParticleSystem[] Roots;
        }
        private static readonly List<SpawnedFx> _spawned = new List<SpawnedFx>();
        private static readonly List<AudioClip> _playing = new List<AudioClip>();

        // ──────────────────────────────────────────────────────────────
        // 발화 목록 / 리셋 / 정리
        // ──────────────────────────────────────────────────────────────

        /// <summary>지그 빌드 결과의 Cue 발화 목록을 설정한다(빌드 교체 시 이전 상태 정리).</summary>
        public static void SetFires(IEnumerable<JigCueFire> fires)
        {
            CleanupAll();
            _fires.Clear();
            if (fires != null) _fires.AddRange(fires);
        }

        /// <summary>실제 Runtime Timeline의 PresentationSignalMarker를 CueBinding에 연결해 프리뷰 목록을 만든다.</summary>
        public static int SetFiresFromTimeline(SkillPresentationData data, TimelineAsset timeline,
            List<string> report = null)
        {
            var fires = new List<JigCueFire>();
            if (data == null || timeline == null || timeline.markerTrack == null)
            {
                SetFires(fires);
                return 0;
            }

            var bindings = new List<CueBinding>();
            data.CollectAllCues(bindings);
            var byId = new Dictionary<string, CueBinding>();
            var byName = new Dictionary<string, CueBinding>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < bindings.Count; i++)
            {
                CueBinding cue = bindings[i];
                if (cue == null) continue;
                if (!string.IsNullOrEmpty(cue.CueId)) byId[cue.CueId] = cue;
                if (!string.IsNullOrEmpty(cue.NormalizedCueName) && !byName.ContainsKey(cue.NormalizedCueName))
                    byName.Add(cue.NormalizedCueName, cue);
            }

            foreach (IMarker raw in timeline.markerTrack.GetMarkers())
            {
                if (!(raw is PresentationSignalMarker marker) || marker.Kind != PresentationSignalKind.Cue) continue;

                CueBinding cue = null;
                if (!string.IsNullOrEmpty(marker.CueId)) byId.TryGetValue(marker.CueId, out cue);
                if (cue == null && !string.IsNullOrEmpty(marker.CueName)) byName.TryGetValue(marker.CueName, out cue);
                if (cue == null)
                {
                    report?.Add($"Cue Marker 연결 실패 @ {raw.time:F3}s (id='{marker.CueId}', name='{marker.CueName}')");
                    continue;
                }

                fires.Add(new JigCueFire
                {
                    FireTime = raw.time,
                    EffectIds = cue.EffectIds != null ? new List<int>(cue.EffectIds) : new List<int>(),
                    SoundIds = cue.SoundIds != null ? new List<int>(cue.SoundIds) : new List<int>(),
                    Anchor = cue.Anchor,
                    Socket = cue.Socket,
                    Label = cue.NormalizedCueName
                });
            }

            SetFires(fires);
            return fires.Count;
        }

        /// <summary>한 재생 패스 리셋 — fired 초기화 + 스폰물/오디오 정리(루프·되감기·재생 시작).</summary>
        public static void ResetPass()
        {
            _fired.Clear();
            DestroyAllSpawned();
            StopAllSounds();
        }

        /// <summary>전체 정리 — 프리뷰 파괴·창 닫기·정지 등. 재생 여부와 무관하게 호출된다.</summary>
        public static void CleanupAll()
        {
            _fired.Clear();
            DestroyAllSpawned();
            StopAllSounds();
        }

        public static void OnEffectToggledOff() => DestroyAllSpawned();
        public static void OnSoundToggledOff() => StopAllSounds();

        // ──────────────────────────────────────────────────────────────
        // 매 tick: 발화 판정 + 파티클 진행
        // ──────────────────────────────────────────────────────────────

        /// <summary>구간 (prevTime, nowTime]에 걸린 Cue를 1회 발화하고, 스폰 이펙트를 dt만큼 진행한다.</summary>
        public static void Advance(double prevTime, double nowTime)
        {
            for (int i = 0; i < _fires.Count; i++)
            {
                if (_fired.Contains(i)) continue;
                if (Crossed(prevTime, nowTime, _fires[i].FireTime))
                {
                    _fired.Add(i);
                    FireOne(_fires[i], nowTime);
                }
            }

            double dt = nowTime - prevTime;
            if (dt > 0d) DriveAndReap(dt, nowTime);
        }

        /// <summary>이번 tick에 발화 시각을 지났는지. 순수 함수(테스트 대상).</summary>
        public static bool Crossed(double prevTime, double nowTime, double fireTime)
        {
            return prevTime < fireTime && fireTime <= nowTime;
        }

        private static void FireOne(JigCueFire f, double now)
        {
            // 사운드 — 앵커 무관(2D). Held는 수집 단계에서 이미 제외됨.
            if (SoundEnabled && SoundRegistry != null && f.SoundIds != null)
            {
                for (int i = 0; i < f.SoundIds.Count; i++)
                {
                    AudioClip clip = SoundRegistry.Get(f.SoundIds[i]);
                    if (clip != null) PlayClip(clip);
                }
            }

            // 이펙트 — Caster/CasterSocket 앵커만(Target 계열은 스킵, 사운드만 재생됨).
            if (EffectEnabled && EffectRegistry != null && f.EffectIds != null && IsSpawnAnchor(f.Anchor))
            {
                Transform anchor = ResolveAnchor(f.Anchor, f.Socket);
                for (int i = 0; i < f.EffectIds.Count; i++)
                {
                    GameObject prefab = EffectRegistry.Get(f.EffectIds[i]);
                    if (prefab != null) SpawnEffect(prefab, anchor, now);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 이펙트 스폰 / 파티클 dt 증분 구동 / 수명
        // ──────────────────────────────────────────────────────────────

        private static void SpawnEffect(GameObject prefab, Transform anchor, double now)
        {
            Vector3 pos = anchor != null ? anchor.position : Vector3.zero;
            Quaternion rot = anchor != null ? anchor.rotation : Quaternion.identity;
            GameObject go = Object.Instantiate(prefab, pos, rot, anchor);
            go.hideFlags |= HideFlags.DontSaveInEditor;   // DontSave 금지
            _spawned.Add(new SpawnedFx { Go = go, BornAt = now, Roots = CollectRootSystems(go) });
        }

        private static void DriveAndReap(double dt, double now)
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                SpawnedFx fx = _spawned[i];
                if (fx.Go == null) { _spawned.RemoveAt(i); continue; }

                bool alive = false;
                for (int p = 0; p < fx.Roots.Length; p++)
                {
                    ParticleSystem ps = fx.Roots[p];
                    if (ps == null) continue;
                    // ★restart:false + dt 증분(누적 age 아님). 전진 재생만 하므로 이걸로 충분하고 깜빡임이 없다.
                    ps.Simulate((float)dt, true, false, false);
                    if (ps.IsAlive(true)) alive = true;
                }

                double age = now - fx.BornAt;
                bool overCap = age > EffectSafetyCapSeconds;
                bool deadPastGrace = !alive && age > ReapGraceSeconds;
                if (overCap || deadPastGrace)
                {
                    Object.DestroyImmediate(fx.Go);
                    _spawned.RemoveAt(i);
                }
            }
        }

        /// <summary>fx 안에서 상위 계층에 다른 ParticleSystem이 없는 시스템(루트)만 수집한다.</summary>
        private static ParticleSystem[] CollectRootSystems(GameObject go)
        {
            ParticleSystem[] all = go.GetComponentsInChildren<ParticleSystem>(true);
            var roots = new List<ParticleSystem>();
            Transform stop = go.transform.parent;   // 앵커 — fx 위로는 올라가지 않는다.
            for (int i = 0; i < all.Length; i++)
            {
                bool hasParentPs = false;
                for (Transform t = all[i].transform.parent; t != null && t != stop; t = t.parent)
                {
                    if (t.GetComponent<ParticleSystem>() != null) { hasParentPs = true; break; }
                }
                if (!hasParentPs) roots.Add(all[i]);
            }
            return roots.ToArray();
        }

        private static void DestroyAllSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i].Go != null) Object.DestroyImmediate(_spawned[i].Go);
            }
            _spawned.Clear();
        }

        // ──────────────────────────────────────────────────────────────
        // 앵커
        // ──────────────────────────────────────────────────────────────

        private static bool IsSpawnAnchor(SpawnAnchor a) => a == SpawnAnchor.Caster || a == SpawnAnchor.CasterSocket;

        private static Transform ResolveAnchor(SpawnAnchor anchor, UnitSocket socket)
        {
            GameObject preview = JigPreviewInstance.Current;
            if (preview == null) return null;

            if (anchor == SpawnAnchor.CasterSocket)
            {
                var profile = preview.GetComponentInChildren<UnitVisualProfile>(true);
                Transform s = profile != null ? profile.GetSocket(socket) : null;   // None→기본 소켓, 실패→내부 폴백
                if (s != null) return s;
            }
            return preview.transform;
        }

        // ──────────────────────────────────────────────────────────────
        // 레지스트리 자동 해석
        // ──────────────────────────────────────────────────────────────

        public static void AutoResolveRegistries()
        {
            if (EffectRegistry == null) EffectRegistry = FindSingle<EffectRegistry>();
            if (SoundRegistry == null) SoundRegistry = FindSingle<SoundRegistry>();
        }

        private static T FindSingle<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length != 1) return null;   // 0개/여러 개 → 창에서 선택
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 오디오 (AudioUtil 리플렉션 — 이 저장소의 internal API 리플렉션 선례를 따름)
        // ──────────────────────────────────────────────────────────────

        private static bool _audioResolved;
        private static MethodInfo _playPreview;
        private static MethodInfo _stopAll;
        private static bool _audioMissingWarned;
        private static bool _globalStopWarned;

        private static void ResolveAudio()
        {
            if (_audioResolved) return;
            _audioResolved = true;

            System.Type t = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
            if (t == null) return;

            _playPreview =
                t.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                    new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? t.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null,
                    new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? t.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null,
                    new[] { typeof(AudioClip) }, null);

            _stopAll =
                t.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)
                ?? t.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);
        }

        private static void PlayClip(AudioClip clip)
        {
            ResolveAudio();
            if (_playPreview == null)
            {
                if (!_audioMissingWarned)
                {
                    Debug.LogWarning("[Jig] 에디터 오디오 프리뷰 API(AudioUtil)를 찾지 못해 사운드 미리보기를 건너뜁니다.");
                    _audioMissingWarned = true;
                }
                return;
            }

            try
            {
                object[] args = _playPreview.GetParameters().Length == 3
                    ? new object[] { clip, 0, false }
                    : new object[] { clip };
                _playPreview.Invoke(null, args);
                _playing.Add(clip);
            }
            catch { /* 버전 차이로 실패해도 no-op */ }
        }

        private static void StopAllSounds()
        {
            if (_playing.Count == 0) return;
            _playing.Clear();

            ResolveAudio();
            // ★개별 정지 API가 없으므로 전역 정지를 fallback으로 쓴다(다른 에디터 프리뷰 소리도 멈출 수 있음, 1회 경고).
            if (_stopAll != null)
            {
                try { _stopAll.Invoke(null, null); } catch { /* no-op */ }
                if (!_globalStopWarned)
                {
                    Debug.LogWarning("[Jig] 개별 정지 API가 없어 전역 프리뷰 오디오 정지를 사용합니다(다른 에디터 프리뷰 소리도 멈출 수 있음).");
                    _globalStopWarned = true;
                }
            }
        }
    }
}
