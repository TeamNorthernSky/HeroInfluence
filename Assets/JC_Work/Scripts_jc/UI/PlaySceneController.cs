using UnityEngine;

/// <summary>
/// PlayScene 초기화 컨트롤러.
/// 카메라 설정을 담당. 터레인은 씬에 프리팹으로 미리 배치.
/// </summary>
public class PlaySceneController : MonoBehaviour
{
    [Header("Camera Edge Scroll Zone (pixels)")]
    [Tooltip("화면 경계로부터 카메라 이동 판정이 시작되는 거리 (픽셀)")]
    [SerializeField] private float edgeZoneX = 120f;
    [SerializeField] private float edgeZoneY = 120f;

    private void Start()
    {
        if (FindFirstObjectByType<PlayGridManagerJC>() == null)
        {
            Debug.LogError("[PlaySceneController] PlayGridManagerJC가 없습니다. GameManager 프리팹에 추가하세요.");
            return;
        }

        SetupCamera();
    }

    private void SetupCamera()
    {
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
        }

        var camController = mainCam.GetComponent<MapCameraController>();
        if (camController == null)
        {
            camController = mainCam.gameObject.AddComponent<MapCameraController>();
        }

        camController.SetEdgeZone(edgeZoneX, edgeZoneY);
    }
}
