using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★빌보드-3D 시각 정합 수학의 단일 소스(260806).
    /// 빌보드와 3D 오브젝트(광선 등)의 불일치는 차수로 갈린다 —
    ///   0차(점): 접점 핀 고정으로 정확히 해소(<see cref="SolvePinnedCenter"/> — 근사가 아니라 항등)
    ///   1차(축): 롤 정렬(<see cref="Mode.CameraFacingUpAligned"/>) 또는 Y축 빌보드(<see cref="Mode.AxialY"/>)
    ///   2차(면·부피, 파랄락스): 트랜스폼 보정으로 원리적 불가 — 진짜 3D 기하로 풀 것(저스티스 회오리 교훈).
    /// 쿼드 관례: +Z가 카메라에서 멀어지는 방향 = 보이는 면이 카메라를 향한다(기존 PawSprite와 동일).
    /// </summary>
    public static class JcBillboardSolver
    {
        public enum Mode
        {
            [InspectorName("카메라 정면 (기본)")] CameraFacing = 0,
            [InspectorName("카메라 정면 + 월드수직 롤 정렬")] CameraFacingUpAligned = 1,
            [InspectorName("Y축 고정 (수직 유지)")] AxialY = 2,
        }

        /// <summary>롤 정렬 퇴화 한계(px²) — 월드 수직의 화면 투영이 이보다 짧으면(진탑뷰) 직전 회전을 유지한다.</summary>
        const float MinScreenUpSqr = 0.25f;

        /// <summary>모드별 빌보드 회전. 퇴화(진탑뷰 롤·카메라 수직선상 축 등)에서는 current를 유지한다.</summary>
        public static Quaternion SolveRotation(Camera cam, Vector3 anchorWorld, Mode mode, Quaternion current)
        {
            if (cam == null) return current;
            switch (mode)
            {
                case Mode.AxialY:
                {
                    Vector3 f = anchorWorld - cam.transform.position;
                    f.y = 0f;
                    return f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : current;
                }
                case Mode.CameraFacingUpAligned:
                {
                    // 해당 지점의 월드 수직선이 화면에 투영되는 방향으로 쿼드를 롤 —
                    // 스크린 투영 델타로 구하므로 원근 수렴(화면 가장자리 기울어짐)까지 정확하다.
                    Vector3 toAnchor = anchorWorld - cam.transform.position;
                    if (Vector3.Dot(cam.transform.forward, toAnchor) <= 1e-3f)
                        return cam.transform.rotation;   // 카메라 뒤/시평면상 — 정면 폴백
                    Vector3 s0 = cam.WorldToScreenPoint(anchorWorld);
                    Vector3 s1 = cam.WorldToScreenPoint(anchorWorld + Vector3.up * 0.5f);
                    Vector2 d = new Vector2(s1.x - s0.x, s1.y - s0.y);
                    if (d.sqrMagnitude < MinScreenUpSqr) return current;   // 진탑뷰 퇴화 — 직전 유지
                    Vector3 up = (cam.transform.right * d.x + cam.transform.up * d.y).normalized;
                    return Quaternion.LookRotation(cam.transform.forward, up);
                }
                default:
                    return cam.transform.rotation;
            }
        }

        /// <summary>
        /// ★접점 핀 고정 — 쿼드 로컬 접점(±0.5 기준)이 anchorWorld에 정확히 놓이도록 쿼드 중심을 되민다.
        /// 그 로컬 점이 그 월드 점 자체가 되므로, 어떤 카메라 각도·원근에서도 투영이 겹친다.
        /// </summary>
        public static Vector3 SolvePinnedCenter(Vector3 anchorWorld, Quaternion rotation, Vector2 anchorLocal, float scale)
            => anchorWorld - rotation * (Vector3)(anchorLocal * scale);
    }
}
