using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Indicators
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("JC Indicators/건물 호버 윤곽선 설정")]
    public sealed class JcBuildingHoverOutlineController : MonoBehaviour
    {
        [SerializeField, InspectorName("윤곽선 색상"), ColorUsage(false), Tooltip("입장 가능한 건물에 호버할 때 표시하는 윤곽선과 순환 하이라이트의 기본 색상입니다. 튜토리얼 안내에는 적용하지 않습니다.")]
        private Color outlineColor = new Color(25f / 255f, 1f, 84f / 255f, 1f);
        [SerializeField, InspectorName("윤곽선 인텐시티"), Range(0f, 4f), Tooltip("고정 윤곽선의 강도입니다. 1은 기본값, 0은 고정 윤곽선만 숨깁니다. 하이라이트 강도는 별도로 조절합니다.")]
        private float outlineIntensity = 1f;
        [SerializeField, InspectorName("하이라이트 순환 속도"), Range(0f, 3f), Tooltip("초당 회전 수입니다. 기본 0.4167은 약 2.4초에 한 바퀴이며, 0은 정지합니다.")]
        private float highlightSpeed = 1f / 2.4f;
        [SerializeField, InspectorName("하이라이트 인텐시티"), Range(0f, 4f), Tooltip("마주 보는 두 순환 하이라이트의 강도입니다. 1은 기본값, 0은 하이라이트만 숨깁니다.")]
        private float highlightIntensity = 1f;
        [SerializeField, InspectorName("하이라이트 범위"), Range(0f, 1f), Tooltip("두 하이라이트가 차지하는 둘레 범위입니다. 0은 하이라이트 없음, 1은 윤곽선 전체 하이라이트입니다.")]
        private float highlightRange = .35f;
        [SerializeField, InspectorName("하이라이트 경계 스무딩"), Range(0f, 1f), Tooltip("하이라이트 양쪽 경계의 감쇠 비율입니다. 0은 선명한 경계, 1은 넓고 부드러운 경계입니다. 범위 1에서는 전체가 균일하게 강조됩니다.")]
        private float highlightSmoothing = .65f;
        [SerializeField, InspectorName("흰색 기본선 두께"), Range(0f, 12f), Tooltip("색상 윤곽선 바로 안쪽, 건물 표면 쪽에 그어지는 흰색 선의 두께입니다. 화면 픽셀 단위이며 기본 2px, 0이면 숨깁니다. 바깥쪽 색상 윤곽선과 분리되어 강도를 높여도 흰색 선을 가리지 않습니다.")]
        private float whiteOutlineWidth = 2f;

        [SerializeField, InspectorName("윤곽선 경계 스무딩"), Range(0f, 2f), Tooltip("완성된 녹색선과 안쪽 흰색선의 안팎 경계를 화면 픽셀 단위로 부드럽게 합니다. 기본 1px, 0이면 추가 합성 패스를 끕니다. 건물 표면은 흐리지 않으며 하이라이트 경계 스무딩과 별개입니다.")]
        private float edgeSmoothing = 1f;

        private static readonly List<JcBuildingHoverOutlineController> Controllers = new List<JcBuildingHoverOutlineController>();
        public JcBuildingHoverOutlineValues Values
        {
            get => new JcBuildingHoverOutlineValues { outlineColor = outlineColor, outlineIntensity = outlineIntensity,
                highlightSpeed = highlightSpeed, highlightIntensity = highlightIntensity,
                shapeVersion = 4, highlightRange = highlightRange, highlightSmoothing = highlightSmoothing,
                whiteOutlineWidth = whiteOutlineWidth, edgeSmoothing = edgeSmoothing };
            set { outlineColor = value.outlineColor; outlineIntensity = value.outlineIntensity;
                highlightSpeed = value.highlightSpeed; highlightIntensity = value.highlightIntensity;
                // 이전 네 항목 프로필도 원래 값을 유지하면서 새 항목은 기본값으로 확장한다.
                highlightRange = value.shapeVersion > 0 ? Mathf.Clamp01(value.highlightRange) : .35f;
                highlightSmoothing = value.shapeVersion > 0 ? Mathf.Clamp01(value.highlightSmoothing) : .65f;
                whiteOutlineWidth = value.shapeVersion >= 2 ? Mathf.Clamp(value.whiteOutlineWidth, 0f, 12f) : 2f;
                edgeSmoothing = value.shapeVersion >= 3 ? Mathf.Clamp(value.edgeSmoothing, 0f, 2f) : 1f; }
        }

        public void LoadSavedSettings()
        {
            var profile = Resources.Load<JcBuildingHoverOutlineProfile>("JC_BuildingHover/" + gameObject.scene.name);
            if (profile != null) Values = profile.values;
        }

        private void OnEnable()
        {
            if (!Controllers.Contains(this)) Controllers.Add(this);
            LoadSavedSettings();
        }
        private void OnDisable() => Controllers.Remove(this);
        private void OnDestroy() => Controllers.Remove(this);

        public static void ResolveAntialiasing(Scene scene, out float smoothing)
        {
            smoothing = 1f;
            for (int i = Controllers.Count - 1; i >= 0; i--)
            {
                var c = Controllers[i];
                if (c == null || !c.isActiveAndEnabled || c.gameObject.scene != scene) continue;
                smoothing = Mathf.Clamp(c.edgeSmoothing, 0f, 2f);
                return;
            }
        }

        public static void Resolve(Scene scene, out Color color, out Vector3 tuning, out Vector3 shape)
        {
            color = new Color(25f / 255f, 1f, 84f / 255f, 1f);
            tuning = new Vector3(1f, 1f / 2.4f, 1f);
            shape = new Vector3(.35f, .65f, 2f);
            for (int i = Controllers.Count - 1; i >= 0; i--)
            {
                var c = Controllers[i];
                if (c == null) { Controllers.RemoveAt(i); continue; }
                if (!c.isActiveAndEnabled || c.gameObject.scene != scene) continue;
                color = c.outlineColor; color.a = 1f;
                tuning = new Vector3(Mathf.Clamp(c.outlineIntensity, 0f, 4f),
                    Mathf.Clamp(c.highlightSpeed, 0f, 3f), Mathf.Clamp(c.highlightIntensity, 0f, 4f));
                shape = new Vector3(Mathf.Clamp01(c.highlightRange), Mathf.Clamp01(c.highlightSmoothing), Mathf.Clamp(c.whiteOutlineWidth, 0f, 12f));
                return;
            }
        }
    }
}
