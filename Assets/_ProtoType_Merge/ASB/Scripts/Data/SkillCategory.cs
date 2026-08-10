/// <summary>
/// 스킬 분류 (구현지시서 §5-7). 기존의 숫자 범위 판정(skillIndex &gt;= 200000 / &gt;= 300000)을 대체하는 명시 필드.
/// 생성 시점에 소스별로 명시 기록한다: 클래스 스킬=Class, 무기 스킬=Weapon, 적 스킬=Enemy.
/// 기본값(Class)은 미기록을 의미하므로, 적/무기 스킬은 반드시 명시해야 한다.
/// </summary>
public enum SkillCategory
{
    Class = 0,
    Enemy = 1,
    Weapon = 2
}
