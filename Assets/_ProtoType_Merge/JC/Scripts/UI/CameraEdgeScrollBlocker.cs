using UnityEngine;

/// <summary>
/// [KJ 260730] 마우스가 이 UI 위에 있는 동안 카메라 엣지 스크롤을 막는 마커.
/// QuarterViewCameraFollower가 UI 레이캐스트 결과에서 이 컴포넌트를 찾으면 버튼과 동일하게 취급한다.
/// 동작은 없고 존재 자체가 표식이다.
/// </summary>
[DisallowMultipleComponent]
public class CameraEdgeScrollBlocker : MonoBehaviour
{
}
