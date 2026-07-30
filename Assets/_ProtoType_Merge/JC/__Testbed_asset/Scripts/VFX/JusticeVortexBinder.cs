using UnityEngine;

namespace JC.VFX
{
    /// <summary>용권풍 프리셋 → 이펙트 컴포넌트 반영 (정적 적용 로직).</summary>
    public static class JusticeVortexRuntime
    {
        public static void ApplyVortex(GameObject root, JusticeVortexPreset p)
        {
            if (root == null || p == null) return;

            var fx = root.GetComponent<Seam.JcVortexProjectileEffect>();
            if (fx == null) return;

            fx.UseStroke = p.useStroke;
            fx.UsePoint = p.usePoint;
            fx.UseWindArcs = p.useWindArcs;
            fx.UseShard = p.useShard;

            if (p.targets != null)
            {
                fx.StrokePrefab = p.targets.strokePrefab;
                fx.PointPrefab = p.targets.pointPrefab;
                fx.WindArcsPrefab = p.targets.windArcsPrefab;
                fx.ShardPrefab = p.targets.shardPrefab;
            }

            fx.LaunchDelay = p.launchDelay;
            fx.Speed = p.speed;
            fx.Distance = p.distance;
            fx.SpawnOffset = p.spawnOffset;

            fx.AngleStart = p.angleStart;
            fx.Clockwise = p.clockwise;
            fx.Turns = p.turns;
            fx.PreserveStrokeArc = p.preserveStrokeArc;
            fx.Opacity = p.opacity;
            fx.TailLinger = p.tailLinger;
        }
    }

    /// <summary>
    /// 이펙트 프리팹에 붙여 두면 스폰될 때마다 프리셋을 읽어 반영한다 —
    /// 프리셋을 만지면 "적용" 없이도 다음 재생부터 곧바로 반영.
    /// 색 변종은 JusticeTrailPresetBinder.UseAlternate 정적 플래그를 공유한다
    /// (자식 프리팹의 바인더들도 같은 플래그를 보므로 청·적이 한 번에 따라온다).
    /// </summary>
    [DisallowMultipleComponent]
    public class JusticeVortexBinder : MonoBehaviour
    {
        [Tooltip("읽어올 프리셋(기본 버전). 비우면 아무것도 하지 않는다.")]
        [SerializeField] private JusticeVortexPreset preset;

        [Tooltip("+스킬 버전 프리셋. 비어 있으면 기본을 그대로 쓴다.")]
        [SerializeField] private JusticeVortexPreset presetAlt;

        public JusticeVortexPreset Preset { get => preset; set => preset = value; }
        public JusticeVortexPreset PresetAlt { get => presetAlt; set => presetAlt = value; }

        public JusticeVortexPreset ActivePreset =>
            (JusticeTrailPresetBinder.UseAlternate && presetAlt != null) ? presetAlt : preset;

        private void Awake() => ApplyNow();

        public void ApplyNow() => JusticeVortexRuntime.ApplyVortex(gameObject, ActivePreset);
    }
}
