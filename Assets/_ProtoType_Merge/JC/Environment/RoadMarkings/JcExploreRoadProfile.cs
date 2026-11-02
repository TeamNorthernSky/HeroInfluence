using UnityEngine;
namespace JC.EnvironmentDetail
{
    [CreateAssetMenu(menuName="JC/환경/탐사 차선 프로필")]
    public sealed class JcExploreRoadProfile : JcRoadMarkingProfile
    {
        [Min(.02f), Tooltip("원본 차선 두께 대비 배율입니다. 1은 원본입니다.")] public float width=1;
        [Min(.02f), Tooltip("원본 차선 마디 길이 대비 배율입니다.")] public float length=1;
        [Min(.02f), Tooltip("원본 차선 빈 간격 대비 배율입니다. 모두 1이면 원본 외형을 유지합니다.")] public float gap=1;
        public override Vector3 Values { get => new Vector3(width,length,gap); set { width=value.x; length=value.y; gap=value.z; } }
    }
}
