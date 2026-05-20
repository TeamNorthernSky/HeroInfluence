# KJ FogofWar_Test 씬 디버깅 과정 기록

> KJ_Work의 FogofWar_Test 씬에서 전장의 안개가 작동하지 않던 문제를 분석하고 해결한 과정을 기록합니다.  
> 작성일: 2026-04-09

---

## 1. 초기 증상

- FogOverlayPlane이 **완전 불투명 검정**으로 렌더됨
- 플레이어 근처의 안개가 걷히지 않음
- 콘솔에 에러 없음

---

## 2. 씬 구조 파악

```
Main Camera (Renderer: Default[-1])
FogOverlayPlane (Plane, FogOfWarOverlayMat → Custom/FogOfWarOverlay)
  Position: (0, 0.1, 0), Scale: (12.8, 1, 12.8) → 실제 128×128 영역
FogOfWarManager (mapSize: 128×128, resolution: 1024)
Player (FogOfWarUnit, visionRadius: 15)
Directional Light
Plane (바닥)
```

### 두 가지 안개 방식이 혼재

- **방식 A**: FogOfWarRenderFeature (후처리 Blit) → Renderer에 미등록 상태
- **방식 B**: FogOverlayPlane (오브젝트 셰이더) → 씬에서 활성화된 방식

방식 B가 실제 사용 중이므로, FogOfWarManager → RT → FogOfWarOverlay 셰이더 경로를 분석.

---

## 3. 1차 시도: View/Projection 매트릭스 수정

### 추론

FogOfWarManager.LateUpdate()에서 가상 카메라 매트릭스를 구성하는 코드:

```csharp
// 원본
Quaternion.LookRotation(Vector3.down, Vector3.up)
```

`LookRotation(forward, up)`에서 forward=down, up=up이면 **forward와 up이 반대 방향**이라 행렬이 불안정할 수 있음.

### 조치

```csharp
// 수정
Quaternion camRot = Quaternion.Euler(90f, 0f, 0f);
```

### 결과: 실패

여전히 검게 나옴.

---

## 4. 2차 시도: GPU Projection 보정 추가

### 추론

Unity에서 RT에 렌더링할 때 플랫폼별 Projection 보정이 필요함 (Reversed-Z 등).

### 조치

```csharp
Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(proj, true);
_cb.SetViewProjectionMatrices(view, gpuProj);
```

### 결과: 실패

여전히 검게 나옴.

---

## 5. 매트릭스 수치 검증 (전환점)

### 디버그 스크립트로 클립 좌표 계산

MCP script-execute로 Quad 꼭짓점의 local → world → clip → NDC 변환을 추적:

```
local(-0.50, -0.50, 0.00) → world(-14.50, 0.00, -20.50) → ndc(-0.23, 0.32, 1.50)
```

**NDC Z=1.50** — 클립 범위(0~1) 밖. GPU가 모든 꼭짓점을 잘라냄.

### 원인 확정

View 매트릭스에서 월드 Y=0(바닥)이 View Space Z=+100으로 변환됨. 직교 투영의 near=0.1, far=200 범위 내이지만, GPU Projection 변환 후 NDC Z가 1.0을 초과하여 클리핑됨.

근본 원인: **가상 카메라 매트릭스 구성 방식 자체가 Unity의 좌표 규약과 불일치.**

---

## 6. 3차 시도: NDC 직접 매핑 (성공)

### 추론

가상 카메라를 사용하지 않고, 월드 좌표를 NDC(-1~1)로 직접 계산하여 Quad를 배치.

### 조치

```csharp
// VP를 identity로 설정
_cb.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

// 월드 XZ 좌표 → NDC (-1 ~ 1)로 직접 매핑
float ndcX = (pos.x - mapMinX) / mapSize.x * 2f - 1f;
float ndcY = (pos.z - mapMinZ) / mapSize.y * 2f - 1f;
float ndcScaleX = (r * 2f) / mapSize.x * 2f;
float ndcScaleY = (r * 2f) / mapSize.y * 2f;

Matrix4x4 trs = Matrix4x4.TRS(
    new Vector3(ndcX, ndcY, 0f),
    Quaternion.identity,
    new Vector3(ndcScaleX, ndcScaleY, 1f)
);
_cb.DrawMesh(_quadMesh, trs, visionMaskMaterial);
```

