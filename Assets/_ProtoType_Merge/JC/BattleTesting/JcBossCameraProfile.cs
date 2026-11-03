using System;
using UnityEngine;

namespace JC.BattleTesting
{
    public sealed class JcBossCameraProfile : JcBattleCameraProfile
    {
        [InspectorName("블랙 불릿 · 공통 시전 구도")] public AimShot aim = new AimShot();

        [InspectorName("오른쪽 가상 벽 X (무대 로컬)"), Range(3f,49f)] public float rightWallX = 19f;
        [InspectorName("오른쪽 연출 허용 폭"), Range(0f,12f)] public float rightMargin = 3f;

        [Serializable]
        public sealed class AimShot
        {
            [InspectorName("기준 카메라")] public JcBattleCameraPose pose = new JcBattleCameraPose {
                position = new Vector3(7.81f, 9.51f, -19.04083f), rotation = new Vector3(23.8f, -24f, 0), fieldOfView = 35f };
            [InspectorName("시전자 발 화면 위치")] public Vector2 actorAnchor = new Vector2(.54f, .15f);
            [InspectorName("발 위치 허용 오차")] public Vector2 actorTolerance = new Vector2(.03f, .03f);
            [InspectorName("표적 화면 중심")] public Vector2 targetAnchor = new Vector2(.555f, .68f);
            [InspectorName("표적 위치 허용 오차")] public Vector2 targetTolerance = new Vector2(.125f, .07f);
            [InspectorName("표적 기준 높이"), Min(0)] public float targetHeight = 1.1f;
            [InspectorName("최대 화각"), Range(10,100)] public float maxFov = 38f;

            // Positive viewport error means the subject needs to move towards the right/top of the picture.
            static Vector2 Error(Vector2 point, Vector2 anchor, Vector2 tolerance)
            {
                var lo = Vector2.Max(Vector2.zero, anchor - Vector2.Max(Vector2.zero,tolerance));
                var hi = Vector2.Min(Vector2.one, anchor + Vector2.Max(Vector2.zero,tolerance));
                return new Vector2(Mathf.Clamp(point.x,lo.x,hi.x), Mathf.Clamp(point.y,lo.y,hi.y)) - point;
            }

            static Vector2 Project(Vector3 local, Vector2 span)
            {
                float z = Mathf.Max(.01f,local.z);
                return new Vector2(.5f + local.x/(z*span.x), .5f + local.y/(z*span.y));
            }

            public void Resolve(Vector3 center, Vector3 actor, Vector3 target, float aspect,
                out Vector3 position, out Quaternion rotation, out float fov)
            {
                pose.Resolve(center,out var seed,out var orientation,out var initialFov);
                target += Vector3.up * targetHeight;
                position = seed; rotation = orientation; fov = initialFov;
                float limit = Mathf.Max(initialFov, maxFov);
                for (;;)
                {
                    float height = 2f * Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
                    var span = new Vector2(height * aspect,height);
                    position = seed; rotation = orientation;
                    for (int i=0;i<10;i++)
                    {
                        var inv = Quaternion.Inverse(rotation);
                        var near = inv * (actor-position);
                        var correction = Error(Project(near,span),actorAnchor,actorTolerance);
                        position -= rotation * new Vector3(correction.x*span.x,correction.y*span.y,0) * near.z;
                        var far = inv * (target-position);
                        var error = Error(Project(far,span),targetAnchor,targetTolerance);
                        if (error.sqrMagnitude < .000004f) return;
                        // Turning while re-anchoring the foreground has less leverage on targets at similar depths.
                        var turn = rotation * Quaternion.Euler(error.y*span.y*Mathf.Rad2Deg,
                            -error.x*span.x*Mathf.Rad2Deg,0);
                        if (i<9) rotation = Quaternion.RotateTowards(orientation,
                            Quaternion.Euler(turn.eulerAngles.x,turn.eulerAngles.y,0),10f);
                    }
                    if (fov >= limit) return;
                    fov = Mathf.Min(limit, fov+1f);
                }
            }
        }
    }
}
