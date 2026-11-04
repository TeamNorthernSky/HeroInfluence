using UnityEngine;

namespace JC.BattleTesting
{
    public sealed class JcBossCamera : MonoBehaviour
    {
        public JcBossCameraProfile Source { get; private set; }
        public JcBossCameraProfile Draft { get; private set; }
        Camera lens;
        Vector3 center;
        Transform actor, target;
        BattlePresentationController presentation;
        bool refreshBase;
        JcBossStageBounds stageBounds;

        Pose accepted;
        float acceptedFov;
        bool hasPose;
        public bool AtBoundary { get; private set; }
        public bool BoundaryChangePending { get; private set; }
        float acceptedRight;

        public bool IsAimShot => actor && target;
        public bool CanCaptureBase => presentation == null || !presentation.IsAttackCameraActive;

        public void Initialize(JcBossCameraProfile source, Camera camera, Vector3 framingCenter, Transform stage)
        {
            Source = source; Draft = Instantiate(source); Draft.hideFlags = HideFlags.DontSave;
            lens = camera; center = framingCenter;
            stageBounds = new JcBossStageBounds(stage);
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

        public void ApplyDraft()
        {

            if (!IsAimShot && !CanCaptureBase) { refreshBase = true; return; }
            if (IsAimShot)
            {
                refreshBase = true;
                ResolveAim(out var pos,out var rot,out var fov);
                ApplyPose(pos,rot,fov);
            }
            else
            {
                Draft.viewA.Resolve(center,out var pos,out var rot,out var fov);
                ApplyPose(pos,rot,fov);
            }
        }

        public bool IsBasePreview { get; private set; }

#if UNITY_EDITOR
        public bool PreviewOutsideStage => IsBasePreview && !stageBounds.Covers(
            new Pose(lens.transform.position,lens.transform.rotation),lens.fieldOfView,
            lens.aspect,lens.nearClipPlane,Mathf.Clamp(Draft.rightWallX+Draft.rightMargin,3f,49f));

        public void PreviewEditorDraft()
        {
            if (!CanCaptureBase || IsAimShot) { ApplyDraft(); return; }
            Draft.viewA.Resolve(center,out var position,out var rotation,out var fov);
            IsBasePreview = true;
            lens.transform.SetPositionAndRotation(position,rotation);
            lens.fieldOfView = fov;
        }
#endif

        public void EndBasePreview()
        {
            if (!IsBasePreview) return;
            IsBasePreview = false;
            float right = Mathf.Clamp(Draft.rightWallX+Draft.rightMargin,3f,49f);
            // Resume from the last valid game pose, never from the unconstrained editor lens.
            hasPose = hasPose && stageBounds.Allows(accepted,acceptedFov,lens.aspect,lens.nearClipPlane,right);
            ApplyDraft();
        }

        // The single writer for authored boss camera movement. Idle frames leave the lens alone.
        public void ApplyPose(Vector3 position, Quaternion rotation, float fov)
        {
            if (IsBasePreview) return;
            var requested = new Pose(position,rotation);
            float right = Mathf.Clamp(Draft.rightWallX+Draft.rightMargin,3f,49f);
            if (!hasPose)
            {
                accepted = stageBounds.EntryPose(requested,fov,lens.aspect,lens.nearClipPlane,right,center,out acceptedFov);
                acceptedRight = right;
                hasPose = true;
                AtBoundary = Quaternion.Angle(accepted.rotation,rotation)>.01f;
            }
            else
            {
                // A tightened wall becomes active as soon as the current shot is inside it.
                // While pending, moves still obey the last accepted limit rather than cutting to another angle.
                if (right >= acceptedRight || stageBounds.Allows(accepted,acceptedFov,lens.aspect,lens.nearClipPlane,right))
                    acceptedRight = right;
                float t = stageBounds.AllowedFraction(accepted,acceptedFov,requested,fov,lens.aspect,lens.nearClipPlane,acceptedRight);
                accepted = JcBossStageBounds.Interpolate(accepted,requested,t);
                acceptedFov = Mathf.Lerp(acceptedFov,fov,t);
                AtBoundary = t<1f;
                if (right<acceptedRight && stageBounds.Allows(accepted,acceptedFov,lens.aspect,lens.nearClipPlane,right))
                    acceptedRight = right;
            }
            BoundaryChangePending = right<acceptedRight;
            lens.transform.SetPositionAndRotation(accepted.position,accepted.rotation);
            lens.fieldOfView = acceptedFov;
        }

        public void BeginAim(Transform caster, Transform victim)
        {
            actor = caster; target = victim;
        }

        public void ResolveAim(out Vector3 pos,out Quaternion rot,out float fov)
        {
            Draft.aim.Resolve(center,actor.position,target.position,lens.aspect,out pos,out rot,out fov);
            // Choose the attack endpoint before the transition starts. The writer never substitutes another shot.
            rot = stageBounds.PlanRotation(accepted,acceptedFov,new Pose(pos,rot),fov,lens.aspect,lens.nearClipPlane,acceptedRight);
        }

        public void EndAttack()
        {
            actor = null; target = null;
            if (refreshBase) { refreshBase = false; ApplyDraft(); }
        }

        public void CaptureBase() => Draft.viewA.Capture(lens,center);

        public void CaptureAim()
        {
            if (!IsAimShot) return;
            Draft.aim.pose.Capture(lens,center);
            Draft.aim.actorAnchor = lens.WorldToViewportPoint(actor.position);
            Draft.aim.targetAnchor = lens.WorldToViewportPoint(target.position+Vector3.up*Draft.aim.targetHeight);
        }

        void OnDestroy() { if (Draft) Destroy(Draft); }
    }
}
