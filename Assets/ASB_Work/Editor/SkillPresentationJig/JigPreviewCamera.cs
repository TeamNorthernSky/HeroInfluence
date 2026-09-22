using UnityEngine;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// 지그 프리뷰용 카메라. <b>Game View가 직접 그린다</b> — 창 안에 뷰포트를 두지 않는다.
    ///
    /// 동작 방식:
    ///   · 에디트 모드 — <c>enabled = true</c> + 높은 <c>depth</c>로 Game View를 잡는다.
    ///     Timeline 창에서 플레이헤드를 끌면 Game View가 그대로 따라간다(별도 갱신 코드가 필요 없다).
    ///   · 플레이 모드 — <see cref="JigPreviewInstance"/>가 playModeStateChanged 훅에서 프리뷰 인스턴스를
    ///     통째로 파괴하므로 이 카메라도 함께 사라지고, 원래 게임 카메라가 그대로 잡는다.
    ///
    /// 씬의 기존 카메라를 비활성화하지 않는다 — <c>depth</c>만 높여 위에 그린다.
    /// 지그를 삭제하면 원래대로 돌아온다.
    ///
    /// 카메라는 보통의 씬 오브젝트다. 각도를 잡을 때는 하이어라키에서 선택해 Scene View 기즈모로 옮기면 된다.
    /// 아래 프리셋·프레이밍은 처음 위치를 잡아주는 보조 수단일 뿐이다.
    ///
    /// <b>편집 편의 전용이다.</b> 이 각도는 자산에 저장되지 않고 게임 연출과 무관하다.
    /// 스킬 연출로서 카메라를 움직이려면 별도의 런타임 카메라 컨트롤러가 필요하다(현재 프로젝트에 없음).
    /// </summary>
    public static class JigPreviewCamera
    {
        public enum Angle
        {
            Front,
            ThreeQuarter,
            Right,
            Back,
            Top,
        }

        public const string CameraName = "[Jig] PreviewCamera";

        /// <summary>기존 씬 카메라(보통 depth 0) 위에 그리기 위한 값.</summary>
        private const float OverlayDepth = 100f;

        private static Camera _camera;

        // 프레이밍 기준값. 애니메이션 중 팔다리가 움직이면 바운즈가 흔들리므로 매 프레임 재계산하지 않는다.
        private static Vector3 _focus;
        private static float _radius = 1f;

        public static Camera Current => _camera;
        public static bool IsAlive => _camera != null;

        /// <summary>프리뷰 인스턴스에 카메라를 붙인다. 이미 있으면 재사용한다.</summary>
        public static Camera Ensure(GameObject previewRoot)
        {
            if (previewRoot == null)
            {
                return null;
            }

            if (_camera == null || _camera.transform.parent != previewRoot.transform)
            {
                var go = new GameObject(CameraName);
                go.transform.SetParent(previewRoot.transform, false);
                go.hideFlags |= HideFlags.DontSaveInEditor;

                _camera = go.AddComponent<Camera>();
                _camera.enabled = true;                          // Game View가 이 카메라로 그린다
                _camera.depth = OverlayDepth;                    // 기존 카메라를 끄지 않고 위에 그린다
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.17f, 0.18f, 0.20f, 1f);
                _camera.nearClipPlane = 0.03f;
                _camera.farClipPlane = 200f;
                _camera.fieldOfView = 45f;

                Frame(previewRoot);
            }

            return _camera;
        }

        /// <summary>렌더러 바운즈로 초점과 거리를 다시 잡고 프리셋 각도를 적용한다.</summary>
        public static void Frame(GameObject previewRoot)
        {
            if (previewRoot == null)
            {
                return;
            }

            Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }
                _focus = b.center;
                _radius = Mathf.Max(b.extents.magnitude, 0.4f);
            }
            else
            {
                _focus = previewRoot.transform.position + Vector3.up;
                _radius = 1f;
            }

            ApplyPreset(Angle.ThreeQuarter);
        }

        /// <summary>초점을 향하는 프리셋 각도로 카메라를 옮긴다.</summary>
        public static void ApplyPreset(Angle angle)
        {
            if (_camera == null)
            {
                return;
            }

            float yaw;
            float pitch;
            switch (angle)
            {
                case Angle.Front:        yaw = 0f;   pitch = 8f;  break;
                case Angle.Right:        yaw = 90f;  pitch = 8f;  break;
                case Angle.Back:         yaw = 180f; pitch = 10f; break;
                case Angle.Top:          yaw = 25f;  pitch = 65f; break;
                default:                 yaw = 35f;  pitch = 14f; break;   // ThreeQuarter
            }

            Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
            float distance = Mathf.Max(0.2f, _radius * 2.4f);

            _camera.transform.position = _focus + dir * distance;
            _camera.transform.LookAt(_focus);
        }

        /// <summary>참조만 버린다. 카메라 오브젝트는 프리뷰 인스턴스와 함께 파괴된다.</summary>
        public static void Forget()
        {
            _camera = null;
        }
    }
}
