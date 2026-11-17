using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-16 | 보스 기본뷰는 저장한 구도에 기준 배치 대비 이동량만 더합니다. 스킬은 일반 전투 경로를 사용하며 보스 전용 경계/앵글 제한을 적용하지 않습니다.
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
            // JC | 2026-11-17 | 본게임/테스트 공통 초기화에서 동일 초안에 연결된 조절 진입점을 만듭니다. 별도 카메라 루프는 없습니다.
            if (!GetComponentInChildren<JcBossFollowOffsetControls>(true))
            {
                var follow = new GameObject("JC_FollowOffset");
                follow.transform.SetParent(transform, false);
                follow.AddComponent<JcBossFollowOffsetControls>();
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
            if (!CanCaptureBase)
            {
                refreshBase = true; return;
            }
            ResolveView(0, out var position, out var rotation, out var fov);
            ApplyPose(position, rotation, fov);
        }

        public void ApplyPose(Vector3 position, Quaternion rotation, float fov)
        {
            // JC | 2026-11-16 | 기본뷰도 공통 최종 출력만 사용합니다. 보스 스킬 제한/보정은 없습니다.
            BattlePresentationController.WriteCameraPose(lens,position,rotation,fov);
        }

        // JC | 2026-11-16 | 공용 스킬 종료 후 보류한 기본뷰/배치 편집만 적용합니다.
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
