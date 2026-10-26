using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Environment
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("JC Environment/월드맵 구름 그림자")]
    public sealed class JcCloudShadowController : MonoBehaviour
    {
        public const string ExplorationScene = "Assets/_ProtoType_Merge/Scenes/DHScene_3.unity";

        [SerializeField, InspectorName("대상 카메라"), Tooltip("구름 그림자를 적용할 메인 탐사 카메라입니다. 씬 재생성으로 참조가 사라지면 같은 씬의 MainCamera를 다시 찾습니다. 다른 씬에는 적용하지 않습니다.")]
        private Camera targetCamera;
        [Header("구름 그림자")]
        [InspectorName("강도"), Range(0, 1f), Tooltip("구름 색상의 최대 합성 비율입니다. 0=끄기, 1=완전한 색상 표시. 녹색과 강도 1로 흐름을 확인할 수 있습니다.")]
        public float strength = .16f;
        [InspectorName("그림자 색상"), ColorUsage(false), Tooltip("기본 검정은 자연스러운 그림자, 선명한 녹색은 흐름 확인용입니다. 알파는 사용하지 않습니다.")]
        public Color shadowColor = Color.black;
        [InspectorName("크기"), Min(1), Tooltip("구름 덩어리의 기준 크기(월드 단위)입니다. 값이 클수록 넓은 그림자가 됩니다. 카메라 확대와 무관합니다.")]
        public float size = 18;
        [InspectorName("속도"), Min(0), Tooltip("구름 그림자가 이동하는 속도(월드 단위/초)입니다. 0이면 이동과 형태 변화를 멈춥니다.")]
        public float speed = .35f;
        [InspectorName("방향 (도)"), Range(0, 360), Tooltip("월드 XZ 평면의 이동 방향입니다. 0도=+X, 90도=+Z, 180도=-X, 270도=-Z.")]
        public float direction = 35;
        [InspectorName("경계 부드러움"), Range(.05f, .5f), Tooltip("그림자 경계의 부드러움입니다. 값이 클수록 밝고 어두운 영역이 완만하게 이어집니다.")]
        public float softness = .18f;
        [InspectorName("구름 양"), Range(.1f, .9f), Tooltip("구름이 덮는 양입니다. 높일수록 그림자 영역이 넓어집니다.")]
        public float coverage = .5f;
        [Header("미리보기")]
        [InspectorName("Scene 뷰 표시"), Tooltip("메인 탐사씬의 Scene 뷰에서도 표시합니다. 실제 게임 카메라 적용에는 영향이 없습니다.")]
        public bool showInSceneView = true;
        [InspectorName("편집 중 애니메이션"), Tooltip("편집 모드에서도 구름을 움직입니다. 끄면 현재 모양을 고정한 채 강도·크기·방향을 조절할 수 있습니다.")]
        public bool animateInEditor = true;

        private static readonly List<JcCloudShadowController> Controllers = new List<JcCloudShadowController>();
        private double clock;
        private Vector2 offset;
        private float travel;
#if UNITY_EDITOR
        private double previousEditorTime;
#endif
        public float AnimationTime => (float)clock;
        public Vector2 Offset => offset;
        public float Travel => travel;
        public Camera TargetCamera
        {
            get
            {
                if (targetCamera == null)
                    foreach (var root in gameObject.scene.GetRootGameObjects())
                        foreach (var camera in root.GetComponentsInChildren<Camera>())
                            if (camera.CompareTag("MainCamera")) { targetCamera = camera; return camera; }
                return targetCamera;
            }
        }

        private void OnEnable()
        {
            if (!Controllers.Contains(this)) Controllers.Add(this);
#if UNITY_EDITOR
            previousEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        private void Update()
        {
            if (Application.IsPlaying(gameObject)) Advance(Time.deltaTime);
        }

        private void Advance(float delta)
        {
            clock += delta;
            float step = Mathf.Max(0, speed) * delta;
            float angle = direction * Mathf.Deg2Rad;
            offset += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
            travel += step;
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            double delta = now - previousEditorTime;
            if (delta < 1.0 / 20) return;
            previousEditorTime = now;
            if (Application.isPlaying || !isActiveAndEnabled || !animateInEditor || speed <= 0 || strength <= 0
                || gameObject.scene.path != ExplorationScene) return;
            Advance((float)System.Math.Min(delta, .2));
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif

        private void OnValidate()
        {
            strength = Mathf.Clamp01(strength);
            size = Mathf.Max(1, size); speed = Mathf.Max(0, speed);
            direction = Mathf.Repeat(direction, 360);
            softness = Mathf.Clamp(softness, .05f, .5f);
            coverage = Mathf.Clamp(coverage, .1f, .9f);
#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void OnDisable()
        {
            Controllers.Remove(this);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public static bool TryGet(Camera camera, out JcCloudShadowController controller)
        {
            controller = null;
            if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return false;
            Scene scene = camera.cameraType == CameraType.SceneView ? SceneManager.GetActiveScene() : camera.gameObject.scene;
            if (scene.path != ExplorationScene) return false;
            for (int i = Controllers.Count - 1; i >= 0; i--)
            {
                var item = Controllers[i];
                if (item == null) { Controllers.RemoveAt(i); continue; }
                if (!item.isActiveAndEnabled || item.gameObject.scene != scene || item.strength <= 0) continue;
                if (camera.cameraType == CameraType.SceneView ? !item.showInSceneView : camera != item.TargetCamera) continue;
                controller = item; return true;
            }
            return false;
        }
    }
}
