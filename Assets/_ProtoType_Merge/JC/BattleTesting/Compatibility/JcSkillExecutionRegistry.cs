// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionRegistry.cs
// 원본 객체: SkillExecutionRegistry -> JcSkillExecutionRegistry
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections.Generic;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// skillKey(string) -> 커스텀 핸들러 매핑. (구현지시서 §5-9)
    /// 캐릭터=HS+숫자, 적=FV+숫자_슬롯(EnemySkillKeyRules.Compose). 무기(HCS…)는 아직 미연결 — 아래 TODO 참고.
    /// 등록되지 않은 키는 JcBattleManager 기본 실행 경로를 사용합니다.
    /// </summary>
    public static class JcSkillExecutionRegistry
    {
        private static readonly Dictionary<string, ISkillEffectHandler> Handlers = new Dictionary<string, ISkillEffectHandler>();
        private static bool s_initialized;

        static JcSkillExecutionRegistry()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 정적 초기화 보호: 1회만 등록.
        /// </summary>
        // 원본 함수 대응: SkillExecutionRegistry.EnsureInitialized (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionRegistry.cs)
        public static void EnsureInitialized()
        {
            if (s_initialized)
            {
                return;
            }
            s_initialized = true;

            //------------------아군 유닛 (캐릭터 스킬 키 = "HS" + 숫자)
            // 가디언
            Register("HS1010", new JcTauntStrikeSkillHandler()); // 단일 도발
            Register("HS1020", new JcTargetFrontPosMoreDmg());   // 단일 전열 보너스
            Register("HS1030", new JcPiercingDashSkillHandler());   // 후열 공격
            Register("HS1040", new JcAoETauntStrikeSkillHandler()); // 전체 도발 + 한열 타격 -------- 한 열 타격 적용 안됨
            Register("HS1050", new JcDamageSkillHandler());      // 단일(전체타겟)공격
            Register("HS1060", new JcAoEDamageSkillHandler());   // 전체공격
            Register("HS1070", new JcCasterLowHPMoreDmg());      // 시전자 체력 낮을수록 데미지 증가

            //블래스터
            Register("HS2010", new JcDamageSkillHandler());                 // 단일 공격
            Register("HS2020", new JcHitTargetAroundRandomHandler());      // 단일 + 랜덤 주변 적 공격
            Register("HS2030", new JcAoEDamageSkillHandler());             // 한 열 공격
            Register("HS2040", new JcPrismExplosionSkillHandler());             // 전체 공격
            Register("HS2050", new JcAtkAfterRest());                      // 임시 보관: 단일 공격 + 자신 한 턴 쉼(기절)
            Register("HS2060", new JcTargetMoreHPMoreDmg());               // 임시 보관: 적의 체력이 높을수록 피해량 증가
            Register("HS2070", new JcHitNumLowerDamageHandler());          // 임시 보관: 공격 대상 수에 따라 피해량 감소

            //스트라이커  (현재 스킬 시트 기준 재매핑)
            Register("HS3010", new JcDamageSkillHandler());        // 단일 공격
            Register("HS3020", new JcTargetFrontPosMoreDmg());     // 전열 적일 경우 더 많은 데미지 공격
            Register("HS3030", new JcDoubleAttackSkillHandler());  // 더블 공격
            Register("HS3040", new JcTargetLowerHPMoreDmg());      // 적 체력이 낮을 경우 높은 데미지

            // 미사용 스킬 파킹 (3050~3080, 핸들러 중복 없음)
            Register("HS3050", new JcTargetBackPosMoreCriticDmg()); // 단일공격 + 후열 공격시 일시적으로 회피율 -20%
            Register("HS3060", new JcTargetLowerHPMoreCriticDmg()); // 단일 공격 + 체력 70%이하 일시적으로 치명타 20% 확률업
            Register("HS3070", new JcAoEDamageSkillHandler());      // 전체 공격
            Register("HS3080", new JcDamageSkillHandler());         // 단일, 전체 체력이 낮을 경우 큰 데미지

            // 서포터
            Register("HS4010", new JcTargetHPPerHeal());      // 대상 체력 비례 힐
            Register("HS4020", new JcHolyBulletHpRecoveryHandler());    // 단일 공격 + HP 회복
            Register("HS4030", new JcHealTargetAroundRandomHandler()); // 광역 힐 + 인접 무작위 1명
            Register("HS4040", new JcRebirthSkillHandler());    // 부활 + 적 전체 공격
            Register("HS4050", new JcTargetLowerHPMoreHeal()); // 긴급 힐
            Register("HS4060", new JcAoEDamageSkillHandler()); //전체 공격
            Register("HS4070", new JcTargetHealBanSkill());   // 단일 공격 + 대상 힐 밴

            // 나이트
            Register("HS5010", new JcTargetFrontPosMoreDmg()); // 단일 공격 + 전열 시 추가피해
            Register("HS5020", new JcDamageSkillHandler());    // 단일 공격 -----
            Register("HS5030", new JcAoEDamageSkillHandler());  // 단일 공격 + 후열 추가 피해  -> 추가 구현 필요
            Register("HS5040", new JcDoubleAttackSkillHandler()); // 더블어택
            Register("HS5050", new JcMoreCriticDmg());         // 일시적 치명타 20% 확률업 + 전열우선 ----- 치명타 20% 적용 안됨
            Register("HS5060", new JcTargetLowerHPMoreDmg());  // 단일 공격 + 대상 체력 낮을수록 데미지 증가
            Register("HS5070", new JcDuelistSkillHandler());   // (1v1 시 데미지 증폭)

            //------------------적 (적 스킬 키 = EnemySkillKeyRules.Compose("FV"+숫자, 슬롯). 옛 200011 = 20001*10+1)
            Register(EnemySkillKeyRules.Compose("FV20001", 1), new JcDamageSkillHandler());    //단일 타깃 공격X -> 표식      AI 내용 : 가장 가까운 캐릭터 1명 표적 지적
            Register(EnemySkillKeyRules.Compose("FV20001", 2), new JcAoEDamageSkillHandler()); //십자 공격 -> 자폭

            Register(EnemySkillKeyRules.Compose("FV20002", 1), new JcDamageSkillHandler());    // 단일 공격
            Register(EnemySkillKeyRules.Compose("FV20002", 2), new JcAoEDamageSkillHandler()); // 원거리 난사(열 광역) — 구 단일힐에서 변경(알파 V4.0)

            Register(EnemySkillKeyRules.Compose("FV20003", 1), new JcDamageSkillHandler());    // 단일공격
            // 이벤트 전투는 enemyIndex로부터 숫자 키(20003_2)를 생성하므로 두 키를 같은 보호 핸들러에 연결한다.
            var guardHandler = new JcGuardSkillHandler();
            Register(EnemySkillKeyRules.Compose("FV20003", 2), guardHandler); // 대신 맞기(피해 가로채기) — 200032
            Register(EnemySkillKeyRules.Compose("20003", 2), guardHandler);

            // 빌런연합 강화병(FV20005) — 알파 V4.0
            Register(EnemySkillKeyRules.Compose("FV20005", 1), new JcDamageSkillHandler());          // 파동탄(단일 데미지)
            Register(EnemySkillKeyRules.Compose("FV20005", 2), new JcHitTargetAroundRandomHandler()); // 연격(단일 + 랜덤 1체)

            //------------------ 포탑(FV20004) + 4구역 율리아 진영(FV40001~40005)
            // 기모으기 2턴째 단일공격(1턴 충전은 EAI_20004가 오케스트레이션). 응축/한턴쉼은 AI가 SelfAction/Skip으로 처리(등록 불필요).
            Register(EnemySkillKeyRules.Compose("FV20004", 1), new JcDamageSkillHandler());    // 기모으기: 2턴째 단일공격
            Register(EnemySkillKeyRules.Compose("FV20004", 2), new JcAoEDamageSkillHandler()); // 포탑 범위공격(선등록; AI 회전은 후속)

            // 율리아 전투(BE490)는 2구역 이벤트 전투라 숫자 키(40001_1)로도 들어오므로 RegisterEnemy로 두 키를 함께 등록한다.
            RegisterEnemy("FV40001", 1, new JcAoEDamageSkillHandler()); // 나락의 폭풍(열 광역)
            RegisterEnemy("FV40001", 2, new JcAoEDamageSkillHandler()); // 공멸의 궤적(행 광역)

            RegisterEnemy("FV40002", 3, new JcAoEDamageSkillHandler()); // 나락의 폭풍(열)
            RegisterEnemy("FV40003", 3, new JcAoEDamageSkillHandler()); // 공멸의 궤적(행)

            RegisterEnemy("FV40005", 1, new JcSelfDestructRowAoEHandler()); // 절망의 굴렁쇠: 돌진+행광역+자폭

            // 소환 스킬(시전자 JcBossController._skillSummonEntries 설정대로 소환). 데미지 없음, target은 파이프라인 통과용.
            RegisterEnemy("FV40001", 3, new JcSummonSkillHandler()); // 절망의 굴레(소환)
            RegisterEnemy("FV40004", 1, new JcSummonSkillHandler()); // 율리아를 가둔 장막: FV40001_3과 동일

            //------------------장비(무기 스킬) — HCS00X 매핑 (X = 무기 스킬 종류)
            Register("HCS001", new JcDamageSkillHandler());                    // 1. 단일 공격
            Register("HCS002", new JcAoEDamageSkillHandler());                 // 2. 광역 공격
            Register("HCS003", new JcDamageTakenReductionSkillHandler());      // 3. 받피감 추가 감소(방어 버프 defense_up)
            Register("HCS004", new JcHealSkillHandler());                      // 4. 단일 체력 회복
            Register("HCS005", new JcDefenseDownSkillHandler());               // 5. 방어력 감소(디버프 defense_down)
        }

        // 원본 함수 대응: SkillExecutionRegistry.TryGetHandler (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionRegistry.cs)

        public static bool TryGetHandler(string skillKey, out ISkillEffectHandler handler)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(skillKey))
            {
                handler = null;
                return false;
            }
            if (Handlers.TryGetValue(skillKey, out handler)) return true;
            return Handlers.TryGetValue(HeroSkillRules.FamilyKey(skillKey), out handler);
        }

        // 원본 함수 대응: SkillExecutionRegistry.Register (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionRegistry.cs)

        private static void Register(string skillKey, ISkillEffectHandler handler)
        {
            if (handler == null || string.IsNullOrEmpty(skillKey))
            {
                return;
            }

            if (Handlers.ContainsKey(skillKey))
            {
                UnityEngine.Debug.LogWarning($"[JcSkillExecutionRegistry] skillKey {skillKey} 중복 등록 무시.");
                return;
            }

            Handlers.Add(skillKey, handler);
        }

        // 카탈로그 키(FV40001_1)와 이벤트 전투 키(40001_1)를 같은 핸들러 인스턴스에 연결한다.
        // 원본 함수 대응: SkillExecutionRegistry.RegisterEnemy (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionRegistry.cs)
        private static void RegisterEnemy(string enemyKey, int slot, ISkillEffectHandler handler)
        {
            string catalogKey = EnemySkillKeyRules.Compose(enemyKey, slot);
            Register(catalogKey, handler);

            string eventKey = EnemySkillKeyRules.ToNumericKey(catalogKey);
            if (eventKey != catalogKey)
            {
                Register(eventKey, handler);
            }
        }
    }
}
