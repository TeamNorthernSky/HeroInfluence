using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 블랙불릿 AimShot — 탄두를 따라 그어지는 궤적(획). TrailRenderer 기반.
    ///
    /// 왜 TrailRenderer인가:
    ///  - 프로젝트의 발사체 이펙트 컨벤션이다(ProjectileOrb·LFL_ProjectileOrb·Tao_MeteorOrb 모두 TrailRenderer).
    ///  - 트랜스폼의 월드 위치 이력을 스스로 기록하므로 "탄두를 따라 그어지는" 형태가 그대로 나온다.
    ///  - 꼬리 도달 범위가 `time`(★절대 초) 하나로 결정된다 → 궤적 길이가 자동으로 맞는다.
    ///  - 탄두가 멈추면 옛 정점이 만료되며 궤적이 스스로 머리 쪽으로 빨려들어 사라진다
    ///    (절차 리본 시절 손으로 만든 _TailCut 소멸 연출이 불필요해졌다).
    ///
    /// 폭 프로파일은 widthCurve를 쓰지 않고 **셰이더 슬라이더**가 담당한다
    /// (KBulletTrail 셰이더의 _TailWidth/_TailFade/_TaperCurve). 여기서는 기준 폭만 준다.
    ///
    /// ★소켓 자식이 되지 않는다. 오케스트레이터가 월드 좌표를 대입한다.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public class KBulletTrailStroke : MonoBehaviour
    {
        private TrailRenderer _tr;
        private MaterialPropertyBlock _mpb;
        private static readonly int IdFadeMul = Shader.PropertyToID("_FadeMul");
        private static readonly int IdTailWidth = Shader.PropertyToID("_TailWidth");
        private static readonly int IdTailFade = Shader.PropertyToID("_TailFade");
        private static readonly int IdTaperCurve = Shader.PropertyToID("_TaperCurve");
        private static readonly int IdHeadColor = Shader.PropertyToID("_HeadColor");
        private static readonly int IdMidColor = Shader.PropertyToID("_MidColor");
        private static readonly int IdTailColor = Shader.PropertyToID("_TailColor");
        private static readonly int IdCoreColor = Shader.PropertyToID("_CoreColor");

        private void Awake()
        {
            Resolve();
            HideImmediate();
        }

        private void Resolve()
        {
            if (_tr == null) _tr = GetComponent<TrailRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
        }

        /// <summary>즉시 소거. 남은 정점까지 버린다.</summary>
        public void HideImmediate()
        {
            Resolve();
            if (_tr == null) return;
            _tr.emitting = false;
            _tr.Clear();
            _tr.enabled = false;
        }

        /// <summary>
        /// 발사 시작. 이전 위치에서 새 위치로 획이 그어지는 것을 막기 위해
        /// 좌표를 먼저 옮긴 뒤 Clear한다(순서가 중요).
        /// </summary>
        public void Begin(Vector3 worldPos)
        {
            Resolve();
            if (_tr == null) return;
            transform.position = worldPos;
            _tr.Clear();
            _tr.enabled = true;
            _tr.emitting = true;
        }

        /// <summary>탄두 위치 추적. 매 프레임 호출.</summary>
        public void SetHeadPosition(Vector3 worldPos) => transform.position = worldPos;

        /// <summary>정점 추가만 중단. 기존 정점은 time에 걸쳐 만료되며 궤적이 스스로 수축한다.</summary>
        public void StopEmitting()
        {
            Resolve();
            if (_tr != null) _tr.emitting = false;
        }

        /// <summary>꼬리 도달 범위(초). 궤적 길이 ≈ 탄두 속도 × 이 값.</summary>
        public void SetTrailTime(float seconds)
        {
            Resolve();
            if (_tr != null) _tr.time = Mathf.Max(0.001f, seconds);
        }

        /// <summary>기준 폭(미터). 형태 테이퍼는 셰이더가 담당한다.</summary>
        public void SetWidth(float width)
        {
            Resolve();
            if (_tr != null) _tr.widthMultiplier = Mathf.Max(0f, width);
        }

        /// <summary>
        /// 셰이더 파라미터를 한 번에 밀어 넣는다. MaterialPropertyBlock을 쓰므로
        /// 공유 머티리얼 에셋을 건드리지 않는다(프리셋이 단일 소스, 에셋 오염·diff 없음).
        ///
        /// tailWidthRatio / tailBrightnessRatio / taperCurve 는 **공간축**(머리↔꼬리) 테이퍼다.
        /// KElementLife의 크기 3값(시간축 전체 굵기)과는 다른 축이므로 서로 간섭하지 않는다.
        /// </summary>
        public void PushShaderParams(float fadeMul, float tailWidthRatio, float tailBrightnessRatio, float taperCurve)
        {
            Resolve();
            if (_tr == null) return;
            _tr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdFadeMul, Mathf.Clamp01(fadeMul));
            _mpb.SetFloat(IdTailWidth, Mathf.Clamp(tailWidthRatio, 0.01f, 1f));
            _mpb.SetFloat(IdTailFade, Mathf.Clamp01(tailBrightnessRatio));
            _mpb.SetFloat(IdTaperCurve, Mathf.Clamp(taperCurve, 0.2f, 6f));
            _tr.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 공간축 3스톱 테두리 색 + 내부(중심선) 색. 발광이 적용된 HDR 값을 받는다.
        /// 셰이더가 획 길이를 따라 head→mid→tail로 보간한다(head = 탄두 쪽).
        /// </summary>
        public void PushColors(Color head, Color mid, Color tail, Color inner)
        {
            Resolve();
            if (_tr == null) return;
            _tr.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdHeadColor, head);
            _mpb.SetColor(IdMidColor, mid);
            _mpb.SetColor(IdTailColor, tail);
            _mpb.SetColor(IdCoreColor, inner);
            _tr.SetPropertyBlock(_mpb);
        }

        public void SetFade(float fadeMul)
        {
            Resolve();
            if (_tr == null) return;
            _tr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdFadeMul, Mathf.Clamp01(fadeMul));
            _tr.SetPropertyBlock(_mpb);
        }

        /// <summary>남아 있는 정점이 있는지(수축이 끝났는지 판정용).</summary>
        public bool HasPoints => _tr != null && _tr.positionCount > 0;
    }
}
