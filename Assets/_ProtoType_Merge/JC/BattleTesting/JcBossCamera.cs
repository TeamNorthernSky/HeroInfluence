using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-16 | 보스 기본뷰는 저장한 구도에 기준 배치 대비 이동량만 더합니다. 무대 경계 검사와 자동 회전/이동 제한은 폐기했습니다.
    public sealed class JcBossCamera : MonoBehaviour
    {
        public JcBossCameraProfile Source { get; private set; }
        public JcBossCameraProfile Draft { get; private set; }
        Camera lens;
        Vector3 center;
        JcBossLayoutController layout;
        BattlePresentationController presentation;
        bool refreshBase;
        int currentView;
        public int LayoutRevision { get; private set; }
        public bool CanCaptureBase => presentation == null || !presentation.IsAttackCameraActive;

        // JC | 2026-11-16 | 기준 배치 이후 캐릭터 이동량을 기본뷰에 한 번만 연결합니다. 오브젝트 전용 이동과 미적용 초안은 제외합니다.
        public Vector3 LayoutOffset => layout && Draft ? layout.AppliedCharacterOffset -
            new Vector3(Draft.layoutReference.x, 0, Draft.layoutReference.y) * layout.TileSize : Vector3.zero;

        public void Initialize(JcBossCameraProfile source, Camera camera, Vector3 framingCenter, JcBossLayoutController layoutController)
        {
            Source = source; Draft = Instantiate(source); Draft.hideFlags = HideFlags.DontSave;
            lens = camera; center = framingCenter; layout = layoutController;
            if (layout) layout.CharacterLayoutApplied += OnLayoutApplied;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                presentation = root.GetComponentInChildren<BattlePresentationController>();
                if (presentation) break;
            }
            if (presentation)
            {
                presentation.BossCamera = this;
                presentation.cameraProfile = Draft;
            }
            ApplyDraft();
        }

        // JC | 2026-11-16 | 원근 계산은 저장 기준에서 수행한 뒤 실제 월드 이동량만 더해 회전/FOV/원근을 유지합니다.
        public void ResolveView(int view, out Vector3 position, out Quaternion rotation, out float fov)
        {
            currentView = view == 0 ? 0 : 1;
            (currentView == 0 ? Draft.viewA : Draft.viewB).Resolve(center, out position, out rotation, out fov);
            position += LayoutOffset;
        }

        void OnLayoutApplied()
        {
            // JC | 2026-11-16 | 배치 적용 후 기본뷰를 먼저 갱신합니다. F6/F7의 이전 이동 목적지는 세대 번호로 무효화합니다.
            LayoutRevision++;
            if (!CanCaptureBase) { refreshBase = true; return; }
            ResolveView(currentView, out var position, out var rotation, out var fov);
            ApplyPose(position, rotation, fov);
        }

        public void ApplyDraft()
        {
            // JC | 2026-11-16 | 스킬 중 편집값은 복귀 후 반영해 추적/애니메이션의 카메라 소유권을 보존합니다.
            if (!CanCaptureBase) { refreshBase = true; return; }
            ResolveView(0, out var position, out var rotation, out var fov);
            ApplyPose(position, rotation, fov);
        }

        public void ApplyPose(Vector3 position, Quaternion rotation, float fov)
        {
            lens.transform.SetPositionAndRotation(position, rotation);
            lens.fieldOfView = fov;
        }

        // JC | 2026-11-16 | 스킬용 임시 각도/정본 변환은 롤백했습니다. 이 조절기는 저장 기본뷰와 배치 평행이동만 담당합니다.
        public void EndAttack()
        {
            if (refreshBase) { refreshBase = false; ApplyDraft(); }
        }
        public void CaptureBase()
        {
            // JC | 2026-11-16 | 이동된 화면을 캡처해도 기준 배치 좌표로 되돌려 저장하여 저장/재실행마다 이동량이 누적되지 않습니다.
            var offset = LayoutOffset;
            Draft.viewA.Capture(lens, center + offset);
            Draft.viewA.position -= offset;
        }
        void OnDestroy()
        {
            if (layout) layout.CharacterLayoutApplied -= OnLayoutApplied;
            Release(Draft);
        }
        static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
