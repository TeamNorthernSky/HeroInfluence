using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-10 | 기준 구도와 배치별 회전·높이 목표를 투영 제약 안에서 함께 구합니다. 결과 위치를 사후 클리핑하지 않습니다.
    // JC | 2026-11-16 | 전장별 풀이를 제거하고 기존 일반 전투 계산을 단일 정본으로 사용합니다. 기준 좌표 변환은 호출부에서 수행합니다.
    public static class JcBlackBulletCameraFraming
    {
        // JC | 2026-11-10 | 빈 셀도 포함한 같은 전후열의 양 끝을 기준으로 중앙열을 보간합니다. 실재 유닛/셀은 이동하지 않습니다.
        public struct ColumnReference
        {
            public Vector3 target, oppositeTarget, position;
            public Quaternion rotation;
            public float fov;
            public Vector3[] points;
        }
        // JC | 2026-11-10 | 배치별 목표와 이동·회전·표시 제약을 한 계산에서 평가합니다. 완성된 카메라를 사후 클리핑하지 않습니다.
        public static bool TryResolve(Vector3 actor, Vector3 target, Vector3[] points,
            Vector3 initialPosition, Quaternion rotation, float initialFov, float aspect, float nearClip,
            Vector2 pixels, JcBlackBulletCameraProfile settings, out Vector3 position, out Quaternion finalRotation, out float fov,
            float groundHeight = 0, float columnBlend = 0, ColumnReference? columnReference = null,
            float? basisPitchOffset = null, float bossTargetColumn = 1f)
        {
            // JC | 2026-11-16 | 보스는 수평 전장 좌표에서 열별 구도를 탐색 목표로 사용합니다.
            // 완성된 구도에 고정 회전을 덮어쓰고 FOV만 넓히지 않도록 높이/열/표시 보호를 한 번에 풉니다.
            // 값이 없는 기존 일반전·은행 경로는 아래 계산을 그대로 유지합니다.
            if (basisPitchOffset.HasValue)
            {
                if (!TryResolveBaseline(actor, target, points, initialPosition, rotation, initialFov, aspect, nearClip, pixels,
                    settings, out position, out finalRotation, out fov, groundHeight,
                    basisPitchOffset, columnBlend, columnReference)) return false;
                if (!ApplyPerspective((actor + target) * .5f, points, settings.perspectiveStrength, aspect, nearClip,
                    pixels, settings, ref position, finalRotation, ref fov)) return false;
                // JC | 2026-11-16 | 추가 검수에 따라 보스 BB는 후퇴나 추가 회전 없이 높이를 1.4 낮추고 투영 폭을 11% 줄입니다.
                // 넓은 화각은 35도까지 좁혀 전열/후열 모두 벽 상단 노출을 줄이되 인물·그리드·숫자
                // 보호에 필요한 화각은 우선합니다. 목적지만 계산하므로 기존 진입/복귀 보간을 그대로 탑니다.
                position.y -= 1.4f;
                float bossRequired = RequiredFov(points, position, finalRotation, aspect, pixels, settings, out bool bossFeasible, nearClip);
                if (!bossFeasible) return false;
                float tighterFov = 2f * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad * .5f) * .89f) * Mathf.Rad2Deg;
                fov = Mathf.Max(Mathf.Min(tighterFov, 35f), bossRequired);
                if (!AlignBossPair(actor, target, points, aspect, nearClip, pixels, settings, finalRotation, fov, ref position)) return false;
                // JC | 2026-11-17 | 보스의 좌/중/우 목표와 가시 제약을 목적지 계산 안에서 함께 풉니다.
                // 런타임 Transform 후처리 없이 기존 단일 진입/공격/복귀 경로에 최종 구도만 전달합니다.
                ResolveBossComposition(actor, target, points, aspect, nearClip, pixels, settings, bossTargetColumn,
                    ref position, ref finalRotation, ref fov);
                return true;
            }
            // JC | 2026-11-10 | 기존 구도와 사용자 지정 구도를 열 가중치로 직접 보간합니다. 위치/회전을 결과 후 클리핑하지 않습니다.
            // 기준 높이의 90→80%와 X 기울기를 거리로 연결하고, 같은 자세에서 필요한 화각만 가시 부등식으로 구합니다.
            if (!TryResolveBaseline(actor, target, points, initialPosition, rotation, initialFov, aspect, nearClip, pixels,
                settings, out position, out finalRotation, out fov, groundHeight)) return false;
            float weight = Mathf.Clamp01(columnBlend);
            var focus = (actor + target) * .5f;
            float oldHeight = position.y;
            var referenceFocus = focus;
            // JC | 2026-11-10 | 중앙 타깃 자체의 기존 구도를 섞으면 회전이 두 끝점 사이를 벗어납니다. 유지할 끝점의 구도를 공통으로 풉니다.
            if (weight > 0 && columnReference.HasValue)
            {
                var endpoint = columnReference.Value;
                if (!TryResolveBaseline(actor, endpoint.target, endpoint.points, endpoint.position, endpoint.rotation, endpoint.fov,
                    aspect, nearClip, pixels, settings, out position, out finalRotation, out _, groundHeight)) return false;
                referenceFocus = (actor + endpoint.oppositeTarget) * .5f;
            }
            var reference = new Vector3(settings.crossColumnPosition.x + referenceFocus.x - settings.crossColumnCenter.x,
                oldHeight, settings.crossColumnPosition.y + referenceFocus.z - settings.crossColumnCenter.y);
            var angles = finalRotation.eulerAngles;
            float pitch = Mathf.LerpAngle(angles.x, settings.crossColumnAngles.x, weight);
            float yaw = Mathf.LerpAngle(angles.y, settings.crossColumnAngles.y, weight);
            position = Vector3.Lerp(position, reference, weight);
            oldHeight = position.y;
            float farRatio = Mathf.Clamp(settings.rightLayoutHeightRatio, .6f, 1f);
            float distanceBlend = Mathf.InverseLerp(settings.heightDistanceRange.x,
                Mathf.Max(settings.heightDistanceRange.x + .01f, settings.heightDistanceRange.y), Vector3.Distance(actor, target));
            float ratio = Mathf.Lerp(Mathf.Clamp(settings.nearHeightRatio, farRatio, 1f), farRatio,
                Mathf.SmoothStep(0, 1, distanceBlend));
            position.y = groundHeight + (oldHeight - groundHeight) / farRatio * ratio;
            // JC | 2026-11-10 | 높이 변화 전후에 같은 인물 중점을 보는 고도각 차이만 더해 가까운 대상의 과도한 하강을 막습니다.
            float horizontal = Mathf.Max(.01f, Vector2.Distance(new Vector2(position.x, position.z), new Vector2(focus.x, focus.z)));
            pitch += (Mathf.Atan2(position.y - focus.y, horizontal) - Mathf.Atan2(oldHeight - focus.y, horizontal)) * Mathf.Rad2Deg;
            finalRotation = Quaternion.Euler(pitch, yaw, 0);
            float required = RequiredFov(points, position, finalRotation, aspect, pixels, settings, out bool feasible, nearClip);
            fov = Mathf.Max(fov, required);
            if (!feasible) return false;
            // JC | 2026-11-11 | 열/거리 구도는 배율1의 기준으로 보존하며 원근 조절은 항상 그 기준에서 계산해 누적 이동을 막습니다.
            return ApplyPerspective(focus, points, settings.perspectiveStrength, aspect, nearClip, pixels, settings,
                ref position, finalRotation, ref fov);
        }

        // JC | 2026-11-17 | 좌/중/우 세 촬영 기준을 C1 연속 보간합니다. 우측 기본값은 정확한 항등 변환입니다.
        public static Vector3 BossComposition(JcBlackBulletCameraProfile settings, float targetColumn)
        {
            float t = Mathf.Clamp01(targetColumn) * 2f;
            return t <= 1f
                ? Vector3.Lerp(settings.bossLeftComposition, settings.bossCenterComposition, Mathf.SmoothStep(0, 1, t))
                : Vector3.Lerp(settings.bossCenterComposition, settings.bossRightComposition, Mathf.SmoothStep(0, 1, t - 1f));
        }

        // JC | 2026-11-17 | 수동 FOV를 그대로 덮어쓰면 미래 숫자 보호가 다시 줌아웃시킵니다.
        // 같은 보호점 전체를 사용해 전후/좌우 위치와 작은 상하각을 함께 맞추며 높이와 천장 방향은 보존합니다.
        static void ResolveBossComposition(Vector3 actor, Vector3 target, Vector3[] points, float aspect, float nearClip,
            Vector2 pixels, JcBlackBulletCameraProfile settings, float targetColumn,
            ref Vector3 position, ref Quaternion rotation, ref float fov)
        {
            var adjustment = BossComposition(settings, targetColumn);
            if ((adjustment - new Vector3(0, 1, 0)).sqrMagnitude < .00000001f) return;
            var originalPosition = position; float originalFov = fov;
            var preferred = position + Vector3.forward * adjustment.x;
            var angles = rotation.eulerAngles;
            float pitch = Mathf.DeltaAngle(0, angles.x), yaw = angles.y + adjustment.z;
            float preferredT = Mathf.Tan(fov * Mathf.Deg2Rad * .5f) * Mathf.Clamp(adjustment.y, .5f, 1.5f);
            float preferredFov = Mathf.Atan(preferredT) * 2f * Mathf.Rad2Deg;
            // 기본 구도보다 위를 보지 않으므로 좁힌 FOV를 상하각 보정이 상쇄해 천장 밖을 드러내지 않습니다.
            float topRay = pitch - originalFov * .5f;
            float strength = Mathf.Clamp01(Mathf.Abs(adjustment.x) + Mathf.Abs(adjustment.y - 1) * 8 + Mathf.Abs(adjustment.z));
            float travel = Mathf.Min(2f, Mathf.Max(0, settings.adaptiveTravel)) * strength;
            float pitchTravel = 2.5f * strength;
            Vector3 bestPosition = position; Quaternion bestRotation = rotation; float bestFov = fov;
            float Cost(Vector3 p, float candidatePitch, float candidateFov)
            {
                float zoomError = Mathf.Tan(candidateFov * Mathf.Deg2Rad * .5f) / preferredT - 1;
                return 100f * zoomError * zoomError + .05f * (p - preferred).sqrMagnitude +
                    .03f * (candidatePitch - pitch) * (candidatePitch - pitch);
            }
            // 기존의 유효 구도도 후보에 포함하므로 어떤 배치에서도 표시 보호를 깨뜨린 결과를 채택하지 않습니다.
            float best = Cost(position, pitch, fov) + .03f * adjustment.z * adjustment.z;
            float Evaluate(Vector3 v)
            {
                var p = preferred + new Vector3(v.x, 0, v.y);
                if (Mathf.Abs(v.x) > travel || Mathf.Abs(v.y) > travel || Mathf.Abs(v.z) > pitchTravel) return float.PositiveInfinity;
                // 전진 요청에서 기존 구도보다 후퇴하는 해는 선택하지 않습니다.
                if (adjustment.x >= 0 && p.z < originalPosition.z - .00001f) return float.PositiveInfinity;
                float candidatePitch = pitch + v.z;
                var q = Quaternion.Euler(candidatePitch, yaw, 0);
                float required = RequiredFov(points, p, q, aspect, pixels, settings, out bool feasible, nearClip);
                float candidateFov = Mathf.Max(preferredFov, required + .001f);
                if (!feasible || candidateFov > Mathf.Clamp(settings.maximumFov, 30, 120) ||
                    candidatePitch - candidateFov * .5f < topRay - .0001f) return float.PositiveInfinity;
                float cost = Cost(p, candidatePitch, candidateFov);
                if (cost < best) { best = cost; bestPosition = p; bestRotation = q; bestFov = candidateFov; }
                return cost;
            }
            var seeds = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, Vector3>>();
            foreach (float x in new[] { -1f, 0f, 1f })
                foreach (float z in new[] { -1f, 0f, 1f })
                    // JC | 2026-11-17 | 낮은 기울기의 가용 구간이 좁아 끝값만 검사하면 중앙 구도가 누락됩니다.
                    foreach (float tilt in new[] { -1f, -.5f, 0f, .5f, 1f })
                    {
                        var v = new Vector3(x * travel, z * travel, tilt * pitchTravel);
                        float cost = Evaluate(v);
                        if (!float.IsPositiveInfinity(cost)) seeds.Add(new System.Collections.Generic.KeyValuePair<float, Vector3>(cost, v));
                    }
            seeds.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < Mathf.Min(3, seeds.Count); i++)
            {
                var v = seeds[i].Value; float score = seeds[i].Key;
                var steps = new Vector3(travel * .5f, travel * .5f, pitchTravel * .5f);
                for (int level = 0; level < 8; level++)
                {
                    for (int sweep = 0; sweep < 3; sweep++)
                    {
                        bool improved = false;
                        for (int axis = 0; axis < 3; axis++)
                        {
                            float value = v[axis], chosen = value;
                            foreach (float sign in new[] { -1f, 1f })
                            {
                                v[axis] = value + sign * steps[axis]; float cost = Evaluate(v);
                                if (cost < score) { score = cost; chosen = v[axis]; improved = true; }
                            }
                            v[axis] = chosen;
                        }
                        if (!improved) break;
                    }
                    steps *= .5f;
                }
            }
            position = bestPosition; rotation = bestRotation; fov = bestFov;
        }

        // JC | 2026-11-16 | 사용자 추가 여백 검수에 따라 보스 BB 중심을 58%에서 62%로 옮깁니다.
        // 두 인물의 실제 깊이에서 필요한 카메라 좌우 이동을 계산하므로 특정 행·열/월드 X에 종속되지 않습니다.
        // 회전·화각·세로 구도는 유지하고 모든 표시 보호점의 허용 이동 구간 안에서만 움직입니다.
        // 런타임 카메라를 사후 이동하지 않고 기존 전환에 전달할 목적지 계산 안에서 한 번 적용합니다.
        static bool AlignBossPair(Vector3 actor, Vector3 target, Vector3[] points, float aspect, float nearClip,
            Vector2 pixels, JcBlackBulletCameraProfile settings, Quaternion rotation, float fov, ref Vector3 position)
        {
            const float centerX = .62f;
            var inverse = Quaternion.Inverse(rotation);
            var a = inverse * (actor - position);
            var b = inverse * (target - position);
            if (a.z <= nearClip || b.z <= nearClip) return false;
            float tangent = Mathf.Tan(fov * Mathf.Deg2Rad * .5f) * Mathf.Max(.01f, aspect);
            float desired = ((centerX - .5f) * 4f * tangent - a.x / a.z - b.x / b.z) / (1f / a.z + 1f / b.z);
            var limits = ScreenLimits(pixels, settings);
            float lower = float.NegativeInfinity, upper = float.PositiveInfinity;
            foreach (var point in points)
            {
                var p = inverse * (point - position);
                if (p.z <= nearClip) return false;
                lower = Mathf.Max(lower, limits.x * tangent * p.z - p.x);
                upper = Mathf.Min(upper, limits.y * tangent * p.z - p.x);
            }
            if (lower > upper) return false;
            position -= rotation * Vector3.right * Mathf.Clamp(desired, lower, upper);
            return true;
        }

        // JC | 2026-11-11 | 공용 카메라 포즈의 원근감 이동을 재사용합니다. 기준면 크기를 보존하되 최종 숫자/그리드 여백은 다시 확인합니다.
        public static bool ApplyPerspective(Vector3 focus, Vector3[] points, float strength, float aspect, float nearClip,
            Vector2 pixels, JcBlackBulletCameraProfile settings, ref Vector3 position, Quaternion rotation, ref float fov)
        {
            strength = Mathf.Clamp(strength, .2f, 1f);
            if (strength >= 1f) return true;
            var pose = new JcBattleCameraPose { position = position, rotation = rotation.eulerAngles,
                fieldOfView = fov, perspectiveStrength = strength };
            pose.Resolve(focus, out position, out _, out _);
            // JC | 2026-11-11 | 공용 포즈의 기준 FOV 입력 범위(10~100)로 BB의 보호 화각을 자르지 않고 실제 기준 화각에 동일 배율을 적용합니다.
            fov = 2f * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad * .5f) * strength) * Mathf.Rad2Deg;
            float required = RequiredFov(points, position, rotation, aspect, pixels, settings, out bool feasible, nearClip);
            fov = Mathf.Max(fov, required);
            return feasible;
        }

        // JC | 2026-11-10 | 적 우측열(플레이어 화면 좌측)의 승인된 기존 목적지를 그대로 계산하는 보간 기준입니다.
        static bool TryResolveBaseline(Vector3 actor, Vector3 target, Vector3[] points,
            Vector3 initialPosition, Quaternion rotation, float initialFov, float aspect, float nearClip,
            Vector2 pixels, JcBlackBulletCameraProfile settings, out Vector3 position, out Quaternion finalRotation, out float fov,
            float groundHeight, float? basisPitchOffset = null, float columnBlend = 0, ColumnReference? columnReference = null)
        {
            if (!ResolveFixed(actor, target, points, initialPosition, rotation, initialFov, aspect, nearClip, pixels,
                settings, out position, out finalRotation, out fov)) return false;
            // JC | 2026-11-10 | 사용자 3번 이미지의 시선을 기준으로 두 인물을 한 묶음으로 배치합니다.
            // 메뉴로부터의 고정 픽셀 간격과 배치별 가중치를 없애 중앙/후열도 같은 화면 비율 목표를 사용합니다. 원근감 배율은 적용하지 않습니다.
            Vector3 basePosition = position;
            Quaternion baseRotation = Quaternion.Euler(settings.referenceAngles.x, settings.referenceAngles.y, 0);
            var goalCenter = new Vector2(settings.pairCenterX * 2 - 1, -.06f);
            var goalSeparation = new Vector2(settings.pairHorizontalGap * 2, .84f);
            float height = groundHeight + (basePosition.y - groundHeight) * settings.rightLayoutHeightRatio;
            // JC | 2026-11-16 | 기존 공통 탐색기의 촬영 목표만 보스 기본뷰와 배치에 맞춥니다.
            // 지면은 수평으로 보존하고 카메라 기울기 차이는 촬영 방향에만 적용합니다.
            if (basisPitchOffset.HasValue)
            {
                float weight = Mathf.Clamp01(columnBlend);
                var focus = (actor + target) * .5f;
                var referenceFocus = columnReference.HasValue ? (actor + columnReference.Value.oppositeTarget) * .5f : focus;
                var columnPosition = new Vector3(settings.crossColumnPosition.x + referenceFocus.x - settings.crossColumnCenter.x,
                    basePosition.y, settings.crossColumnPosition.y + referenceFocus.z - settings.crossColumnCenter.y);
                basePosition = Vector3.Lerp(basePosition, columnPosition, weight);
                float far = Mathf.Clamp(settings.rightLayoutHeightRatio, .6f, 1f);
                float distance = Mathf.InverseLerp(settings.heightDistanceRange.x,
                    Mathf.Max(settings.heightDistanceRange.x + .01f, settings.heightDistanceRange.y), Vector3.Distance(actor, target));
                float ratio = Mathf.Lerp(Mathf.Clamp(settings.nearHeightRatio, far, 1f), far, Mathf.SmoothStep(0, 1, distance));
                height = groundHeight + (basePosition.y - groundHeight) * ratio;
                baseRotation = Quaternion.Euler(Mathf.LerpAngle(settings.referenceAngles.x, settings.crossColumnAngles.x, weight)
                    + basisPitchOffset.Value, Mathf.LerpAngle(settings.referenceAngles.y, settings.crossColumnAngles.y, weight), 0);
            }
            Vector3 right = Vector3.ProjectOnPlane(baseRotation * Vector3.right, Vector3.up).normalized;
            Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
            var angles = baseRotation.eulerAngles;
            float travel = Mathf.Max(0, settings.adaptiveTravel);
            var bound = new[] { travel, travel * .6f, Mathf.Max(0, settings.adaptiveAngles.x), Mathf.Max(0, settings.adaptiveAngles.y) };
            // JC | 2026-11-16 | 보스의 열별 촬영 방향은 확정값입니다. 화면 간격을 맞추려고
            // 회전 허용치를 끝까지 사용해 정면 구도로 바뀌지 않도록 위치와 화각만 탐색합니다.
            // 일반전·은행의 기존 회전 탐색 범위는 유지합니다.
            if (basisPitchOffset.HasValue) { bound[2] = 0; bound[3] = 0; }
            // JC | 2026-11-16 | 방향 고정 시 두 인물을 세로로 겹치려는 가로 간격 목표는
            // 과도한 축소를 유도하므로 사용하지 않습니다. 중앙·세로 크기와 표시 보호는 유지합니다.
            float horizontalSeparationWeight = basisPitchOffset.HasValue ? 0f : 8f;
            float best = float.PositiveInfinity;
            Vector3 bestPosition = position; Quaternion bestRotation = finalRotation; float bestFov = fov;
            float Evaluate(float[] v)
            {
                // JC | 2026-11-10 | 허용 영역 밖의 후보를 거부하고 roll은 0으로 유지합니다. 표적이 약간 왼쪽 위인 기준 구도도 허용합니다.
                if (v[0] * v[0] + v[1] * v[1] > travel * travel + .0001f) return float.PositiveInfinity;
                float pitch = angles.x + v[2];
                if (pitch < 16 || pitch > 55) return float.PositiveInfinity;
                var rot = Quaternion.Euler(pitch, angles.y + v[3], 0);
                var pos = basePosition + right * v[0] + forward * v[1]; pos.y = height;
                var inv = Quaternion.Inverse(rot);
                var a = inv * (actor - pos); var b = inv * (target - pos);
                if (a.z <= nearClip || b.z <= nearClip) return float.PositiveInfinity;
                var ap = new Vector2(a.x / (a.z * aspect), a.y / a.z);
                var bp = new Vector2(b.x / (b.z * aspect), b.y / b.z);
                // JC | 2026-11-10 | 가로·세로 비례 확대는 방향을 바꾸지 않으므로 비싼 보호점 순회 전에 사선을 검사합니다.
                float diagonal = Mathf.Atan2(bp.y - ap.y, bp.x - ap.x) * Mathf.Rad2Deg;
                // JC | 2026-11-16 | 보스는 촬영 방향을 고정하므로 교차 배치의 인물 사선 각도까지
                // 강제하면 정상 구도가 전부 탈락할 수 있습니다. 각도 미관 제한만 제외하며 표시 보호는 아래에서 검사합니다.
                if (!basisPitchOffset.HasValue && (diagonal < 25 || diagonal > 115)) return float.PositiveInfinity;
                float required = RequiredFov(points, pos, rot, aspect, pixels, settings, out bool possible, nearClip);
                if (!possible) return float.PositiveInfinity;
                // 고정 후보 자세에서 화면 목표의 최소제곱 확대율을 구하고 같은 가시 제약의 구간에 제한합니다.
                // JC | 2026-11-10 | 중앙 정렬과 두 인물 간격을 함께 최소화합니다. 가로 간격을 맞추려고 둘 다 오른쪽으로 밀리는 해를 방지합니다.
                var center = (ap + bp) * .5f;
                var separation = bp - ap;
                float numerator = 32 * center.x * goalCenter.x + horizontalSeparationWeight * separation.x * goalSeparation.x +
                    4 * center.y * goalCenter.y + 16 * separation.y * goalSeparation.y;
                float denominator = 32 * center.x * center.x + horizontalSeparationWeight * separation.x * separation.x +
                    4 * center.y * center.y + 16 * separation.y * separation.y;
                float minT = Mathf.Tan(required * Mathf.Deg2Rad * .5f);
                // JC | 2026-11-10 | 가로 간격만 줄이려고 두 인물을 과도하게 축소하지 않습니다. 기존 기준 구도와 같이 보호 최소 화각의 15% 여유까지만 탐색합니다.
                float maxT = Mathf.Min(Mathf.Tan(Mathf.Clamp(settings.maximumFov, 30, 120) * Mathf.Deg2Rad * .5f), minT * 1.15f);
                float t = Mathf.Clamp(numerator > .00001f ? denominator / numerator : maxT, minT, maxT);
                var ec = center / t - goalCenter; var es = separation / t - goalSeparation;
                float cost = 32 * ec.x * ec.x + horizontalSeparationWeight * es.x * es.x + 4 * ec.y * ec.y + 16 * es.y * es.y;
                // JC | 2026-11-10 | 안전 영역을 확보하는 범위에서 기준 시선에 가까운 후보를 선호해 과한 좌우 회전을 줄입니다.
                cost += .001f * (v[0]*v[0] + v[1]*v[1]) + .00015f * (v[2]*v[2] + v[3]*v[3]);
                if (cost < best) { best = cost; bestPosition = pos; bestRotation = rot; bestFov = Mathf.Atan(t) * 2 * Mathf.Rad2Deg; }
                return cost;
            }
            // JC | 2026-11-10 | 전열 교차 배치는 원점 주변이 모두 사선 제약 밖일 수 있습니다.
            // 허용 원판의 중앙·경계에서 가시 조건을 만족하는 후보를 먼저 찾고 상위 세 곳을 정밀 탐색합니다.
            var seeds = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, float[]>>();
            var origins = new[] { Vector2.zero, Vector2.right, Vector2.left, new Vector2(0, .6f), new Vector2(0, -.6f),
                new Vector2(.8f, .6f), new Vector2(.8f, -.6f), new Vector2(-.8f, .6f), new Vector2(-.8f, -.6f) };
            foreach (var origin in origins)
                foreach (float pitchSeed in new[] { -1f, 0f, 1f })
                    foreach (float yawSeed in new[] { -1f, 0f, 1f })
                    {
                        var seed = new[] { origin.x * travel, origin.y * travel, bound[2] * pitchSeed, bound[3] * yawSeed };
                        float cost = Evaluate(seed);
                        if (!float.IsPositiveInfinity(cost)) seeds.Add(new System.Collections.Generic.KeyValuePair<float, float[]>(cost, seed));
                    }
            seeds.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int start = 0; start < Mathf.Min(3, seeds.Count); start++)
            {
                var v = seeds[start].Value;
                float score = seeds[start].Key;
                var steps = new[] { bound[0] * .5f, bound[1] * .5f, bound[2] * .5f, bound[3] * .5f };
                for (int level = 0; level < 7; level++)
                {
                    for (int sweep = 0; sweep < 4; sweep++)
                    {
                        bool improved = false;
                        for (int axis = 0; axis < 4; axis++)
                        {
                            float original = v[axis], chosen = original;
                            foreach (float sign in new[] { -1f, 1f })
                            {
                                float candidate = original + sign * steps[axis];
                                if (Mathf.Abs(candidate) > bound[axis] + .00001f) continue;
                                v[axis] = candidate; float cost = Evaluate(v);
                                if (cost < score) { score = cost; chosen = candidate; improved = true; }
                            }
                            v[axis] = chosen;
                        }
                        if (!improved) break;
                    }
                    for (int axis = 0; axis < 4; axis++) steps[axis] *= .5f;
                }
            }
            if (float.IsPositiveInfinity(best)) return false;
            position = bestPosition; finalRotation = bestRotation; fov = bestFov;
            return true;
        }

        static bool ResolveFixed(Vector3 actor, Vector3 target, Vector3[] points,
            Vector3 initialPosition, Quaternion rotation, float initialFov, float aspect, float nearClip,
            Vector2 pixels, JcBlackBulletCameraProfile settings, out Vector3 position, out Quaternion finalRotation, out float fov)
        {
            position = initialPosition; finalRotation = rotation; fov = initialFov;
            if (points == null || points.Length == 0 || settings == null) return false;
            var inverse = Quaternion.Inverse(rotation);
            var local = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                local[i] = inverse * (points[i] - initialPosition);
                if (local[i].z <= nearClip + .01f) return false;
            }
            var a = inverse * (actor - initialPosition);
            var b = inverse * (target - initialPosition);
            if (a.z <= nearClip || b.z <= nearClip) return false;
            var limits = ScreenLimits(pixels, settings);
            float initialT = Mathf.Tan(initialFov * Mathf.Deg2Rad * .5f);
            float depth = Mathf.Min(a.z, b.z);
            var travel = new Vector2(2 * depth * initialT * Mathf.Max(.01f, aspect) * Mathf.Clamp(settings.panFraction.x, 0, .2f),
                2 * depth * initialT * Mathf.Clamp(settings.panFraction.y, 0, .1f));
            float lower = Mathf.Tan(5f * Mathf.Deg2Rad);
            float upper = Mathf.Tan(Mathf.Clamp(settings.maximumFov, 30, 120) * Mathf.Deg2Rad * .5f);
            if (!Intervals(local, upper, aspect, limits, travel, out _)) return false;
            // JC | 2026-11-10 | 모든 표시점에 대한 선형 제약의 교집합에서 최소 가능 FOV를 이분 탐색합니다.
            for (int i = 0; i < 32; i++)
            {
                float mid = (lower + upper) * .5f;
                if (Intervals(local, mid, aspect, limits, travel, out _)) upper = mid; else lower = mid;
            }
            float minimum = upper;
            float iz = 1f / a.z + 2f / b.z;
            var desired = new Vector2((a.x / a.z + 2f * b.x / b.z) / iz, (a.y / a.z + 2f * b.y / b.z) / iz);
            float best = float.PositiveInfinity;
            // JC | 2026-11-10 | 2/3 초점은 소프트 목표입니다. 최소 FOV부터 최대 15% 여유 안에서 초점·이동량을 함께 평가합니다.
            float end = Mathf.Min(Mathf.Tan(Mathf.Clamp(settings.maximumFov, 30, 120) * Mathf.Deg2Rad * .5f), minimum * 1.15f);
            for (int i = 0; i <= 24; i++)
            {
                float tangent = Mathf.Lerp(minimum, end, i / 24f) + .00001f;
                if (!Intervals(local, tangent, aspect, limits, travel, out var interval)) continue;
                // 목적 함수의 무제약 최적점과 가용 구간을 교차하는 해이며, 적용 후 카메라를 잘라내는 보정이 아닙니다.
                var pan = new Vector2(Mathf.Clamp(desired.x * .9f, interval.x, interval.y), Mathf.Clamp(desired.y * .9f, interval.z, interval.w));
                float dx = (desired.x - pan.x) * iz / (3 * tangent * Mathf.Max(.01f, aspect));
                float dy = (desired.y - pan.y) * iz / (3 * tangent);
                float loss = tangent / minimum - 1;
                float cost = dx * dx + dy * dy + loss * loss * 3f + pan.sqrMagnitude / Mathf.Max(1, depth * depth) * .1f;
                if (cost >= best) continue;
                best = cost; position = initialPosition + rotation * new Vector3(pan.x, pan.y, 0);
                fov = Mathf.Atan(tangent) * 2 * Mathf.Rad2Deg;
            }
            return !float.IsPositiveInfinity(best);
        }

        static bool Intervals(Vector3[] points, float tangent, float aspect, Vector4 limits, Vector2 travel, out Vector4 interval)
        {
            interval = new Vector4(-travel.x, travel.x, -travel.y, travel.y);
            foreach (var p in points)
            {
                interval.x = Mathf.Max(interval.x, p.x - limits.y * p.z * tangent * aspect);
                interval.y = Mathf.Min(interval.y, p.x - limits.x * p.z * tangent * aspect);
                interval.z = Mathf.Max(interval.z, p.y - limits.w * p.z * tangent);
                interval.w = Mathf.Min(interval.w, p.y - limits.z * p.z * tangent);
                if (interval.x > interval.y || interval.z > interval.w) return false;
            }
            return true;
        }

        public static Vector4 ScreenLimits(Vector2 pixels, JcBlackBulletCameraProfile settings)
        {
            // JC | 2026-11-10 | 픽셀 여유와 UI 예약 영역을 같은 투영 제약에 포함합니다. x=좌/y=우/z=하/w=상 비율입니다.
            float x = Mathf.Clamp(settings.edgeMarginPixels / Mathf.Max(1, pixels.x), 0, .15f);
            float y = Mathf.Clamp(settings.edgeMarginPixels / Mathf.Max(1, pixels.y), 0, .15f);
            var ui = settings.uiInsets;
            return new Vector4(-1 + 2 * Mathf.Min(.4f, x + Mathf.Clamp(ui.x, 0, .3f)), 1 - 2 * Mathf.Min(.4f, x + Mathf.Clamp(ui.y, 0, .3f)),
                -1 + 2 * Mathf.Min(.4f, y + Mathf.Clamp(ui.z, 0, .3f)), 1 - 2 * Mathf.Min(.4f, y + Mathf.Clamp(ui.w, 0, .3f)));
        }

        public static float RequiredFov(Vector3[] points, Vector3 position, Quaternion rotation, float aspect, Vector2 pixels,
            JcBlackBulletCameraProfile settings, out bool feasible, float nearClip = .3f)
        {
            // JC | 2026-11-10 | 진입/복귀 경로도 적용 전 동일한 가시 부등식으로 FOV를 구합니다. 회전·이동 경로와 전투 시간은 변경하지 않습니다.
            float tangent = 0; var inverse = Quaternion.Inverse(rotation); var limits = ScreenLimits(pixels, settings);
            feasible = true;
            if (points == null) return 1;
            foreach (var point in points)
            {
                var p = inverse * (point - position);
                if (p.z <= nearClip + .01f) { feasible = false; continue; }
                tangent = Mathf.Max(tangent, Mathf.Max(p.x / (p.z * Mathf.Max(.01f, aspect) * (p.x < 0 ? limits.x : limits.y)),
                    p.y / (p.z * (p.y < 0 ? limits.z : limits.w))));
            }
            float result = Mathf.Max(1, 2 * Mathf.Atan(tangent) * Mathf.Rad2Deg);
            feasible &= result <= Mathf.Clamp(settings.maximumFov, 30, 120);
            return result;
        }

        // JC | 2026-11-10 | 시작 때 이미 보이는 점은 화면 안에 유지하면서 UI/미래 효과 예약 여백을 점진적으로 확보합니다.
        // 점별 시작 투영과 최종 경계 사이를 보간하므로 화면 안의 점에 다른 점의 화면 밖 여유가 전파되지 않습니다.
        // 시작 때 화면 밖인 미래 숫자 영역은 더 밖으로 밀지 않고 안으로 회수하며, 실제 생성된 숫자는 호출부에서 별도 엄격 보호합니다.
        public static float RequiredTransitionFov(Vector3[] points, Vector3 position, Quaternion rotation, float aspect, Vector2 pixels,
            JcBlackBulletCameraProfile settings, Vector3 startPosition, Quaternion startRotation, float startFov, float blend,
            out bool feasible, float nearClip = .3f)
        {
            if (blend >= 1f) return RequiredFov(points, position, rotation, aspect, pixels, settings, out feasible, nearClip);
            float tangent = 0;
            float startTangent = Mathf.Tan(Mathf.Clamp(startFov, 1, 179) * Mathf.Deg2Rad * .5f);
            float ratio = Mathf.Max(.01f, aspect);
            var inverse = Quaternion.Inverse(rotation);
            var startInverse = Quaternion.Inverse(startRotation);
            var finalLimits = ScreenLimits(pixels, settings);
            feasible = true;
            if (points == null) return 1;
            foreach (var point in points)
            {
                var p = inverse * (point - position);
                var start = startInverse * (point - startPosition);
                // JC | 2026-11-10 | 현재 근접면 뒤의 점은 기존 불가 판정입니다. 출발 때 투영할 수 없던 점은 여백을 완화하지 않습니다.
                if (p.z <= nearClip + .01f) { feasible = false; continue; }
                var limits = finalLimits;
                if (start.z > nearClip + .01f)
                {
                    float x = start.x / (start.z * startTangent * ratio);
                    float y = start.y / (start.z * startTangent);
                    limits = Vector4.Lerp(new Vector4(Mathf.Min(finalLimits.x, x), Mathf.Max(finalLimits.y, x),
                        Mathf.Min(finalLimits.z, y), Mathf.Max(finalLimits.w, y)), finalLimits, Mathf.Clamp01(blend));
                }
                tangent = Mathf.Max(tangent, Mathf.Max(p.x / (p.z * ratio * (p.x < 0 ? limits.x : limits.y)),
                    p.y / (p.z * (p.y < 0 ? limits.z : limits.w))));
            }
            float result = Mathf.Max(1, 2 * Mathf.Atan(tangent) * Mathf.Rad2Deg);
            feasible &= result <= Mathf.Clamp(settings.maximumFov, 30, 120);
            return result;
        }

        public static Vector3 Corner(Bounds box, int corner) => box.center + Vector3.Scale(box.extents,
            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
    }
}
