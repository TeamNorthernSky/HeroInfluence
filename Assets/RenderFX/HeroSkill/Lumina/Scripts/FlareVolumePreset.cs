using UnityEngine;
namespace JC.VFX
{
    [CreateAssetMenu(menuName="JC VFX/Flare Bomb Volume")]
    public sealed class FlareVolumePreset : ScriptableObject
    {
        [Header("소환 구체 — 기존 FB_M2 불꽃")]
        [Range(.2f,2f)] public float diameter = 1.15f;
        [Tooltip("상승 없이 소켓 정점에서 완성된 크기의 구체로 시작합니다.")]
        public bool startAtApex;
        [Tooltip("정점 생성 시 크기는 유지하고 밝기만 나타나는 시간입니다.")]
        [Range(.01f,.15f)] public float appearSeconds = .05f;
        [Tooltip("생성부터 발사까지의 프리뷰 시간입니다. 실제 발사는 타임라인 마커가 결정합니다.")]
        [Range(.05f,.8f)] public float summonSeconds = .304f;
        [Tooltip("기존 소환 시간 안에서 마지막에 확보하는 정점 회전 시간입니다. 발사 마커를 늦추지 않습니다.")]
        [Range(0,.25f)] public float apexHoldSeconds = .12f;
        [Tooltip("발사 전 정점 구체의 회전 속도입니다. 비행 나선 회전과 별개입니다.")]
        [InspectorName("정점 구체 회전 (도/초)")]
        [Range(0,720)] public float apexSpinDegreesPerSecond = 180;
        public float RiseSeconds => Mathf.Max(.01f, summonSeconds - Mathf.Clamp(apexHoldSeconds,0,summonSeconds-.01f));
        [Range(0,1)] public float riseHeight = .45f;
        [Range(0,1)] public float startScale = .12f;
        [ColorUsage(true,true)] public Color lowColor = new Color(.9f,.1f,.02f);
        [ColorUsage(true,true)] public Color midColor = new Color(1,.4f,.05f);
        [ColorUsage(true,true)] public Color highColor = new Color(1,.9f,.45f);
        [Range(.1f,6)] public float emission = 3.5f;
        [InspectorName("꼬리 불꽃 폭 배율")]
        [Range(.3f,2)] public float flareSpread = 1;
        [Header("비행 — 길어지는 불꽃과 회전 잔상")]
        [Tooltip("구체 지름에 대한 꼬리 최대 길이 배율입니다.")]
        [Range(1,6)] public float tailLength = 3.5f;
        [Tooltip("이 거리(구체 지름 배율)를 이동하면서 꼬리가 0에서 최대 크기까지 자랍니다.")]
        [Range(.2f,6)] public float tailGrowDistance = 2f;
        [Range(.05f,.6f)] public float trailLifetime = .32f;
        [Tooltip("진행 방향 축의 나선 회전입니다. 0이면 비행 회전이 멈춥니다.")]
        [InspectorName("비행 나선 회전 (도/초)")]
        [Range(0,720)] public float rollDegreesPerSecond = 0;
        [Range(0,.5f)] public float spiralRadius = .16f;
        [Range(.01f,.25f)] public float spiralWidth = .075f;
        [Range(0,2)] public float spiralBrightness = .6f;
        [Header("폭발 — 공용 프리셋")]
        [Range(.5f,3)] public float explosionDiameter = 1.8f;
        [Range(0,4)] public float explosionGlow = 1.6f;
        public FlareImpactPreset impact;
    }
}
