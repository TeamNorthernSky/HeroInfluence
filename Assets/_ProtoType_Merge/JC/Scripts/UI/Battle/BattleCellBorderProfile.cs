using System;
using UnityEngine;

[Serializable]
public struct BattleCellBorderShape
{
    [Tooltip("기준 셀의 가로·세로 크기입니다.")] public Vector2 size;
    [Min(0), Tooltip("셀의 각 변에서 안쪽으로 줄이는 여백입니다.")] public float padding;
    [Min(0), Tooltip("둥근 모서리의 반지름입니다.")] public float cornerRadius;
    [Range(2, 32), Tooltip("모서리 하나의 곡선 분할 수입니다. 높을수록 매끄럽습니다.")] public int curveSegments;
    [Min(.001f), Tooltip("은회색 바깥 보조선의 가로 폭입니다.")] public float outerWidth;
    [Min(.001f), Tooltip("대상색 본체 테두리의 가로 폭입니다.")] public float mainWidth;
    [Min(.001f), Tooltip("안쪽 보조선의 가로 폭입니다.")] public float innerWidth;
    [Min(.001f), Tooltip("메시 자체의 세로 두께입니다. 밑면에서 윗면까지의 높이입니다.")] public float depth;
    [Min(0), Tooltip("셀 기준면에서 메시 밑면까지의 기본 부유 높이입니다.")] public float floatHeight;

    public static BattleCellBorderShape Default => new BattleCellBorderShape {
        size = new Vector2(3.3f, 3.3f), padding = .1f, cornerRadius = .26f,
        curveSegments = 8, outerWidth = .035f, mainWidth = .11f, innerWidth = .035f,
        depth = .08f, floatHeight = .08f
    };
}

[Serializable]
public sealed class BattleCellBorderStyle
{
    [Tooltip("이 상태의 테두리 형태입니다.")] public BattleCellBorderShape shape = BattleCellBorderShape.Default;
    [Tooltip("바깥 보조선을 표시합니다. 끄면 본체가 바깥 경계까지 확장됩니다.")] public bool showOuter = true;
    [Tooltip("안쪽 보조선을 표시합니다.")] public bool showInner = true;
    [Tooltip("바깥·안쪽 보조선에 함께 적용할 색과 불투명도입니다.")] public Color outlineColor = new Color(.86f, .9f, .94f, 1);
    [Tooltip("대상 구분색과 불투명도입니다.")] public Color mainColor = new Color(1f, .8f, .08f, 1);
    [Tooltip("이 상태에서 스윕을 허용합니다.")] public bool allowSweep = true;
    [Tooltip("내부 채움을 표시합니다. 채움도 테두리와 함께 상하 이동합니다.")] public bool useFill;
    [Tooltip("내부 채움의 색과 알파입니다.")] public Color fillColor = Color.red;
    [Range(0, 1), Tooltip("테두리와 독립적인 내부 채움 불투명도입니다.")] public float fillOpacity;
    [Tooltip("선택적인 내부 채움 문양입니다.")] public Texture2D fillTexture;
}

[Serializable]
public sealed class BattleCellBorderConfiguration
{
    [Tooltip("노란 현재 턴 표시입니다.")] public BattleCellBorderStyle currentTurn = new BattleCellBorderStyle();
    [Tooltip("보라색 선택 가능 표시입니다.")] public BattleCellBorderStyle selectable = new BattleCellBorderStyle { mainColor = new Color(.68f,.2f,1,1) };
    [Tooltip("빨간 확정 대상 표시입니다. 이 상태만 공통 왕복 속도의 1.5배로 움직입니다.")] public BattleCellBorderStyle confirmedArea = new BattleCellBorderStyle { mainColor = new Color(1,.12f,.1f,1) };
    [Tooltip("선택 불가 표시입니다.")] public BattleCellBorderStyle unavailable = new BattleCellBorderStyle { mainColor = new Color(.13f,.14f,.16f,1), outlineColor = new Color(.2f,.22f,.25f,1), allowSweep = false, useFill = true, fillOpacity = .25f };
    [Min(.1f), Tooltip("기본 상태가 위아래로 한 번 왕복하는 시간(초)입니다. 빨간 확정 대상은 1.5배 빠릅니다.")] public float bobPeriod = 2.4f;
    [Min(0), Tooltip("기본 부유 높이에서 위아래로 움직이는 한쪽 폭입니다. 0이면 정지합니다.")] public float bobAmplitude = .04f;
    [Min(0), Tooltip("상태 변경 시 새 공통 움직임에 부드럽게 합류하는 시간(초)입니다. 0이면 즉시 맞춥니다.")] public float resyncDuration = .3f;
    [Tooltip("전체 스윕을 켭니다.")] public bool useSweep = true;
    [ColorUsage(true,true), Tooltip("테두리 스윕에 곱할 색과 강도입니다.")] public Color sweepColor = Color.white;
    [Range(0,8), Tooltip("스윕 밝기입니다.")] public float sweepIntensity = 2f;
    [Range(.01f,.5f), Tooltip("빛띠의 반쪽 폭입니다.")] public float sweepWidth = .12f;
    [Range(0,1), Tooltip("빛띠 주변의 부드러운 번짐입니다.")] public float sweepSoftness = .5f;
    [Range(-80,80), Tooltip("빛띠 기울기(도)입니다.")] public float sweepTilt = 18f;
    [Min(.05f), Tooltip("스윕이 한 번 지나가는 시간(초)입니다.")] public float sweepDuration = .8f;
    [Min(0), Tooltip("스윕 반복 사이의 대기 시간(초)입니다.")] public float sweepInterval = 1.5f;
    [Tooltip("스윕 이동 방향을 반대로 바꿉니다.")] public bool reverseSweep;

    public BattleCellBorderStyle For(BattleCellVisualState state)
    {
        switch (state) {
            case BattleCellVisualState.Selectable: return selectable;
            case BattleCellVisualState.ConfirmedArea: return confirmedArea;
            case BattleCellVisualState.Unavailable: return unavailable;
            default: return currentTurn;
        }
    }
    public void UnifyShape(BattleCellVisualState source)
    {
        var shape = For(source).shape;
        currentTurn.shape = shape; selectable.shape = shape; confirmedArea.shape = shape; unavailable.shape = shape;
    }
    public BattleCellBorderConfiguration Copy() => JsonUtility.FromJson<BattleCellBorderConfiguration>(JsonUtility.ToJson(this));
}

[CreateAssetMenu(menuName = "JC/전투 셀 테두리 프로필")]
public sealed class BattleCellBorderProfile : ScriptableObject
{
    [Tooltip("설정 씬에서 저장한 전투 테두리 설정입니다.")]
    public BattleCellBorderConfiguration settings = new BattleCellBorderConfiguration();
}