### 결과: 부분 성공

안개가 걷히기 시작했으나, **Z좌표가 반전**됨 (플레이어가 +Z로 이동하면 -Z 방향이 밝혀짐).

---

## 7. 4차 시도: Z반전 수정 (최종 성공)

### 추론

초기에 `GL.GetGPUProjectionMatrix(identity, true)`를 사용했는데, 두 번째 인자 `true`(RT 렌더링)가 DirectX에서 **Y축을 뒤집음**. RT에 그려지는 마스크가 상하 반전되어, Overlay 셰이더의 UV 매핑과 불일치.

### 조치

```csharp
// GPU Projection 보정 제거, 순수 identity 사용
_cb.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
```

### 결과: 성공

안개가 정상적으로 플레이어 위치에서 걷히고, Z좌표도 올바르게 작동.

---

## 8. 최종 수정 요약

FogOfWarManager.cs에서 변경한 내용은 **LateUpdate() 내 매트릭스 설정 + Quad 배치 방식**뿐:

| 항목 | 원본 | 수정 |
|------|------|------|
| VP 매트릭스 | 가상 카메라 TRS + Ortho | `Matrix4x4.identity` 양쪽 |
| Quad 위치 | 월드 좌표 + Euler(90) 회전 | NDC 직접 계산, 회전 없음 |
| Quad 크기 | 월드 단위 (r*2) | NDC 비율 계산 |

Blit 누적(Max Blend), 셰이더, 머티리얼, RT 설정은 **원본 그대로 정상 작동**.

---

## 9. 교훈

### Command Buffer + DrawMesh에서 가상 카메라 매트릭스 사용 시 주의점

1. Unity의 View 매트릭스 좌표 규약(카메라 앞 = -Z)과 직교 투영의 near/far 매핑이 직관적이지 않음
2. `GL.GetGPUProjectionMatrix(proj, true)`는 RT 렌더링 시 필수이지만, Y축 반전을 유발할 수 있음
3. 복잡한 매트릭스 구성 대신 **NDC 직접 매핑**이 더 안전하고 디버깅이 쉬움
4. 클립 좌표 디버깅: 꼭짓점의 NDC 값을 직접 계산하여 -1~1 범위 내인지 확인하는 것이 가장 확실한 검증 방법

### 디버깅 방법론

1. RT 내용 읽기: `ReadPixels`로 RT 픽셀을 샘플링하여 데이터가 실제로 그려지는지 확인
2. 매트릭스 수치 검증: MVP 변환을 코드로 재현하여 클립/NDC 좌표가 유효 범위인지 확인
3. 단계별 격리: "RT에 그려지는가?" → "전역 텍스처가 바인딩되는가?" → "셰이더가 샘플링하는가?" 순서로 문제 지점 좁히기

---

## 10. 추가 발견사항

### AutoBootstrap과 GameManager

JC의 AutoBootstrap이 `BeforeSceneLoad`에서 GameManager를 생성하므로, KJ의 FogofWar_Test 씬에서도 GameManager(PlayGridManager, PlayFogManager 포함)가 로드됨. 단, 다른 전역 텍스처명을 사용하므로 **렌더링 충돌은 없음**.

| JC 전역 텍스처 | KJ 전역 텍스처 | 충돌 |
|---------------|---------------|------|
| `_VisibilityTex` | `_FogCurrentRT` | 없음 |
| — | `_FogVisitedRT` | 없음 |
| — | `_FogMapBounds` | 없음 |

### 미커밋 스크립트 에러

KJ의 `URP Renderer.asset`이 참조하는 `PostProcessFogOfWarFeature` 스크립트(GUID: d9085cdd...)가 커밋되어 있지 않음. KJ 로컬에만 존재하는 것으로 추정. 해결: 해당 .cs 파일을 커밋하거나, Renderer Asset에서 Feature 참조 제거.
