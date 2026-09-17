using System;
using UnityEngine;
namespace JC.BuildingColors
{
    [Serializable] public sealed class BgColorSetEntry { [Tooltip("이 항목에 대응하는 건물 종류 ID입니다.")] public string buildingId; [Tooltip("이 건물의 각 파츠 색상값입니다. 씬 연결 정보는 포함하지 않습니다.")] public BgPartColor[] parts; [Tooltip("저장값에 대응하는 파츠 ID입니다. 구형 각 파츠 프로필은 자동으로 연결됩니다.")] public string[] partIds; }
    [CreateAssetMenu(menuName="JC/Building Colors/전체 건물 색상 프로필")]
    public sealed class BgColorSetProfile : ScriptableObject { [Tooltip("전체 건물의 색상 스냅샷입니다. 일반 저장·불러오기는 씬의 BG_ColorModifier에서 진행하세요.")] public BgColorSetEntry[] buildings; }
}
