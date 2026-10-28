using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「체인 라이팅」 연출 프리셋.
    /// 볼트(LightningBolt)·감전 아우라(ShockAura) 재질 + ChainLightningVfx 타이밍을 한 곳에서 튜닝.
    /// 에디트 모드에서는 previewProgress/previewSeed로 볼트 긋기·플리커 형상을 정지 프레임 스크럽.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Chain Lightning Preset (체인 라이팅)", fileName = "FX_ChainLightningPreset")]
    public class ChainLightningPreset : ScriptableObject
    {
        [Header("볼트 — 색")]
        [Tooltip("볼트 코어 색(백열 청백).")]
        [ColorUsage(true, true)] public Color boltCoreColor = new Color(1.5f, 1.65f, 1.95f);
        [Tooltip("볼트 글로우 색(흐린 파랑).")]
        [ColorUsage(true, true)] public Color boltGlowColor = new Color(0.38f, 0.62f, 1.55f);
        [Tooltip("볼트 발광 배수.")]
        [Range(0, 6)] public float boltEmission = 1.6f;

        [Header("볼트 — 형상")]
        [Tooltip("볼트 절반 폭(m).")]
        [Range(0.02f, 1f)] public float boltWidth = 0.18f;
        [Tooltip("대 꺾임 세그먼트 수. 많을수록 잘게 꺾인다.")]
        [Range(3, 32)] public int segCount = 14;
        [Tooltip("대 꺾임 진폭(폭 단위 0~0.85).")]
        [Range(0, 0.85f)] public float jitterAmp = 0.5f;
        [Tooltip("미세 지그 진폭(2옥타브).")]
        [Range(0, 0.5f)] public float microJag = 0.18f;
        [Tooltip("미세 지그 빈도(대 세그먼트 배수).")]
        [Range(1, 6)] public float microFreq = 3f;
        [Tooltip("양 끝 고정 구간(0~0.5). 시작·도착점에 중심선이 수렴.")]
        [Range(0.01f, 0.5f)] public float endPin = 0.12f;
        [Tooltip("코어 절반 폭(폭 단위).")]
        [Range(0.01f, 0.5f)] public float coreWidth = 0.09f;
        [Tooltip("글로우 감쇠 폭(폭 단위).")]
        [Range(0.05f, 1f)] public float glowWidth = 0.42f;
        [Tooltip("헤드 플래시 크기(길이 단위). 그어지는 선의 선두 광점.")]
        [Range(0.01f, 0.3f)] public float headSize = 0.06f;
        [Tooltip("헤드 플래시 강도.")]
        [Range(0, 6)] public float headBoost = 2.2f;

        [Header("볼트 — 가지")]
        [Tooltip("분기 가지 수(0~3).")]
        [Range(0, 3)] public int branchCount = 2;
        [Tooltip("가지 길이(길이 단위).")]
        [Range(0.02f, 0.4f)] public float branchLen = 0.14f;
        [Tooltip("가지 기울기(폭/길이). 클수록 가파르게 벌어진다.")]
        [Range(0, 6f)] public float branchSlope = 3.2f;
        [Tooltip("가지 폭 배율(코어 대비).")]
        [Range(0.1f, 1f)] public float branchWidthMul = 0.55f;

        [Header("감전 아우라 — 색")]
        [Tooltip("아크 하이라이트(심지) 색. 볼트 코어와 동일 조합 권장.")]
        [ColorUsage(true, true)] public Color shockCoreColor = new Color(1.5f, 1.65f, 1.95f);
        [Tooltip("아크 몸통·글로우 색. 볼트 글로우와 동일 조합 권장.")]
        [ColorUsage(true, true)] public Color shockGlowColor = new Color(0.38f, 0.62f, 1.55f);
        [Tooltip("아우라 발광 배수.")]
        [Range(0, 6)] public float shockEmission = 1.5f;

        [Header("감전 아우라 — 형상")]
        [Tooltip("링 반경(uv).")]
        [Range(0.1f, 0.9f)] public float shockRadius = 0.5f;
        [Tooltip("링 세로 비율(1=정원). 몸 실루엣 근사용 타원.")]
        [Range(0.5f, 2f)] public float shockEllipseK = 1.25f;
        [Tooltip("아크별 반경 지터.")]
        [Range(0, 0.6f)] public float radJitter = 0.25f;
        [Tooltip("아크 섹터 수.")]
        [Range(4, 24)] public int arcCount = 11;
        [Tooltip("아크 밀도(표시 비율). 플리커마다 다른 조합이 켜진다.")]
        [Range(0, 1)] public float arcDensity = 0.65f;
        [Tooltip("아크 절반 폭(uv).")]
        [Range(0.005f, 0.15f)] public float arcWidth = 0.035f;
        [Tooltip("아크 하이라이트 폭 비율. 색 몸통 안의 백열 심지 — 입체감.")]
        [Range(0, 1)] public float arcHighlightRatio = 0.45f;
        [Tooltip("아크 각진 워블 진폭(uv). 지글거림의 크기.")]
        [Range(0, 0.25f)] public float wobbleAmp = 0.15f;
        [Tooltip("아크 워블 세그먼트 수. 클수록 파장이 짧다.")]
        [Range(4, 48)] public float wobbleFreq = 42f;
        [Tooltip("아크 글로우 양.")]
        [Range(0, 2)] public float glowAmt = 0.8f;
        [Tooltip("내부 글로우(감전 아우라 기본값).")]
        [Range(0, 3)] public float shockCoreGlow = 0.35f;
        [Tooltip("머즐 구체의 내부 글로우 오버라이드(MPB). 작은 번개 구체의 밝은 심지.")]
        [Range(0, 3)] public float muzzleCoreGlow = 1.6f;

        [Header("감전 배경 버스트(알파 블렌드)")]
        [Tooltip("배경 몸통 색(어두운 남색 실루엣). 알파 블렌드로 배경을 덮어 가산 아크를 광원처럼 강조.")]
        public Color bgBodyColor = new Color(0.07f, 0.13f, 0.34f);
        [Tooltip("배경 중심 색(밝은 파랑).")]
        [ColorUsage(true, true)] public Color bgCenterColor = new Color(0.28f, 0.52f, 1.1f);
        [Tooltip("배경 중심 글로우 크기(uv).")]
        [Range(0.05f, 0.8f)] public float bgCenterSize = 0.3f;
        [Tooltip("배경 스파이크 수.")]
        [Range(4, 32)] public int bgSpikeCount = 14;
        [Tooltip("배경 스파이크 최대 길이(uv).")]
        [Range(0.2f, 1f)] public float bgSpikeLen = 0.85f;
        [Tooltip("배경 스파이크 길이 지터.")]
        [Range(0, 1)] public float bgLenJitter = 0.5f;
        [Tooltip("배경 스파이크 밑동 절반 폭(uv).")]
        [Range(0.02f, 0.4f)] public float bgSpikeWidth = 0.14f;
        [Tooltip("배경 스파이크 테이퍼 샤프니스.")]
        [Range(0.5f, 6f)] public float bgTaperSharp = 2f;
        [Tooltip("배경 최대 불투명도.")]
        [Range(0, 1)] public float bgOpacity = 0.85f;

        [Header("타이밍")]
        [Tooltip("머즐 팝 시간(초).")]
        [Range(0.02f, 0.5f)] public float muzzleTime = 0.12f;
        [Tooltip("본볼트가 그어지는 시간(초).")]
        [Range(0.02f, 0.5f)] public float drawTime = 0.08f;
        [Tooltip("본볼트 유지(플리커) 시간(초).")]
        [Range(0.05f, 1.5f)] public float holdTime = 0.25f;
        [Tooltip("볼트·머즐 페이드 시간(초).")]
        [Range(0.02f, 0.6f)] public float fadeTime = 0.15f;
        [Tooltip("플리커 리롤 빈도(회/초).")]
        [Range(2, 60)] public float flickerRate = 18f;
        [Tooltip("피격 후 연쇄까지 지연(초). 기획 범위 0.1~1.0.")]
        [Range(0.1f, 1f)] public float chainDelay = 0.35f;
        [Tooltip("연쇄 볼트가 그어지는 시간(초).")]
        [Range(0.02f, 0.5f)] public float chainDrawTime = 0.06f;
        [Tooltip("연쇄 볼트 유지 시간(초).")]
        [Range(0.05f, 1.5f)] public float chainHoldTime = 0.2f;
        [Tooltip("감전 잔류 시간(초). 기획 기본 1초.")]
        [Range(0.2f, 3f)] public float shockDuration = 1f;
        [Tooltip("감전 페이드 시간(초).")]
        [Range(0.05f, 1f)] public float shockFadeTime = 0.25f;

        [Header("배치")]
        [Tooltip("머즐 구체 위치(캐스터 로컬 오프셋). 캐릭터 전방.")]
        public Vector3 muzzleOffset = new Vector3(0f, 1.15f, 0.5f);
        [Tooltip("머즐 구체 월드 크기(m).")]
        [Range(0.1f, 1.5f)] public float muzzleSize = 0.4f;
        [Tooltip("감전 아우라 월드 크기(m).")]
        [Range(0.5f, 4f)] public float shockSize = 1.7f;
        [Tooltip("피격점 높이(m, 대상 피벗 기준).")]
        [Range(0, 2f)] public float targetHeight = 0.8f;

        [Header("디버그")]
        [Tooltip("에디트 모드 정지 프레임: 볼트 긋기 진행도(0~1).")]
        [Range(0, 1)] public float previewProgress = 1f;
        [Tooltip("에디트 모드 정지 프레임: 플리커 시드. 바꾸면 볼트·아크 형상 리롤.")]
        [Range(0, 512)] public float previewSeed = 0f;
    }
}
