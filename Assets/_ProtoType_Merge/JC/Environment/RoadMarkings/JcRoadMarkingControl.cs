using UnityEngine;
namespace JC.EnvironmentDetail
{
    public abstract class JcRoadMarkingControl : MonoBehaviour
    {
        [Min(.02f), InspectorName("차선 두께"), Tooltip("전투는 월드 단위, 탐사는 원본 대비 배율입니다. 플레이 중 즉시 반영됩니다.")]
        public float width=1;
        [Min(.02f), InspectorName("마디 길이"), Tooltip("전투는 월드 단위, 탐사는 원본 대비 배율입니다.")]
        public float length=1;
        [Min(.02f), InspectorName("빈 간격"), Tooltip("전투는 월드 단위, 탐사는 원본 대비 배율입니다.")]
        public float gap=1;
        [ColorUsage(false), InspectorName("차선 색상"), Tooltip("플레이 중 즉시 반영됩니다. 현재 값을 프로필에 저장하면 색상도 보존됩니다.")]
        public Color lineColor = JcRoadMarkingProfile.DefaultColor;
        public abstract JcRoadMarkingProfile CurrentProfile { get; set; }
        public abstract bool IsBattle { get; }
        public Vector3 Values => new Vector3(width,length,gap);
        public string Status { get; protected set; } = "플레이 시 적용";
        protected abstract void RefreshTargets(bool changed);
        protected virtual void RestoreTargets() { }
        Vector3 applied;
        Color appliedColor;
        float nextDiscovery;
        bool requested;
        protected virtual void OnEnable() { if(Application.isPlaying) ApplyProfile(); }
        protected virtual void OnDisable() { RestoreTargets(); }
        protected virtual void OnDestroy() { RestoreTargets(); }
        protected virtual void OnValidate()
        {
            width=Finite(width); length=Finite(length); gap=Finite(gap); requested=true;
        }
        static float Finite(float value) => float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Clamp(value,.02f,100);
        protected virtual void Update()
        {
            if(!Application.isPlaying)return;
            bool changed=requested||applied!=Values||appliedColor!=lineColor;
            if(changed||Time.unscaledTime>=nextDiscovery) RefreshNow(changed);
        }
        public void RefreshNow(bool changed=true)
        {
            width=Finite(width);length=Finite(length);gap=Finite(gap);
            RefreshTargets(changed);applied=Values;appliedColor=lineColor;requested=false;nextDiscovery=Time.unscaledTime+.5f;
        }
        public void ApplyProfile()
        {
            var values=CurrentProfile!=null?CurrentProfile.Values:(IsBattle?new Vector3(.3f,2,3.8025f):Vector3.one);
            lineColor=CurrentProfile!=null?CurrentProfile.lineColor:JcRoadMarkingProfile.DefaultColor;
            width=Finite(values.x);length=Finite(values.y);gap=Finite(values.z);requested=true;
            if(Application.isPlaying)RefreshNow();
        }
    }
}
