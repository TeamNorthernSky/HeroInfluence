using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬의 머리 위 바 표시 설정. 전투 수치와 유닛 수명은 변경하지 않습니다.</summary>
public sealed class BattleUnitBarVisualSettings : MonoBehaviour
{
    public enum LossCurve { Linear, Accelerate, Decelerate, Custom }

    [Serializable]
    public sealed class LossMotion
    {
        [Tooltip("게이지 변화 완료까지의 실제 초입니다. 변화량·전투 배속과 무관하며 0이면 즉시 완료합니다. 머리 위 바는 일시정지 중 멈춥니다.")]
        [Min(0f)] public float duration;
        [Tooltip("게이지 변화 속도입니다. Linear=일정, Accelerate=점점 빠르게, Decelerate=점점 느리게, Custom=아래 곡선. 총 시간은 동일합니다.")]
        public LossCurve curve = LossCurve.Linear;
        [Tooltip("Custom에서 사용합니다. 가로는 경과 시간 0~1, 세로는 변화 진행률 0~1입니다. 역행은 막고 마지막에는 목표값에 도달합니다.")]
        public AnimationCurve customCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public LossMotion(float seconds) { duration = seconds; }
        public float Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            switch (curve)
            {
                case LossCurve.Accelerate: return t * t;
                case LossCurve.Decelerate: return 1f - (1f - t) * (1f - t);
                case LossCurve.Custom: return Mathf.Clamp01(customCurve != null ? customCurve.Evaluate(t) : t);
                default: return t;
            }
        }
    }

    [Header("머리 위 바 배치")]
    [Tooltip("모델별 기준 높이에서 더 띄울 화면 여백입니다. 1080px 화면 기준이며 화면 높이에 비례합니다. 이 씬의 모든 머리 위 바에 적용합니다.")]
    public Vector2 screenOffset = new Vector2(0f, 34f);
    [Tooltip("1080px 화면에서 바의 가로 폭입니다. 최소 1px이며 카메라 거리와 모델 배율의 영향을 보정합니다.")]
    [Min(1f)] public float screenWidth = 170f;
    [Tooltip("HP/IP 각 행의 두께입니다. 프리팹 RectTransform 단위이며 화면 표시 크기는 가로 폭과 함께 환산됩니다.")]
    [Min(1f)] public float rowHeight = 22f;
    [Tooltip("HP와 IP 사이의 빈 간격입니다. 프리팹 RectTransform 단위이며 0이면 두 행이 붙습니다.")]
    [Min(0f)] public float rowGap = 6f;
    [Tooltip("바 안 숫자의 글자 크기입니다. TMP 기준이며 화면 표시 크기는 바와 함께 환산됩니다.")]
    [Min(1f)] public float fontSize = 17f;

    [Header("HP 소모 구간")]
    [Tooltip("HP 감소 시간과 속도 곡선입니다. 실제 HP와 숫자는 즉시 반영하고 소모 구간만 이 설정으로 줄입니다.")]
    public LossMotion hpLoss = new LossMotion(0.5f);
    [Tooltip("HP 소모 구간의 시간별 색입니다. 왼쪽=피격 순간, 오른쪽=완료 순간. 속도 곡선과 별개로 시간에 따라 변합니다.")]
    public Gradient hpLossColors = CreateHpGradient();

    [Header("IP 소모 구간")]
    [Tooltip("IP 감소 시간과 속도 곡선입니다. 실제 IP와 숫자는 즉시 반영하며 HP와 독립적으로 동작합니다.")]
    public LossMotion ipLoss = new LossMotion(0.2f);
    [Tooltip("IP 소모 구간의 색입니다. 기본은 흰색이며 남은 IP의 파란색은 유지합니다.")]
    public Color ipLossColor = Color.white;

    [Header("HP 회복 · 부활")]
    [Tooltip("HP 회복·부활 시 증가 구간의 시간과 곡선입니다. 실제 HP와 숫자는 즉시 반영합니다.")]
    public LossMotion hpRecovery = new LossMotion(0.5f);
    [Tooltip("HP 회복 구간의 시간별 색입니다. 기본 흰색→밝은 연두색이며 완료하면 원래 HP 색으로 돌아갑니다.")]
    public Gradient hpRecoveryColors = CreateRecoveryGradient(new Color(0.80f, 1f, 0.65f));
    [Header("IP 획득")]
    [Tooltip("IP 획득 시 증가 구간의 시간과 곡선입니다. 실제 IP와 숫자는 즉시 반영합니다.")]
    public LossMotion ipRecovery = new LossMotion(0.2f);
    [Tooltip("IP 획득 구간의 시간별 색입니다. 기본 흰색→밝은 하늘색이며 완료하면 원래 IP 색으로 돌아갑니다.")]
    public Gradient ipRecoveryColors = CreateRecoveryGradient(new Color(0.65f, 0.83f, 1f));

    internal static readonly LossMotion DefaultHpRecovery = new LossMotion(0.5f);
    internal static readonly LossMotion DefaultIpRecovery = new LossMotion(0.2f);
    internal static readonly Gradient DefaultHpRecoveryColors = CreateRecoveryGradient(new Color(0.80f, 1f, 0.65f));
    internal static readonly Gradient DefaultIpRecoveryColors = CreateRecoveryGradient(new Color(0.65f, 0.83f, 1f));

    public static Gradient CreateRecoveryGradient(Color endColor)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(endColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    internal static readonly LossMotion DefaultHpLoss = new LossMotion(0.5f);
    internal static readonly LossMotion DefaultIpLoss = new LossMotion(0.2f);
    internal static readonly Gradient DefaultHpColors = CreateHpGradient();
    private static readonly List<BattleUnitBarVisualSettings> Active = new List<BattleUnitBarVisualSettings>();

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);

    public static BattleUnitBarVisualSettings ForScene(Scene scene)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var settings = Active[i];
            if (settings == null) { Active.RemoveAt(i); continue; }
            if (settings.isActiveAndEnabled && settings.gameObject.scene == scene) return settings;
        }
        return null;
    }

    private static Gradient CreateHpGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(1f, 0.12f, 0.08f), 0f),
            new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.5f),
            new GradientColorKey(new Color(1f, 0.9f, 0.08f), 1f)
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}
