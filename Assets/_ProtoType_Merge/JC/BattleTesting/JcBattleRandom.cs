using System;

namespace JC.BattleTesting
{
    // 원본 대응: AutoBattleController/EnemyAIScript_4Sector/CombatCalculator의 UnityEngine.Random 호출.
    // 변경: 테스트 전투 판정은 전용 스트림을 사용해 VFX가 난수 소비 순서를 바꾸지 않도록 합니다.
    public static class JcBattleRandom
    {
        private static Random source = new Random();
        public static void Reset(bool useFixedSeed, int seed) => source = useFixedSeed ? new Random(seed) : new Random();
        public static float value => (float)source.NextDouble();
        public static int Range(int min, int max) => source.Next(min, max);
        public static float Range(float min, float max) => min + value * (max - min);
    }
}
