using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 스킬 단계 치트 — 전투 중인 유닛의 장착 스킬을 1~8단계로 즉시 갈아끼운다. VFX 확인용.
    ///
    /// 조작: Ctrl+Shift+ <b>J</b>(저스티스) / <b>K</b>(블랙 불릿) / <b>L</b>(루미나) / <b>N</b>(네코밍)
    ///       → 1초 안에 <b>Q W E R T Y U I</b> (왼쪽부터 1~8단계)
    /// 캐릭터를 나눠 둔 이유: 강화 스킬 이펙트가 아직 없는 계열이 있어, 한 번에 전부 바꾸면 확인이 어렵다.
    /// ★숫자키를 쓰지 않는다 — 4와 Ctrl+Shift+8은 InputHandler의 턴 스킵 치트가 이미 쓰고 있다.
    ///
    /// ★영속 데이터를 건드리지 않는다. `BattleCharactor.LoadPersistentEquipment`는 카탈로그에서 읽어
    ///   전투 캐릭터의 메모리 필드에만 쓰는 「주입」 경로다(레포지토리 writeback 없음).
    ///   그래서 전투를 나갔다 오면 원래 스킬로 돌아온다 — 세이브가 오염될 여지가 없다.
    ///   (LabManager.EquipSkill은 영속에 기록하므로 여기서는 쓰지 않는다.)
    ///
    /// 단계 → 스킬 인덱스 (데이터 테이블 V3.0 기준):
    ///   1~4 = 기본 4종   C010 · C020 · C030 · C040
    ///   5~8 = 강화 4종   C011 · C021 · C031 · C041
    /// </summary>
    [DisallowMultipleComponent]
    public class JcSkillStageCheat : MonoBehaviour
    {
        /// <summary>트리거를 누른 뒤 숫자를 받는 제한 시간(초).</summary>
        const float InputWindow = 1f;

        private struct Target
        {
            public KeyCode key;
            public int band;        // 1000 = 저스티스, 2000 = 루미나, 3000 = 블랙 불릿, 4000 = 네코밍
            public string label;
        }

        private static readonly Target[] Targets =
        {
            new Target { key = KeyCode.J, band = 1000, label = "저스티스" },
            new Target { key = KeyCode.K, band = 3000, label = "블랙 불릿" },
            new Target { key = KeyCode.L, band = 2000, label = "루미나" },
            new Target { key = KeyCode.N, band = 4000, label = "네코밍" },
        };

        /// <summary>
        /// 단계 입력 = QWERTYUI (왼쪽부터 1~8단계).
        /// ★숫자열을 쓰지 않는 이유: InputHandler가 4(단독)와 Ctrl+Shift+8을 「턴 스킵」으로 이미 쓰고 있어,
        /// 같은 프레임에 두 스크립트가 각자 입력을 읽으면 스킬 교체와 턴 스킵이 함께 터진다.
        /// 문자열로 갈라 두면 ASB 파일을 수정하지 않고도 충돌이 사라진다.
        /// </summary>
        private static readonly KeyCode[] StageKeys =
        {
            KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R,
            KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I,
        };

        private int pendingBand;
        private string pendingLabel;
        private float pendingUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // ★HideFlags.DontSave를 쓰지 않는다 — 그 플래그는 「씬 저장 제외」만이 아니라
            //   「플레이 종료 시 파괴하지 않음」까지 뜻해서, 오브젝트가 에디트 모드로 새어 나가고
            //   다음 플레이마다 하나씩 누적된다. 런타임 생성 오브젝트는 어차피 씬에 저장되지 않는다.
            if (FindAnyObjectByType<JcSkillStageCheat>() != null) return;   // 중복 방지

            var go = new GameObject("[JC] SkillStageCheat");
            DontDestroyOnLoad(go);
            go.AddComponent<JcSkillStageCheat>();
            Debug.Log("[SkillStage] 준비 완료 — Ctrl+Shift+J(저스티스)/K(블랙 불릿)/L(루미나)/N(네코밍) → Q~I");
        }

        private void Update()
        {
            // 대기 중이면 숫자를 먼저 본다.
            if (pendingBand != 0)
            {
                if (Time.unscaledTime > pendingUntil)
                {
                    Debug.Log($"[SkillStage] {pendingLabel} — 입력 시간 초과(1초). 취소.");
                    pendingBand = 0;
                    return;
                }

                int stage = ReadStageKey();
                if (stage > 0)
                {
                    Apply(pendingBand, pendingLabel, stage);
                    pendingBand = 0;
                }
                return;
            }

            if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return;
            if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) return;

            for (int i = 0; i < Targets.Length; i++)
            {
                if (!Input.GetKeyDown(Targets[i].key)) continue;

                pendingBand = Targets[i].band;
                pendingLabel = Targets[i].label;
                pendingUntil = Time.unscaledTime + InputWindow;

                // 진단: 지금 전투에 어떤 유닛이 어떤 스킬을 들고 있는지 함께 남긴다.
                // 대역 판정이 「현재 스킬 인덱스」 기준이라, 여기서 대상이 안 잡히면 그게 원인이다.
                var all = FindObjectsByType<BattleCharactor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                var sb = new StringBuilder();
                sb.Append("[SkillStage] ").Append(pendingLabel).Append(" 대기 — 1초 안에 Q~I(1~8단계). 전투 유닛 ")
                  .Append(all.Length).Append("명:");
                for (int u = 0; u < all.Length; u++)
                {
                    SkillData s = all[u] != null ? all[u].SelectedSkillData : null;
                    sb.Append("\n    ").Append(all[u] != null ? all[u].UnitName : "?")
                      .Append("  현재 스킬=").Append(s != null ? s.skillIndex.ToString() : "null");
                }
                Debug.Log(sb.ToString());
                return;
            }
        }

        private static int ReadStageKey()
        {
            for (int i = 0; i < StageKeys.Length; i++)
            {
                if (Input.GetKeyDown(StageKeys[i])) return i + 1;
            }
            return 0;
        }

        /// <summary>단계(1~8) → 스킬 인덱스. 1~4는 기본 4종, 5~8은 같은 순서의 강화 4종.</summary>
        private static int StageToSkillIndex(int band, int stage)
        {
            int slot = ((stage - 1) % 4) + 1;      // 1~4
            bool plus = stage >= 5;
            return band + slot * 10 + (plus ? 1 : 0);
        }

        private void Apply(int band, string label, int stage)
        {
            int skillIndex = StageToSkillIndex(band, stage);

            var catalog = DHCsvTemplateCatalog.Instance;
            if (catalog == null)
            {
                Debug.LogWarning("[SkillStage] 카탈로그가 없습니다 — 전투 씬이 맞는지 확인하세요.");
                return;
            }
            if (catalog.GetSkillTemplate(skillIndex) == null)
            {
                Debug.LogWarning($"[SkillStage] {label} {stage}단계(인덱스 {skillIndex})가 카탈로그에 없습니다. 건너뜁니다.");
                return;
            }

            var units = FindObjectsByType<BattleCharactor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var changed = new List<string>();

            for (int i = 0; i < units.Length; i++)
            {
                BattleCharactor u = units[i];
                if (u == null || u.IsDead) continue;

                // 이 유닛이 어느 계열인지는 「지금 들고 있는 스킬 인덱스」로 판정한다.
                // 유닛 템플릿·클래스명 표기에 의존하지 않아, 데이터 표기가 바뀌어도 따라간다.
                SkillData cur = u.SelectedSkillData;
                if (cur == null || cur.skillIndex / 1000 != band / 1000) continue;

                // ★영속 저장 없음 — 카탈로그에서 읽어 이 인스턴스에만 주입한다.
                u.LoadPersistentEquipment(skillIndex, 0, 1, 0);
                PatchMissingTargetRule(u);
                changed.Add(u.UnitName);
            }

            if (changed.Count == 0)
            {
                Debug.Log($"[SkillStage] {label} {stage}단계 — 대상 유닛이 전투에 없습니다.");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("[SkillStage] ").Append(label).Append(' ').Append(stage).Append("단계 → 인덱스 ")
              .Append(skillIndex).Append(" 적용 ").Append(changed.Count).Append("명: ")
              .Append(string.Join(", ", changed))
              .Append("\n  (영속 저장 없음 — 전투를 나가면 원래 스킬로 돌아옵니다)");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ★전투 규칙 임시 보정 — V3.0 데이터 테이블의 `ClassSkillTarget` 컬럼이 비어 있어 생긴 구멍을 메운다.
        ///
        /// 범위 핸들러(BaseAoESkillHandler)는 첫 줄에서 `classSkillTarget == 0`이면 그대로 return하고,
        /// 대상 0개 → SkillExecutionResult.Success=false → 아무 로그 없이 「행동 실행 실패」가 된다.
        /// 저스티스 펀치(1020)·대쉬(1030)가 여기 걸린다. 등장!(1010)·크래쉬(1040)는 단일 핸들러라 무사하다.
        ///
        /// 보정 조건을 좁게 잡는다 — **boundary가 채워져 있는데 target만 0인 경우**만 건드린다.
        ///   1020 boundary=[0](중심만) / 1030 boundary=[3](관통) → 값은 이미 기획서대로 들어와 있고,
        ///   빠진 것은 「범위 스킬임을 알리는 스위치」 하나뿐이다. 그래서 target=1만 켜 준다.
        ///   1010은 boundary가 비어 있어 대상에서 제외 → 지금 잘 도는 동작을 흔들지 않는다.
        ///
        /// ★대상은 LoadPersistentEquipment가 만든 **복제본**이다(CloneSkillData).
        ///   카탈로그 원본·영속 데이터·ASB 자산 어디에도 기록되지 않고, 이 유닛 이 전투에만 산다.
        /// </summary>
        private static void PatchMissingTargetRule(BattleCharactor unit)
        {
            SkillData s = unit != null ? unit.SelectedSkillData : null;
            if (s == null) return;
            if (s.classSkillTarget != 0) return;                       // 이미 채워져 있으면 그대로
            if (s.boundary == null || s.boundary.Count == 0) return;   // 단일 스킬은 건드리지 않는다

            s.classSkillTarget = 1;   // 1 = boundary 패턴으로 범위 계산
            Debug.Log($"[SkillStage] 전투 규칙 임시 보정: {unit.UnitName} 스킬 {s.skillIndex} " +
                      $"classSkillTarget 0 → 1 (boundary={string.Join("/", s.boundary)})\n" +
                      "  ※V3.0 테이블에 ClassSkillTarget이 비어 있어 범위 핸들러가 0대상으로 종료되는 것을 우회합니다. " +
                      "이 유닛의 복제본만 고치며 원본 데이터는 그대로입니다.");
        }

        private void OnGUI()
        {
            if (pendingBand == 0) return;
            float left = Mathf.Max(0f, pendingUntil - Time.unscaledTime);
            GUI.Label(new Rect(12, 190, 460, 22),
                $"<b>{pendingLabel}</b> 스킬 단계 — <b>Q W E R T Y U I</b> = 1~8단계 ({left:F1}s)",
                new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true });
        }
    }
}
