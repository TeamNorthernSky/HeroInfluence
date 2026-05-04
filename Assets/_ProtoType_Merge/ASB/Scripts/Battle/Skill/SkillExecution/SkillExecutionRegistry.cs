using System.Collections.Generic;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// skillIndex -> 커스텀 핸들러 매핑.
    /// 등록되지 않은 인덱스는 BattleManager 기본 실행 경로를 사용합니다.
    /// </summary>
    public static class SkillExecutionRegistry
    {
        private static readonly Dictionary<int, ISkillEffectHandler> Handlers = new Dictionary<int, ISkillEffectHandler>();
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


            // 이것 등록하는 것도 필요한 클래스의 함수만 따로 분리하는게 좋을듯함
            //------------------아군 유닛
            // 워리어
            Register(1010, new TauntStrikeSkillHandler()); // 단일 도발
            Register(1020, new AoEDamageSkillHandler());   // 열 공격
            Register(1030, new AoEDamageSkillHandler());   // 후열 공격
            Register(1040, new TauntStrikeSkillHandler()); // 일단 전체 도발만!!
            Register(1050, new DamageSkillHandler());      // 단일(전체타겟)공격
            Register(1060, new AoEDamageSkillHandler());   // 전체공격
            Register(1070, new CasterLowHPMoreDmg());      // 

            // 아처
            Register(2010, new DamageSkillHandler());    // 단일 공격
            Register(2020, new TargetFrontPosMoreDmg()); // 단일 공격 + 전열 추가 피해
            Register(2030, new DamageSkillHandler());    // 단일공격 + 후열 공격시 일시적으로 회피율 -20%
            Register(2040, new AoEDamageSkillHandler()); // 전체 공격
            Register(2050, new DamageSkillHandler());    // 단일 공격 + 체력 70%이하 일시적으로 치명타 20% 확률업
            Register(2060, new AoEDamageSkillHandler()); //전체 공격
            Register(2070, new DamageSkillHandler());    // 단일, 전체 체력이 낮을 경우 큰 데미지

            //메이지
            Register(3010, new DamageSkillHandler());        // 단일 공격
            Register(3020, new AoEDamageSkillHandler());     // 열 공격
            Register(3030, new AtkAfterRest());              // 단일공격 + 자신 한턴 쉼(기절)
            Register(3040, new HitTargetAroundRandomHandler()); // 단일 + 렌덤 주변적 공격
            Register(3050, new AoEDamageSkillHandler());     // 전체 공격
            Register(3060, new TargetMoreHPMoreDmg());       // 적의 체력이 높을수록 피해량 증가
            Register(3070, new HitNumLowerDamageHandler());    // 전제!!! 공격 대상 수에 따라 피해량 감소

            // 클레릭
            Register(4010, new HealSkillHandler());      // 단일 힐
            Register(4020, new DamageSkillHandler());    // 단일 공격
            Register(4030, new TargetLowerHPMoreHeal()); // 긴급 힐: 대상의 체력이 낮을수록 힐량 증가
            Register(4040, new TargetHealBanSkill());    // 단일공격 + 대상 힐 밴
            Register(4050, new HealTargetAroundRandomHandler());    // 단일 공격 + 체력 70%이하 일시적으로 치명타 20% 확률업
            Register(4060, new AoEDamageSkillHandler()); //전체 공격
            Register(4070, new RebirthSkillHandler());

            // 나이트 
            Register(5010, new TargetFrontPosMoreDmg()); // 단일 공격 + 전열 시 추가피해
            Register(5020, new DamageSkillHandler());    // 단일 공격
            Register(5030, new TargetBackPosMoreDmg());  // 단일 공격 + 후열 추가 피해
            Register(5040, new DamageSkillHandler());    // 더블어택 
            Register(5050, new DamageSkillHandler());    // 일시적 치명타 20% 확률업 + 전열우선
            Register(5060, new AoEDamageSkillHandler()); //전체 공격
            //Register(5070, new RebirthSkillHandler());




            //------------------적
            Register(200011, new DamageSkillHandler());    //단일 공격
            Register(200012, new AoEDamageSkillHandler()); //열 공격

            Register(200021, new DamageSkillHandler());    // 단일 공격
            Register(200022, new HealSkillHandler());      //단일 힐

            Register(200031, new DamageSkillHandler());    // 단일공격
            Register(200032, new AoEDamageSkillHandler());   // 전체공격



            //------------------장비
            Register(311010, new DamageSkillHandler()); // 단일 공격 
            Register(311020, new DamageSkillHandler());    // 단일 공격
            Register(311030, new DamageSkillHandler());  // 단일 공격 

            Register(312010, new DamageSkillHandler());    // 단일공격 
            Register(312020, new AoEDamageSkillHandler());    // 열공격
            Register(312030, new DamageSkillHandler()); // 단일공격

            Register(313010, new DamageSkillHandler());    // 단일공격 
            Register(313020, new HitTargetAroundRandomHandler());    // 단일 + 랜덤 주변공격
            Register(313030, new AoEDamageSkillHandler()); //전체 공격

            Register(314010, new DamageSkillHandler());    // 단일공격
            Register(314020, new DamageSkillHandler());    // 단일공격
            Register(314030, new AoEDamageSkillHandler()); // 전체 + 흡혈  -> 구현 필요함므루웅

            Register(315010, new DamageSkillHandler());    // 단일공격 
            Register(315020, new DamageSkillHandler());    // 자신의 위치 후열 -> 후열 공격,  자신의 위치 전열 -> 전열 공격 
            Register(315030, new AoEDamageSkillHandler()); // 단일공격
        }

        public static bool TryGetHandler(int skillIndex, out ISkillEffectHandler handler)
        {
            EnsureInitialized();
            return Handlers.TryGetValue(skillIndex, out handler);
        }

        private static void Register(int skillIndex, ISkillEffectHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            if (Handlers.ContainsKey(skillIndex))
            {
                UnityEngine.Debug.LogWarning($"[SkillExecutionRegistry] skillIndex {skillIndex} 중복 등록 무시.");
                return;
            }

            Handlers.Add(skillIndex, handler);
        }
    }
}
