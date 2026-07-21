using System;

namespace JC.VFX.Seam
{
    /// <summary>힐 연출이 실제로 실어 나른 단계 플래그. 두 버전 비교 매트릭스의 데이터.</summary>
    [Flags]
    public enum HealStageFlags
    {
        None   = 0,
        Charge = 1 << 0,  // 시전자 손 차징 구체
        Launch = 1 << 1,  // 손→타깃 발사체 비행
        Orbit  = 1 << 2,  // 타깃 주위 궤도
        Heal   = 1 << 3,  // 발밑 힐 오라
    }
}
