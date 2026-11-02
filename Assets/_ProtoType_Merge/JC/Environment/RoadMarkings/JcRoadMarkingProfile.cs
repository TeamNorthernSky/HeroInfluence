using UnityEngine;
namespace JC.EnvironmentDetail
{
    public abstract class JcRoadMarkingProfile : ScriptableObject
    {
        public static Color DefaultColor => new Color(.85f,.59f,.045f,1f);
        [ColorUsage(false), InspectorName("차선 색상")] public Color lineColor = DefaultColor;
        public abstract Vector3 Values { get; set; }
    }
}
