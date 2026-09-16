using UnityEngine;
namespace JC.BuildingColors
{
    [CreateAssetMenu(menuName="JC/Building Colors/건물 색상 프로필")]
    public sealed class BgColorProfile : ScriptableObject
    {
        [Tooltip("같은 ID의 건물 조절기에서만 불러올 수 있습니다.")] public string buildingId;
        [Tooltip("파츠 ID에 대응하는 저장값입니다. 일반 조정은 씬의 건물 조절기에서 진행하세요.")] public BgPartColor[] parts; [Tooltip("저장값에 대응하는 파츠 ID입니다. 구형 각 파츠 프로필은 자동으로 연결됩니다.")] public string[] partIds;
    }
}
