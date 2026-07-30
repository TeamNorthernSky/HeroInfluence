using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 촬영용 — 플레이 모드에서도 「에디트 화면에 보이던 그대로」의 이펙트 상태를 유지시킨다.
    ///
    /// 왜 필요한가:
    ///   ① 이펙트 컴포넌트들이 Awake에서 자기 하위 오브젝트를 꺼 버린다
    ///      (FlareBombVfx.Awake → chargeOrb·flightFx SetActive(false),
    ///       FlareBombImpact.Awake → 폭발 렌더러 전부 off).
    ///      에디트 모드에선 Awake가 돌지 않아 보이던 것이 플레이 진입 순간 사라진다.
    ///   ② FlareOrbLayerToggle은 [ExecuteAlways] + Update로 레이어 활성 상태를 매 프레임
    ///      강제 복구한다 — 수동으로 꺼도 되살아난다(「지울 수 없는 구체」의 정체).
    ///   ③ 일부 이펙트는 수명이 끝나면 스스로 렌더러를 끈다.
    ///
    /// 해결: 모든 Awake가 끝난 뒤(Start)에 대상 하위의 제어 스크립트를 멈추고 전부 켠다.
    ///   제어가 멈춰도 파티클과 셰이더의 시간은 계속 흐르므로 화면은 살아 움직인다.
    ///   ★촬영 전용이다 — 실제 연출 동작을 막으므로 촬영이 끝나면 이 오브젝트를 꺼 둘 것.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcVfxShotKeeper : MonoBehaviour
    {
        [Tooltip("촬영에 담을 이펙트 루트들. 비워 두면 이 오브젝트 자신을 대상으로 한다.\n" +
                 "여러 개를 넣으면 전부 동시에 켜져 한 화면에 겹쳐 렌더된다.")]
        [SerializeField] private GameObject[] targets;

        [Tooltip("매 프레임 상태를 다시 강제한다. 수명이 끝나 스스로 꺼지는 이펙트까지 붙잡아 둔다.\n" +
                 "끄면 시작할 때 한 번만 켠다(이후 자연 소멸 허용).")]
        [SerializeField] private bool keepEveryFrame = true;

        [Tooltip("대상 하위의 제어 스크립트(MonoBehaviour)를 정지시킨다.\n" +
                 "끄지 않으면 그 스크립트들이 다시 오브젝트를 꺼 버린다.")]
        [SerializeField] private bool freezeControllers = true;

        // ★정지 제외 목록(260730) — 표시 자체를 담당하는 컴포넌트까지 멈추면 화면이 죽는다.
        // 예: FlareOrbShell은 OnEnable에서 셸 메시를 Build()하고 자전을 돌린다 —
        //     이걸 꺼 두면 활성화해도 OnEnable이 불리지 않아 메시가 비고 무늬가 사라진다.
        [Tooltip("정지시키지 않을 컴포넌트 이름(부분 일치). 표시·형상을 담당하는 스크립트를 여기에 둔다.\n" +
                 "★실제로 멈춰야 하는 것은 오브젝트를 꺼 버리는 연출 제어 스크립트뿐이다.")]
        [SerializeField]
        private string[] keepEnabledNames = { "FlareOrbShell", "FlareOrbSpriteAura", "FlareOrbLayerToggle", "Billboard", "Flicker" };

        [Tooltip("꺼졌다 켜진 파티클을 다시 재생시킨다. 폭발처럼 단발성 파티클을 반복 재생할 때 필요.")]
        [SerializeField] private bool replayParticles = true;

        [Tooltip("파티클 반복 재생 간격(초). 0이면 반복하지 않고 한 번만 재생한다.")]
        [SerializeField, Min(0f)] private float replayInterval = 2f;

        [Tooltip("시작 시 정리 내역을 콘솔에 남긴다.")]
        [SerializeField] private bool logOnStart = true;

        private float replayTimer;

        private void Start()
        {
            // 이전 실행에서 메시가 빈 채로 남았을 수 있으므로, 정지 전에 한 번 재빌드시킨다.
            // (컴포넌트를 껐다 켜면 OnEnable이 다시 돌아 Build()가 실행된다)
            RebuildShells();
            int n = Apply(true);
            if (logOnStart)
                Debug.Log("[JcVfxShotKeeper] 촬영 세팅 적용 — 활성화 " + n + "개 오브젝트" +
                          (freezeControllers ? ", 제어 스크립트 정지" : "") +
                          "\n※ 촬영이 끝나면 이 오브젝트를 비활성화하세요(실제 연출 동작이 막혀 있습니다).", this);
        }

        private void LateUpdate()
        {
            if (keepEveryFrame) Apply(false);

            if (replayParticles && replayInterval > 0.01f)
            {
                replayTimer += Time.deltaTime;
                if (replayTimer >= replayInterval)
                {
                    replayTimer = 0f;
                    ReplayAll();
                }
            }
        }

        /// <summary>
        /// 대상 하위를 전부 켜고, 제어 스크립트를 멈춘다. 반환값은 켠 오브젝트 수.
        ///
        /// ★순서가 중요하다(260730): 「활성화 → 정지」여야 한다.
        ///   Unity는 컴포넌트가 disabled면 GameObject를 켜도 OnEnable을 호출하지 않는다.
        ///   먼저 정지시키면 FlareOrbShell.OnEnable → Build()가 실행되지 않아
        ///   셸 메시가 비고 무늬가 통째로 사라진다(플레이 모드에서 오브가 민무늬 공이 되던 원인).
        /// </summary>
        private int Apply(bool first)
        {
            int count = 0;
            foreach (var root in Roots())
            {
                if (root == null) continue;

                // ① 먼저 전부 활성화 — 여기서 각 컴포넌트의 OnEnable이 정상 실행된다.
                if (!root.activeSelf) { root.SetActive(true); count++; }

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject.activeSelf) continue;
                    t.gameObject.SetActive(true);
                    count++;
                }

                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (!r.enabled) { r.enabled = true; count++; }

                // ② 그 다음 제어 스크립트 정지 — 표시 담당(keepEnabledNames)은 남긴다.
                if (freezeControllers)
                {
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null || mb == this) continue;
                        if (!mb.enabled) continue;
                        if (ShouldKeep(mb)) continue;
                        mb.enabled = false;
                    }
                }

                if (first && replayParticles) ReplayIn(root);
            }
            return count;
        }

        /// <summary>
        /// OnEnable에서 메시를 만드는 컴포넌트(FlareOrbShell 등)를 껐다 켜 재빌드시킨다.
        /// 앞선 촬영에서 메시가 빈 채로 굳은 경우를 자력으로 복구한다.
        /// </summary>
        private void RebuildShells()
        {
            foreach (var root in Roots())
            {
                if (root == null) continue;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb == this || !ShouldKeep(mb)) continue;
                    mb.enabled = false;
                    mb.enabled = true;   // OnEnable 재실행 → Build()
                }
            }
        }

        /// <summary>표시·형상을 담당해 정지시키면 안 되는 컴포넌트인가.</summary>
        private bool ShouldKeep(MonoBehaviour mb)
        {
            if (keepEnabledNames == null) return false;
            string n = mb.GetType().Name;
            for (int i = 0; i < keepEnabledNames.Length; i++)
            {
                var k = keepEnabledNames[i];
                if (!string.IsNullOrEmpty(k) && n.Contains(k)) return true;
            }
            return false;
        }

        private void ReplayAll()
        {
            foreach (var root in Roots())
                if (root != null) ReplayIn(root);
        }

        private void ReplayIn(GameObject root)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
            }
        }

        private GameObject[] Roots()
        {
            if (targets != null && targets.Length > 0) return targets;
            return new[] { gameObject };
        }

#if UNITY_EDITOR
        /// <summary>씬의 이펙트 프리뷰 루트들을 대상으로 자동 수집한다(에디터 편의).</summary>
        [ContextMenu("씬의 _Preview 오브젝트를 대상으로 수집")]
        private void CollectPreviewRoots()
        {
            var found = new System.Collections.Generic.List<GameObject>();
            foreach (var go in gameObject.scene.GetRootGameObjects())
                if (go != this.gameObject && go.name.Contains("_Preview"))
                    found.Add(go);
            targets = found.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[JcVfxShotKeeper] 대상 " + found.Count + "개 수집: " +
                      string.Join(", ", found.ConvertAll(g => g.name)), this);
        }
#endif
    }
}
