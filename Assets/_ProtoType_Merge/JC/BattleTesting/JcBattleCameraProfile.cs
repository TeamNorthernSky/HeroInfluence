using System;
using UnityEngine;
namespace JC.BattleTesting
{
    [Serializable]
    public sealed class JcBattleCameraPose
    {
        [Tooltip("원근감 강도 1일 때의 기준 월드 위치입니다. 플레이 중 현재 A/B 초안 변경은 자동 반영됩니다. 원근감을 낮추면 실제 카메라는 이 위치보다 뒤로 이동합니다.")] public Vector3 position;
        [Tooltip("카메라의 월드 회전 각도(X/Y/Z, 도)입니다. 플레이 중 현재 A/B 초안 변경은 자동 반영됩니다.")] public Vector3 rotation;
        [Tooltip("기준 수직 시야각입니다. 플레이 중 현재 A/B 초안 수정은 자동 반영되며 프로필 저장으로 보관합니다. 원근감을 낮추면 화면 중심 크기를 유지하도록 실제 시야각도 함께 줄어듭니다.")] [Range(10f, 100f)] public float fieldOfView = 60f;
        [InspectorName("원근감 강도")]
        [Tooltip("1은 기존 원근감, 낮을수록 먼 캐릭터의 축소가 줄어듭니다. 0.5는 카메라 깊이를 2배로 늘리고 시야각을 줄여 전투 중심의 화면 크기를 유지합니다. 플레이 중 현재 A/B 초안 수정은 자동 반영됩니다. 프로필 저장을 눌러 보관하세요.")]
        [Range(.2f, 1f)] public float perspectiveStrength = 1f;
        public JcBattleCameraPose Copy() => new JcBattleCameraPose { position = position, rotation = rotation, fieldOfView = fieldOfView, perspectiveStrength = perspectiveStrength };

        public void Resolve(Vector3 framingCenter, out Vector3 cameraPosition, out Quaternion cameraRotation, out float cameraFov)
        {
            float strength = Mathf.Clamp(perspectiveStrength, .2f, 1f);
            cameraRotation = Quaternion.Euler(rotation);
            var forward = cameraRotation * Vector3.forward;
            float depth = Vector3.Dot(framingCenter - position, forward);
            // 기준면이 뒤에 있으면 원래 자세를 유지합니다. 프리카메라의 역방향 포즈도 허용합니다.
            if (depth <= .01f) strength = 1f;
            cameraPosition = position - forward * (depth * (1f / strength - 1f));
            cameraFov = 2f * Mathf.Atan(Mathf.Tan(Mathf.Clamp(fieldOfView, 10f, 100f) * Mathf.Deg2Rad * .5f) * strength) * Mathf.Rad2Deg;
        }

        public void Capture(Camera camera, Vector3 framingCenter)
        {
            float strength = Mathf.Clamp(perspectiveStrength, .2f, 1f);
            float depth = Vector3.Dot(framingCenter - camera.transform.position, camera.transform.forward);
            if (depth <= .01f) strength = 1f;
            // 보정된 실제 자세를 기준 자세로 되돌려 저장해 재적용 때 거리가 누적되지 않게 합니다.
            position = camera.transform.position + camera.transform.forward * (depth * (1f - strength));
            rotation = camera.transform.eulerAngles;
            fieldOfView = 2f * Mathf.Atan(Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / strength) * Mathf.Rad2Deg;
        }
    }
    [CreateAssetMenu(menuName = "JC/전투 테스트/카메라 프로필")]
    public sealed class JcBattleCameraProfile : ScriptableObject
    {
        [Tooltip("F6 구도입니다. 플레이 중에는 세션 초안을 편집한 뒤 명시적으로 저장하세요.")]
        public JcBattleCameraPose viewA = new JcBattleCameraPose { position = new Vector3(13.322f, 15.563f, -9.328f), rotation = new Vector3(45f, -55f, 0f) };
        [Tooltip("F7 구도입니다. F8 기존 뷰는 프로필에 저장하지 않습니다.")]
        public JcBattleCameraPose viewB = new JcBattleCameraPose { position = new Vector3(13.322f, 15.563f, 9.328f), rotation = new Vector3(45f, -125f, 0f) };
    }
}
