using UnityEngine;

[System.Serializable]
public struct ArnoldShakeValues
{
    [InspectorName("발걸음 강도"), Tooltip("발소리가 날 때 카메라를 흔드는 세기입니다(월드 단위). 0이면 발소리만 재생됩니다."), Min(0)] public float footStrength;
    [InspectorName("발걸음 지속 시간(초)"), Tooltip("발을 한 번 디딘 뒤 반동이 끝날 때까지의 시간입니다. 발걸음 간격은 기존 발소리 타이밍을 따릅니다."), Min(.01f)] public float footDuration;
    [InspectorName("채팅 첫 박자 카메라 강도"), Tooltip("굉음 안내 직후 첫 번째 카메라 흔들림의 세기입니다(월드 단위)."), Min(0)] public float cameraStart;
    [InspectorName("채팅 마지막 박자 카메라 강도"), Tooltip("네 번째 카메라 흔들림의 세기입니다(월드 단위). 둘째와 셋째 박자는 첫 값과 이 값 사이에서 고르게 변합니다."), Min(0)] public float cameraEnd;
    [InspectorName("채팅 첫 박자 창 이동량"), Tooltip("첫 박자에 HEROTALK 창을 흔드는 폭입니다(UI 캔버스 단위). 카메라 강도와 별도로 조절합니다."), Min(0)] public float frameStart;
    [InspectorName("채팅 마지막 박자 창 이동량"), Tooltip("네 번째 박자에 HEROTALK 창을 흔드는 폭입니다(UI 캔버스 단위). 첫 값보다 크게 설정하면 박자마다 창이 더 크게 흔들립니다."), Min(0)] public float frameEnd;
    [InspectorName("채팅 한 박자 지속 시간(초)"), Tooltip("한 번의 굉음에 카메라와 창이 흔들렸다가 돌아오는 시간입니다. 게임이 일시정지된 채팅 중에도 실제 시간으로 진행됩니다."), Min(.01f)] public float chatDuration;
    [InspectorName("채팅 박자 간격(초)"), Tooltip("한 박자가 시작된 뒤 다음 박자가 시작될 때까지의 시간입니다. 지속 시간보다 짧게 설정하면 앞 박자가 끝난 뒤 다음 박자가 시작됩니다."), Min(.01f)] public float interval;

    [InspectorName("발걸음 반동 비율(%)"), Tooltip("첫 충격 뒤 위로 반동하는 높이입니다. 30이면 첫 충격의 30%, 0이면 반동 없이 제자리로 돌아옵니다."), Range(0, 100)] public float footReboundPercent;

    public static ArnoldShakeValues Default => new ArnoldShakeValues {
        footStrength = .08f, footDuration = .20f, footReboundPercent = 30, cameraStart = .035f, cameraEnd = .08f,
        frameStart = 2, frameEnd = 5, chatDuration = .14f, interval = .30f
    };
}

[CreateAssetMenu(menuName = "JC/아놀드 셰이크 프로필")]
public sealed class JcArnoldShakeProfile : ScriptableObject
{
    public ArnoldShakeValues values = ArnoldShakeValues.Default;
}
