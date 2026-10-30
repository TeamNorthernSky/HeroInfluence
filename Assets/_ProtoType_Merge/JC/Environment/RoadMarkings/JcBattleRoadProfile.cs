using UnityEngine;
namespace JC.EnvironmentDetail
{
    [CreateAssetMenu(menuName="JC/환경/전투 차선 프로필")]
    public sealed class JcBattleRoadProfile : JcRoadMarkingProfile
    {
        [Min(.02f), Tooltip("전투 월드 단위의 차선 두께입니다.")] public float width=.3f;
        [Min(.1f), Tooltip("전투 월드 단위의 마디 길이입니다.")] public float length=2;
        [Min(.1f), Tooltip("마디 사이 빈 간격이며 횡단보도 최소 여백에도 적용합니다.")] public float gap=3.8025f;
        public override Vector3 Values { get => new Vector3(width,length,gap); set { width=value.x; length=value.y; gap=value.z; } }
    }
}
