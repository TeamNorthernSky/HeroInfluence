using UnityEngine;
namespace JC.EnvironmentDetail
{
    public abstract class JcRoadMarkingProfile : ScriptableObject
    {
        public abstract Vector3 Values { get; set; }
    }
}
