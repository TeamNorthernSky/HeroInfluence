using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 블랙불릿 AimShot(3010) 마스터 프리셋 — 타이밍·배치·크기.
    /// 셰이더 파라미터(색·발광·형상)는 각 머티리얼이 직접 들고 있고 여기서는 건드리지 않는다.
    ///
    /// 구조: 요소별로 KElementLife 블록을 하나씩 두고, 각 요소가 자기 시계로 독립 동작한다.
    /// 한 요소의 수명을 늘려도 다른 요소가 밀리지 않는다.
    /// (머즐 플래시부터 이 구조로 전환 완료. 탄두·궤적·탄착은 순차 이관 예정)
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/K_AimShot/K0_AimShot Master Preset", fileName = "K0_AimShotMasterPreset")]
    public class KAimShotPreset : ScriptableObject
    {
        [Header("── 적용 대상 ──")]
        [Tooltip("「▶ 프리팹에 적용」/「● 현재값 캡처」의 대상 프리팹. " +
                 "★경로를 코드에 하드코딩하지 않고 프리셋이 자기 대상을 들고 있게 한다 — " +
                 "저스티스에서 상수 하드코딩 때문에 다른 스킬 프리팹에 기록되는 오배선 사고가 있었다.")]
        public GameObject targetPrefab;

        [Header("── 시퀀스 ──")]
        [Tooltip("탄환이 총구를 떠나는 시각(초). 각 요소의 수명과 무관하다 — 요소는 자기 시계로 돈다.")]
        public float launchDelay = 0.06f;
        [Tooltip("탄환이 총구→타겟까지 이동하는 시간. 짧을수록 광선에 가까워진다. " +
                 "탄착 요소의 startDelay는 보통 launchDelay + travelTime(도착 시각)으로 맞춘다.")]
        public float travelTime = 0.12f;

        [Header("── 머즐 플래시 ──")]
        [Tooltip("총구 섬광의 독립 생명주기. 수명·페이드·크기 구간을 자기 시계로 처리한다.")]
        public KElementLife muzzleFlash = new KElementLife
        {
            enabled = true,
            startDelay = 0f,
            lifetime = 0.18f,
            fadeIn = 0f,
            fadeOut = 0.35f,
            startSize = 0.18f,
            maxSize = 0.45f,
            endSize = 0.12f,
            expandEnd = 0.22f,
            holdEnd = 0.45f,
            expandEase = KEase.Linear,
            shrinkEase = KEase.Linear,
            offset = new Vector3(0f, 0f, 0.02f)
        };

        [Header("── 탄두(투사체 머리) ──")]
        [Tooltip("탄두의 독립 생명주기. 크기·페이드는 자기 시계로 돌고, 위치만 주행 진행도에 묶인다. " +
                 "수명이 주행 시간보다 길면 탄착 지점에 잠시 머무르며 사라진다.")]
        public KElementLife bulletHead = new KElementLife
        {
            enabled = true,
            startDelay = 0.06f,
            lifetime = 0.16f,
            fadeIn = 0f,
            fadeOut = 0.25f,
            startSize = 2.6f,
            maxSize = 3.0f,
            endSize = 0.8f,
            expandEnd = 0.12f,
            holdEnd = 0.70f,
            expandEase = KEase.Linear,
            shrinkEase = KEase.EaseOut,
            offset = Vector3.zero
        };

        [Header("── 궤적 : 시간축 ──")]
        [Tooltip("궤적의 독립 생명주기. 크기 3값(시작/최대/종료)은 **획 전체의 기준 굵기이며 시간에 따라 변한다**. " +
                 "머리↔꼬리의 굵기 차이(공간축)는 아래 '궤적 : 공간 테이퍼' 항목이 담당한다 — 서로 다른 축이다.")]
        public KElementLife trail = new KElementLife
        {
            enabled = true,
            startDelay = 0.06f,
            lifetime = 0.45f,
            fadeIn = 0f,
            fadeOut = 0.35f,
            fadeInEase = KEase.Linear,
            fadeOutEase = KEase.EaseOut,
            startSize = 0.14f,
            maxSize = 0.14f,
            endSize = 0.06f,
            expandEnd = 0f,
            holdEnd = 0.65f,
            expandEase = KEase.Linear,
            shrinkEase = KEase.Linear,
            offset = Vector3.zero
        };

        [Tooltip("★그어진 획이 화면에 남는 시간(절대 초). TrailRenderer.time에 직결.\n" +
                 "· 비행 중에는 궤적 길이를 결정한다(길이 ≈ 탄두 속도 × 이 값). 주행 시간 이상이면 꼬리가 총구까지 닿는다.\n" +
                 "· 탄착 후에는 획이 남아 있는 시간을 결정한다. 획을 오래 유지하려면 이 값을 늘린다.\n" +
                 "· 위 trail.lifetime과 혼동 주의 — 그쪽은 '요소가 존재하는 시간'일 뿐 획 길이·잔존과 무관하다.\n" +
                 "· trail.lifetime ≥ travelTime + 이 값 이어야 획이 끝까지 재생된다.")]
        public float tailReachSeconds = 0.14f;

        [Header("── 궤적 : 공간 테이퍼 (머리↔꼬리) ──")]
        [Tooltip("꼬리 굵기 / 머리 굵기 비율. 1이면 균일한 막대, 작을수록 꼬리가 가늘어진다. " +
                 "머리(탄두) 쪽이 항상 최대 굵기 기준이다.")]
        [Range(0.01f, 1f)] public float trailTailWidthRatio = 0.30f;
        [Tooltip("꼬리 휘도 / 머리 휘도 비율. 작을수록 꼬리가 옅어진다.")]
        [Range(0f, 1f)] public float trailTailBrightnessRatio = 0.22f;
        [Tooltip("테이퍼 곡률. 1이면 선형, 클수록 머리 근처에서 급격히 굵어진다.")]
        [Range(0.2f, 6f)] public float trailTaperCurve = 1.15f;

        [Header("── 탄착 ──")]
        [Tooltip("탄착 스타버스트의 독립 생명주기.\n" +
                 "· startDelay는 도착 시각(launchDelay + travelTime)에 맞추는 것이 기본이다.\n" +
                 "· offset은 타겟 루트 기준 위치 보정. 탄두가 도달하는 지점도 이 값을 따른다.\n" +
                 "· 팝 연출은 startSize를 maxSize보다 크게 잡으면 된다(크게 터진 뒤 수축).")]
        public KElementLife impactBurst = new KElementLife
        {
            enabled = true,
            startDelay = 0.18f,
            lifetime = 0.30f,
            fadeIn = 0f,
            fadeOut = 0.45f,
            fadeInEase = KEase.Linear,
            fadeOutEase = KEase.EaseOut,
            startSize = 8.0f,
            maxSize = 5.5f,
            endSize = 1.5f,
            expandEnd = 0.12f,
            holdEnd = 0.45f,
            expandEase = KEase.EaseOut,
            shrinkEase = KEase.Linear,
            offset = new Vector3(0f, 0.8f, 0f)
        };

        [Header("── 색 : 머즐 플래시 ──")]
        [Tooltip("그라데이션 축 = 시간(머즐 수명). head=발사 순간, tail=소멸 직전.")]
        public KColorSet muzzleColors = new KColorSet();

        [Header("── 색 : 탄두 ──")]
        [Tooltip("그라데이션 축 = 시간(탄두 수명). head=발사 순간, tail=소멸 직전.")]
        public KColorSet headColors = new KColorSet();

        [Header("── 색 : 궤적 ──")]
        [Tooltip("★그라데이션 축 = 공간(획 길이). head=탄두 쪽, tail=총구 쪽. " +
                 "휘도 감쇠는 위 '공간 테이퍼'가 담당하므로 여기서는 색만 다룬다.")]
        public KColorSet trailColors = new KColorSet();

        [Header("── 색 : 탄착 ──")]
        [Tooltip("그라데이션 축 = 시간(탄착 수명). head=착탄 순간, tail=소멸 직전.")]
        public KColorSet impactColors = new KColorSet();

        [Header("── 발사 지점 폴백 ──")]
        [Tooltip("총구 소켓을 못 받았을 때 캐스터 루트 기준으로 쓸 오프셋.")]
        public Vector3 casterFallbackOffset = new Vector3(0.35f, 1.0f, 0.15f);
    }
}
