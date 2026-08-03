using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// VFX 프리뷰 씬 전용 — 연출 확인을 방해하는 전투 판정과 입력을 끈다.
    ///
    /// 막는 것 세 가지:
    /// 1) 반격 — ExecuteGridSkill이 대미지 확정 후 CombatCalculator.RollCounter로 굴려
    ///    FlushCounterAttacks가 반격 스킬을 큐에 붙인다. 확률 판정이라 산발적으로 나타난다.
    /// 2) 전투 입력 — ASB InputHandler가 좌클릭에서 TryExecutePendingAction을 호출한다.
    ///    프리뷰 씬엔 정상 턴 흐름이 없어서, 게임 뷰를 클릭하면 엉뚱한 행동이 실행된다.
    /// 3) 대미지 — 반복 프리뷰 중 타겟이 죽지 않도록.
    ///
    /// ASB 코드·데이터·프리팹은 건드리지 않는다. 전부 기존 공개 API와 컴포넌트 enabled 토글만 사용.
    /// </summary>
    public class PreviewCombatSuppressor : MonoBehaviour
    {
        [Header("Suppress")]
        [Tooltip("반격을 끈다. 프리뷰에서 적이 제멋대로 1회 공격하는 원인.")]
        [SerializeField] private bool suppressCounterAttack = true;

        [Tooltip("ASB InputHandler를 끈다. 게임 뷰 좌클릭이 전투 행동을 실행하는 것을 막는다.")]
        [SerializeField] private bool suppressBattleInput = true;

        [Tooltip("회피를 끈다. 켜면 스킬이 빗나가 이펙트가 안 나오는 일이 사라진다.")]
        [SerializeField] private bool suppressEvade;

        [Tooltip("치명타를 끈다. 대미지 팝업 표기만 달라지므로 기본은 유지.")]
        [SerializeField] private bool suppressCritical;

        [Header("Damage")]
        [Tooltip("대미지를 무효화한다. 최대 HP를 크게 잡아 리타이어를 막고, 매 프레임 만피로 되돌린다.")]
        [SerializeField] private bool suppressDamage;

        [Tooltip("suppressDamage가 켜졌을 때 부여할 최대 HP.")]
        [SerializeField, Min(1f)] private float immortalMaxHp = 999999f;

        [Header("Timing")]
        [Tooltip("스폰이 끝난 뒤 적용해야 하므로 몇 프레임 기다린다.")]
        [SerializeField, Min(1)] private int waitFrames = 2;

        [Tooltip("★스폰·초기화가 늦게 base 스탯을 덮어써도 유지되도록 매 프레임 다시 강제한다.")]
        [SerializeField] private bool enforceEveryFrame = true;

        [SerializeField] private bool logResult = true;

        private readonly List<BattleCharactor> tracked = new List<BattleCharactor>();
        private bool loggedOnce;

        private IEnumerator Start()
        {
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            Apply();
        }

        /// <summary>유닛 목록을 다시 수집하고 억제를 적용한다.</summary>
        public void Apply()
        {
            tracked.Clear();
            tracked.AddRange(FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            EnforceStats(forceLog: true);
            EnforceInput();
        }

        private void EnforceInput()
        {
            if (!suppressBattleInput)
            {
                return;
            }

            InputHandler[] handlers = FindObjectsByType<InputHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i] != null && handlers[i].enabled)
                {
                    handlers[i].enabled = false;
                    if (logResult)
                    {
                        Debug.Log($"[PreviewCombatSuppressor] InputHandler '{handlers[i].gameObject.name}' 비활성화 — 클릭 오작동 차단.", handlers[i]);
                    }
                }
            }
        }

        /// <summary>
        /// base 스탯에서 확률 판정을 0으로 만든다.
        /// 스포너·초기화가 나중에 base를 덮어쓸 수 있으므로 매 프레임 상태를 확인하고 어긋나면 다시 적용한다.
        /// </summary>
        private void EnforceStats(bool forceLog = false)
        {
            for (int i = 0; i < tracked.Count; i++)
            {
                BattleCharactor unit = tracked[i];
                if (unit == null)
                {
                    continue;
                }

                StatBlock final = unit.FinalStats;
                bool needs =
                    (suppressCounterAttack && final.CounterRate > 0f) ||
                    (suppressEvade && final.AvoidRate > 0f) ||
                    (suppressCritical && final.CriticalRate > 0f) ||
                    (suppressDamage && final.HP < immortalMaxHp);

                if (!needs)
                {
                    continue;
                }

                StatBlock stats = unit.RuntimeBaseStats;
                if (suppressCounterAttack) stats.CounterRate = 0f;
                if (suppressEvade) stats.AvoidRate = 0f;
                if (suppressCritical) stats.CriticalRate = 0f;
                if (suppressDamage) stats.HP = immortalMaxHp;

                unit.SetBaseStats(stats);
                unit.RecalculateStats(applyCurrentHpClamp: false);
                if (suppressDamage)
                {
                    unit.InitializeCurrentHpToMax();
                }

                if (logResult && (forceLog || !loggedOnce))
                {
                    StatBlock after = unit.FinalStats;
                    Debug.Log($"[PreviewCombatSuppressor] {unit.UnitName}: counter={after.CounterRate:F3} " +
                              $"evade={after.AvoidRate:F3} crit={after.CriticalRate:F3} maxHp={after.HP:F0}", unit);
                }
            }

            loggedOnce = true;
        }

        private void LateUpdate()
        {
            if (enforceEveryFrame)
            {
                EnforceStats();
                EnforceInput();
            }

            if (!suppressDamage)
            {
                return;
            }

            for (int i = 0; i < tracked.Count; i++)
            {
                BattleCharactor unit = tracked[i];
                if (unit == null)
                {
                    continue;
                }

                if (unit.IsDead || unit.CurrentHp < unit.MaxHp)
                {
                    unit.InitializeCurrentState(unit.MaxHp, unit.CurrentInfluence);
                }
            }
        }
    }
}
