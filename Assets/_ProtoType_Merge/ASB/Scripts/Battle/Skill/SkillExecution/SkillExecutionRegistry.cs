using System.Collections.Generic;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// skillKey(string) -> 커스텀 핸들러 매핑. (구현지시서 §5-9)
    /// 캐릭터=HS+숫자, 적=FV+숫자_슬롯(EnemySkillKeyRules.Compose). 무기(HCS…)는 아직 미연결 — 아래 TODO 참고.
    /// 등록되지 않은 키는 BattleManager 기본 실행 경로를 사용합니다.
    /// </summary>
    public static class SkillExecutionRegistry
    {
        private static readonly Dictionary<string, ISkillEffectHandler> Handlers = new Dictionary<string, ISkillEffectHandler>();
        private static bool s_initialized;

        static SkillExecutionRegistry()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 정적 초기화 보호: 1회만 등록.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (s_initialized)
            {
                return;
            }
            s_initialized = true;

            //------------------아군 유닛 (캐릭터 스킬 키 = "HS" + 숫자)
            // 가디언
            Register("HS1010", new TauntStrikeSkillHandler()); // 단일 도발
            Register("HS1020", new AoEDamageSkillHandler());   // 열 공격
            Register("HS1030", new AoEDamageSkillHandler());   // 후열 공격
            Register("HS1040", new TauntStrikeSkillHandler()); // 전체 도발 + 한열 타격 -------- 한 열 타격 적용 안됨
            Register("HS1050", new DamageSkillHandler());      // 단일(전체타겟)공격
            Register("HS1060", new AoEDamageSkillHandler());   // 전체공격
            Register("HS1070", new CasterLowHPMoreDmg());      // 시전자 체력 낮을수록 데미지 증가

            //블래스터
            Register("HS2010", new DamageSkillHandler());                 // 단일 공격
            Register("HS2020", new HitTargetAroundRandomHandler());      // 단일 + 랜덤 주변 적 공격
            Register("HS2030", new AoEDamageSkillHandler());             // 한 열 공격
            Register("HS2040", new AoEDamageSkillHandler());             // 전체 공격
            Register("HS2050", new AtkAfterRest());                      // 임시 보관: 단일 공격 + 자신 한 턴 쉼(기절)
            Register("HS2060", new TargetMoreHPMoreDmg());               // 임시 보관: 적의 체력이 높을수록 피해량 증가
            Register("HS2070", new HitNumLowerDamageHandler());          // 임시 보관: 공격 대상 수에 따라 피해량 감소

            //스트라이커  (현재 스킬 시트 기준 재매핑)
            Register("HS3010", new DamageSkillHandler());        // 단일 공격
            Register("HS3020", new TargetFrontPosMoreDmg());     // 전열 적일 경우 더 많은 데미지 공격
            Register("HS3030", new DoubleAttackSkillHandler());  // 더블 공격
            Register("HS3040", new TargetLowerHPMoreDmg());      // 적 체력이 낮을 경우 높은 데미지

            // 미사용 스킬 파킹 (3050~3080, 핸들러 중복 없음)
            Register("HS3050", new TargetBackPosMoreCriticDmg()); // 단일공격 + 후열 공격시 일시적으로 회피율 -20%
            Register("HS3060", new TargetLowerHPMoreCriticDmg()); // 단일 공격 + 체력 70%이하 일시적으로 치명타 20% 확률업
            Register("HS3070", new AoEDamageSkillHandler());      // 전체 공격
            Register("HS3080", new DamageSkillHandler());         // 단일, 전체 체력이 낮을 경우 큰 데미지

            // 서포터
            Register("HS4010", new TargetHPPerHeal());      // 대상 체력 비례 힐
            Register("HS4020", new HolyBulletHpRecoveryHandler());    // 단일 공격 + HP 회복
            Register("HS4030", new HealTargetAroundRandomHandler()); // 광역 힐 + 인접 무작위 1명
            Register("HS4040", new RebirthSkillHandler());    // 부활 + 적 전체 공격
            Register("HS4050", new TargetLowerHPMoreHeal()); // 긴급 힐
            Register("HS4060", new AoEDamageSkillHandler()); //전체 공격
            Register("HS4070", new TargetHealBanSkill());   // 단일 공격 + 대상 힐 밴

            // 나이트
            Register("HS5010", new TargetFrontPosMoreDmg()); // 단일 공격 + 전열 시 추가피해
            Register("HS5020", new DamageSkillHandler());    // 단일 공격 -----
            Register("HS5030", new AoEDamageSkillHandler());  // 단일 공격 + 후열 추가 피해  -> 추가 구현 필요
            Register("HS5040", new DoubleAttackSkillHandler()); // 더블어택
            Register("HS5050", new MoreCriticDmg());         // 일시적 치명타 20% 확률업 + 전열우선 ----- 치명타 20% 적용 안됨
            Register("HS5060", new TargetLowerHPMoreDmg());  // 단일 공격 + 대상 체력 낮을수록 데미지 증가
            Register("HS5070", new DuelistSkillHandler());   // (1v1 시 데미지 증폭)

            //------------------적 (적 스킬 키 = EnemySkillKeyRules.Compose("FV"+숫자, 슬롯). 옛 200011 = 20001*10+1)
            Register(EnemySkillKeyRules.Compose("FV20001", 1), new DamageSkillHandler());    //단일 공격
            Register(EnemySkillKeyRules.Compose("FV20001", 2), new AoEDamageSkillHandler()); //열 공격

            Register(EnemySkillKeyRules.Compose("FV20002", 1), new DamageSkillHandler());    // 단일 공격
            Register(EnemySkillKeyRules.Compose("FV20002", 2), new HealSkillHandler());      //단일 힐

            Register(EnemySkillKeyRules.Compose("FV20003", 1), new DamageSkillHandler());    // 단일공격
            Register(EnemySkillKeyRules.Compose("FV20003", 2), new AoEDamageSkillHandler()); // 전체공격

            //------------------ 포탑(FV20004) + 4구역 율리아 진영(FV40001~40005)
            // 기모으기 2턴째 단일공격(1턴 충전은 EAI_20004가 오케스트레이션). 응축/한턴쉼은 AI가 SelfAction/Skip으로 처리(등록 불필요).
            Register(EnemySkillKeyRules.Compose("FV20004", 1), new DamageSkillHandler());    // 기모으기: 2턴째 단일공격
            Register(EnemySkillKeyRules.Compose("FV20004", 2), new AoEDamageSkillHandler()); // 포탑 범위공격(선등록; AI 회전은 후속)

            Register(EnemySkillKeyRules.Compose("FV40001", 1), new AoEDamageSkillHandler()); // 나락의 폭풍(열 광역)
            Register(EnemySkillKeyRules.Compose("FV40001", 2), new AoEDamageSkillHandler()); // 공멸의 궤적(행 광역)

            Register(EnemySkillKeyRules.Compose("FV40002", 3), new AoEDamageSkillHandler()); // 나락의 폭풍(열)
            Register(EnemySkillKeyRules.Compose("FV40003", 3), new AoEDamageSkillHandler()); // 공멸의 궤적(행)

            Register(EnemySkillKeyRules.Compose("FV40005", 1), new SelfDestructRowAoEHandler()); // 절망의 굴렁쇠: 돌진+행광역+자폭

            //------------------장비(무기 스킬) — HCS00X 매핑 (X = 무기 스킬 종류)
            Register("HCS001", new DamageSkillHandler());                    // 1. 단일 공격
            Register("HCS002", new AoEDamageSkillHandler());                 // 2. 광역 공격
            Register("HCS003", new DamageTakenReductionSkillHandler());      // 3. 받피감 추가 감소(방어 버프 defense_up)
            Register("HCS004", new HealSkillHandler());                      // 4. 단일 체력 회복
            Register("HCS005", new DefenseDownSkillHandler());               // 5. 방어력 감소(디버프 defense_down)
        }

        public static bool TryGetHandler(string skillKey, out ISkillEffectHandler handler)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(skillKey))
            {
                handler = null;
                return false;
            }
            return Handlers.TryGetValue(skillKey, out handler);
        }

        private static void Register(string skillKey, ISkillEffectHandler handler)
        {
            if (handler == null || string.IsNullOrEmpty(skillKey))
            {
                return;
            }

            if (Handlers.ContainsKey(skillKey))
            {
                UnityEngine.Debug.LogWarning($"[SkillExecutionRegistry] skillKey {skillKey} 중복 등록 무시.");
                return;
            }

            Handlers.Add(skillKey, handler);
        }
    }
}
