using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 씬-공간(lossyScale 1) 트레일/반짝임 홀더. 오브의 월드 위치를 매 프레임 따라간다.
    /// 소켓 lossyScale 100 하에서 파티클/트레일 크기가 뒤틀리는 것을 피하려 씬 루트에 두고 위치만 추종.
    /// - bandTrail: 연속 띠(TrailRenderer). 팔로워가 손 경로를 따라가며 리본을 그린다.
    /// - systems: 반짝임 등 부수 파티클.
    /// </summary>
    public class ChargeTrailFollower : MonoBehaviour
    {
        [SerializeField] private TrailRenderer bandTrail;
        [SerializeField] private ParticleSystem[] systems;

        public void Begin(Vector3 worldPos)
        {
            transform.position = worldPos;
            if (bandTrail) { bandTrail.Clear(); bandTrail.emitting = true; }
            foreach (var ps in systems)
            {
                if (!ps) continue;
                ps.Clear();
                ps.Play();
            }
        }

        public void Follow(Vector3 worldPos) => transform.position = worldPos;

        /// <summary>
        /// 라이브 프리뷰 — 프리셋의 밴드/반짝임 구성을 인스턴스에 반영.
        /// 매핑은 ChargeOrbPresetEditor 의 「프리팹에 적용」과 동일(TrailStreaks + Sparkles).
        /// </summary>
        public void ApplyPresetConfig(ChargeOrbPreset p)
        {
            if (p == null) return;
            var band = FindSystem("TrailStreaks");
            if (band)
            {
                var m = band.main;
                m.startLifetime = p.bandStartLifetime;
                m.startSize = p.bandStartSize;
                var e = band.emission; e.rateOverTime = p.bandRate;
                var iv = band.inheritVelocity; iv.curve = new ParticleSystem.MinMaxCurve(p.bandInheritVelocity);
                var sh = band.shape; sh.radius = p.bandShapeRadius;
                var tr = band.trails; tr.lifetime = new ParticleSystem.MinMaxCurve(p.bandTrailLifetime);
            }
            var sparkle = FindSystem("Sparkles");
            if (sparkle)
            {
                var m = sparkle.main;
                m.startSize = new ParticleSystem.MinMaxCurve(p.sparkleSizeMin, p.sparkleSizeMax);
                m.startLifetime = p.sparkleLifetime;
                var e = sparkle.emission; e.rateOverTime = p.sparkleRate;
                var sh = sparkle.shape; sh.radius = p.sparkleShapeRadius;
            }
        }

        /// <summary>라이브 프리뷰 — 밴드/반짝임 색을 MPB 로 반영(비파괴).</summary>
        public void ApplyPresetColors(ChargeOrbPreset p, MaterialPropertyBlock mpb)
        {
            if (p == null || mpb == null) return;
            SetColor(FindSystem("TrailStreaks"), p.bandColor, mpb);
            SetColor(FindSystem("Sparkles"), p.sparkleColor, mpb);
        }

        private ParticleSystem FindSystem(string childName)
        {
            var t = transform.Find(childName);
            return t ? t.GetComponent<ParticleSystem>() : null;
        }

        private static void SetColor(ParticleSystem ps, Color c, MaterialPropertyBlock mpb)
        {
            if (!ps) return;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (!r) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }

        public void EndEmit()
        {
            if (bandTrail) bandTrail.emitting = false;                 // 꼬리 자연 소멸
            foreach (var ps in systems)
            {
                if (!ps) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
