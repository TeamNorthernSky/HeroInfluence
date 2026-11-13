using System;
using UnityEngine;

// JC | 2026-11-13 | 칭호별 배경·테두리·글자 색은 전투 종류와 무관한 하나의 저장 프로필로 관리합니다.
[Serializable]
public sealed class JcInfluenceBadgeColors
{
    [InspectorName("배경색")] public Color surface;
    [InspectorName("테두리색")] public Color border;
    [InspectorName("글자색")] public Color text = Color.white;
    public JcInfluenceBadgeColors(Color fill, Color edge) { surface = fill; border = edge; }
}

[Serializable]
public sealed class JcInfluenceBadgeConfiguration
{
    [InspectorName("듣보")] public JcInfluenceBadgeColors unknown = new JcInfluenceBadgeColors(new Color(.33f,.36f,.40f), new Color(.64f,.68f,.72f));
    [InspectorName("유망주")] public JcInfluenceBadgeColors promising = new JcInfluenceBadgeColors(new Color(.15f,.38f,.24f), new Color(.39f,.70f,.46f));
    [InspectorName("베테랑")] public JcInfluenceBadgeColors veteran = new JcInfluenceBadgeColors(new Color(.13f,.30f,.53f), new Color(.38f,.65f,.90f));
    [InspectorName("국가권력")] public JcInfluenceBadgeColors national = new JcInfluenceBadgeColors(new Color(.53f,.35f,.10f), new Color(.95f,.76f,.34f));
    public JcInfluenceBadgeColors Get(int tier) => tier == 0 ? unknown : tier == 1 ? promising : tier == 2 ? veteran : national;
    public JcInfluenceBadgeConfiguration Copy() => JsonUtility.FromJson<JcInfluenceBadgeConfiguration>(JsonUtility.ToJson(this));
}

[CreateAssetMenu(menuName = "JC/전투/IP 칭호 색상")]
public sealed class JcInfluenceBadgeProfile : ScriptableObject
{
    public JcInfluenceBadgeConfiguration settings = new JcInfluenceBadgeConfiguration();
}
