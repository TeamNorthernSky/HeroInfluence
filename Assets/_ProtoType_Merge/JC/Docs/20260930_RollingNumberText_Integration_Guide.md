# 공용 숫자 롤링 컴포넌트 적용 가이드 — KJ 및 구현 에이전트

작성·수정일: 2026-09-30.


## 1. 무엇을 가져다 쓰는가

**기존 숫자 TMP에 `RollingNumberText` 컴포넌트를 붙여 사용한다.** IP나 자원 보유량 하나가 증가·감소할 때 글자가 위로 빠져나가고 다음 글자가 아래에서 들어오는 연출이다. 숫자 값만 전달하면 필요한 마스크와 자릿수별 TMP는 컴포넌트가 실행 중 만든다.

공용 구현은 완료되어 전투 결과 IP에도 사용하고 있다. 탐사씬에는 아직 적용하지 않았으며 KJ가 해당 UI에 연결한다. 실제 획득·차감·저장 코드는 변경할 필요가 없다.

이 컴포넌트는 정수 숫자 하나를 표시한다. 이름, 단위, 소수점이 한 TMP에 섞여 있다면 숫자 영역을 별도 TMP로 분리한다. 폰트, 색, 크기, 굵기, 외곽선 재질은 각 화면의 기존 TMP를 기준으로 사용한다.

## 2. Unity에서 연결하는 순서

1. 탐사씬의 UI 설정 계층 아래에 숫자 연출 설정용 오브젝트를 만들고 `NumberRollSettings`를 붙인다. 이미 같은 역할의 설정 오브젝트가 있다면 그 오브젝트를 사용한다.
2. 자원량 또는 영웅 IP를 표시하는 기존 TextMeshProUGUI 오브젝트에 `RollingNumberText`를 붙인다.
3. 컴포넌트의 **Settings**에 앞서 만든 설정을 연결한다. 여러 숫자가 같은 설정 오브젝트를 참조할 수 있다.
4. **Alignment**로 가로 정렬을 정한다. 기본은 오른쪽 정렬이다. **Use Thousands Separator**를 켜면 천 단위 쉼표가 표시된다.
5. 기존 UI 스크립트에서 직접 텍스트를 쓰던 부분을 아래의 숫자 표시 함수 호출로 교체한다.
6. 화면을 처음 열거나 영웅을 교체할 때는 즉시 표시하고, 실제 획득·소비가 발생했을 때만 연출한다.

마스크와 글자 자식은 직접 배치하지 않는다. 원본 TMP 오브젝트와 RectTransform은 유지한다. 원본 TMP 컴포넌트의 렌더링만 잠시 끄고 생성된 글자들이 표시를 담당한다.

## 3. 조절 항목

| 위치 / 항목 | 기본값 | 의미 |
|---|---|---|
| NumberRollSettings / Duration | 0.8초 | 목표값까지 바뀌는 전체 실제 시간. 영이면 즉시 표시한다. |
| NumberRollSettings / Progress Curve | 끝에서 느려지는 감속 | 일정하게 움직이거나, 빨라지거나, 끝에서 느려지는 속도 변화를 정한다. |
| RollingNumberText / Settings | 미연결 가능 | 미연결이면 기본 시간과 감속을 사용한다. 팀에서 조절하려면 씬 설정을 연결한다. |
| RollingNumberText / Alignment | Right | Left, Center, Right 중 선택한다. 재생 중에는 확보한 자릿수 영역을 유지한다. |
| RollingNumberText / Use Thousands Separator | 꺼짐 | 정수에 천 단위 쉼표를 붙인다. 쉼표 자체는 굴리지 않는다. |

시간과 곡선은 새 연출을 시작할 때 읽는다. 글꼴·색·정렬·구분 기호도 즉시 표시하거나 연출을 시작할 때 적용한다. 진행 중 Inspector 값을 바꾸는 실시간 미리보기 기능은 제공하지 않는다.

숫자의 변화량이 커도 전체 시간은 같으며, 빠르게 지나가는 중간 숫자는 생략될 수 있다. 게임이 일시정지되어도 UI 연출 시간은 흐른다. 표시 영역이 좁으면 재생 시작 때 글자를 줄여 맞추며, 매 프레임 크기를 바꾸지는 않는다.

## 4. 각 화면에서 맡는 일

| 상황 | 호출 |
|---|---|
| 최초 표시, 저장 불러오기, 화면 다시 열기, 영웅 교체 | `SetValue` |
| 획득·소비로 보유량 변경 | `AnimateTo` |
| 현재 보이는 정수에서 멈추기 | `Stop` |
| 목표값으로 즉시 끝내기 | `Complete` |
| 생성 글자와 마스크를 없애고 원본 TMP로 돌리기 | `Release` |

같은 목표값이 다시 전달되면 재시작하지 않는다. 연출 중 새로운 값이 들어오면 현재 표시 위치에서 최신 목표값으로 이어간다. 반대 방향으로 바뀌는 경우에도 이전 시작값으로 되돌아가지 않는다.

오브젝트나 컴포넌트를 비활성화하면 목표값으로 정리하고 연출을 멈춘다. 다시 활성화할 때는 화면 담당 코드가 실제 최신 보유량을 `SetValue`로 전달해야 한다. 숨겨진 동안 데이터가 바뀌었는지는 숫자 컴포넌트가 알 수 없다.

원본 TMP를 직접 갱신하는 기존 코드가 남아 있으면 다른 표시와 충돌할 수 있다. 갱신 경로를 하나로 정리하고, 이벤트 구독과 해제는 기존 화면 스크립트가 담당한다.

## 5. 내부 구조와 소유권

```text
기존 보유량 TMP 오브젝트
 ├─ TextMeshProUGUI              스타일과 표시 영역의 기준
 ├─ RollingNumberText           해당 숫자의 값·목표·진행 상태
 └─ RollingNumberMask           실행 중 생성되는 마스크
     ├─ Digit0 / Next0          첫 번째 자리의 현재·다음 글자
     ├─ Digit1 / Next1          다음 자리의 현재·다음 글자
     └─ …

씬 숫자 연출 설정 오브젝트
 └─ NumberRollSettings          여러 숫자가 참조하는 시간·곡선
```

세 자리 숫자에는 현재·다음 글자용 TMP가 각각 세 개 필요하다. 생성한 글자는 재사용하며 프레임마다 만들거나 지우지 않는다. 필요한 자릿수가 늘면 추가 확보한다. 마스크는 숫자 영역 하나에 하나를 사용한다.

각 숫자 컴포넌트가 자신의 생성물과 진행 상태를 소유한다. 중앙 관리자나 DDOL에 개별 숫자를 등록할 필요는 없다. 공유되는 것은 코드와 시간 설정이며, 값과 폰트는 숫자별로 독립적이다.

## 6. 적용 후 확인

- 자원 획득과 소비 모두 자연스럽게 목표 보유량에 도달하는지 확인한다.
- 자릿수가 늘거나 줄 때, 쉼표가 있는 경우에도 잘림과 위치 튐이 없는지 확인한다.
- 연출 중 추가 획득이나 소비가 들어와도 이전 시작값으로 되돌아가지 않는지 확인한다.
- 같은 값의 반복 전달로 연출이 무한 재시작되지 않는지 확인한다.
- 화면 재표시와 영웅 교체에서 이전 대상의 숫자가 남지 않는지 확인한다.
- 실제 자원 지급 횟수와 저장값이 표시 연출 때문에 바뀌지 않는지 확인한다.

## 부록 — 구현 에이전트용 연결 명세

### A. 파일과 기존 연결 후보

HeroInfluence 저장소 기준:

- `Assets/_ProtoType_Merge/JC/Scripts/UI/RollingNumberText.cs`: 공용 표시 컴포넌트. TextMeshProUGUI와 같은 오브젝트에 부착한다.
- `Assets/_ProtoType_Merge/JC/Scripts/UI/NumberRollSettings.cs`: 씬별 시간·곡선 설정. 런타임 상태나 실제 재화 참조는 없다.
- `Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleResultIPPresentation.cs`: 공용 컴포넌트를 외부 진행률로 제어하는 실제 사용 예. 전투 전용 지연·완료 잔상은 여기에 남아 있다.

탐사 연결 후보는 최신 바인딩을 다시 확인한다. 이 문서 작성에서는 아래 파일을 수정하지 않았다.

- `Assets/_ProtoType_Merge/JC/Scripts/UI/ResourceHUDController.cs`: `EconomyManager.OnResourceChanged` 기반 표시. 로비·탐사 공용 TopBar 영향에 주의한다.
- `Assets/_ProtoType_Merge/JC/Scripts/UI/Exploration/ExplorationHeroBoxController.cs`: 영웅별 IP 표시.
- `Assets/_ProtoType_Merge/DH/Scripts/UI/ResourceUI.cs`: 별도 직접 텍스트 갱신 코드. 실제 해당 UI 사용 여부를 확인한다.

### B. 최소 연결 예제

다음은 호출 방법을 보여주는 예제다. 기존 화면 클래스에 같은 역할의 필드와 호출을 연결하며, 별도의 병행 데이터 갱신 체계를 만들지 않는다. 프로젝트의 실제 획득 이벤트 이름이나 데이터 접근자를 새로 가정하지 않는다.

```csharp
using UnityEngine;

public sealed class BalanceDisplayExample : MonoBehaviour
{
    [Tooltip("기존 보유량 TMP에 붙인 숫자 롤링 컴포넌트입니다.")]
    [SerializeField] private RollingNumberText number;

    // 최초 바인딩, OnEnable 이후 최신값 동기화, 영웅 교체 시 호출
    public void BindCurrentValue(long actualValue)
    {
        number.SetValue(actualValue);
    }

    // 기존 데이터 변경 이벤트에서 실제 확정 보유량을 전달
    public void OnActualValueChanged(long actualValue)
    {
        number.AnimateTo(actualValue);
    }

    // 화면 고유의 즉시 완료가 필요할 때만 호출
    public void FinishDisplay()
    {
        number.Complete();
    }
}
```

`AnimateTo`는 내부 Update에서 자동으로 진행한다. 화면 코드에서 `Tick`을 추가 호출하면 시간이 이중으로 진행하므로 호출하지 않는다. 첫 호출 전 `SetValue`가 없으면 `AnimateTo`도 즉시 초기값을 표시한다. 반드시 최초 실제값을 먼저 바인딩한다.

외부 데이터가 float라면 기존 UI의 표시 정밀도에 맞게 호출자가 long으로 변환한다. 반올림한 값을 실제 재화 데이터에 다시 쓰지 않는다. 소수점·접두사·단위·지역별 숫자 형식은 이 컴포넌트의 지원 범위가 아니다. 지원 형식은 부호 있는 정수와 선택적인 영문 천 단위 쉼표다.

### C. 공개 API와 수명 계약

| API | 계약 |
|---|---|
| `SetValue(long value)` | 목표와 표시를 즉시 확정. 진행 중 연출 취소. 스타일·배치 재적용. |
| `AnimateTo(long value)` | 현재 소수 진행 위치에서 새 목표로 재생. 같은 목표는 재시작하지 않음. 설정 시간을 새로 시작. |
| `Stop()` | 현재 표시 정수로 확정하고 정지. 소수 전환 단계는 남기지 않음. |
| `Complete()` | 목표값 확정. 재화 콜백 없음. |
| `Release()` | 생성물 제거, 마지막 표시 정수를 원본 TMP에 기록, 원래 enabled 상태 복원. 이후 재사용 가능. |
| `BeginManual(long from, long to)` | 외부 제어 모드로 시작·목표 자릿수를 확보. 자동 Update 재생 없음. |
| `SetProgress(float p)` | 외부 제어 모드에서만 적용. 범위 제한 및 역행 방지. 마지막에 반드시 1 전달. |
| `Tick(float unscaledDeltaTime)` | 자동 모드 진행의 테스트용 진입점. 일반 화면 코드는 호출하지 않음. |
| `IsAnimating` | 현재 연출 진행 여부. 실제 보상 처리의 완료 조건으로 사용하지 않음. |
| `DisplayedValue` / `TargetValue` | 현재 확정 정수 / 목표 정수. 읽기 전용. |
| `DisplayedPosition` | 소수 전환을 포함한 현재 표시 위치(decimal). 읽기 전용. |

외부 제어 예:

```csharp
number.BeginManual(oldValue, newValue);
// 호출자가 소유하는 시간에 맞춰 반복 호출
number.SetProgress(normalizedProgress);
// 완료 또는 조기 확인
number.Complete();
```

공용 컴포넌트는 지연, 완료 잔상, 저장, 이벤트 구독, 보상 승인을 수행하지 않는다. 필요하면 호출자가 제어한다. 파괴 시 자동 Release, 비활성화 시 Complete와 원본 표시 복원을 수행한다. 비활성화 상태의 `AnimateTo`는 즉시 표시한다. 같은 UI를 다른 대상에 재사용하면 반드시 `SetValue`로 이전 상태를 교체한다.

스타일은 원본 TMP의 Font Asset, 공유 재질, Font Style, Color, Font Size를 기준으로 복사한다. 원본 TMP의 자동 크기·문단·줄바꿈·사용자별 문자 간격까지 그대로 재현하지는 않는다. 자릿수는 고정 칸 폭과 가운데 정렬을 사용하며, 원본 RectTransform 폭과 높이에 맞춰 축소할 수 있다. 공유 폰트와 재질 자체는 수정하지 않는다.

### D. 계산과 경계

기본 감속은 `2t - t²`이며 키 `(0,0)`의 입출력 접선은 2, `(1,1)`의 입출력 접선은 0이다. 씬 설정의 곡선이 null이면 선형이다. 진행률은 단조 증가하도록 제한하고 재생 시간 종료 시 1을 강제한다.

현재 연출의 시작 표시 위치를 S, 목표 정수를 T, 진행률을 p라 하면:

```text
position = S + (T - S) * p
증가: 현재 정수는 floor(position), 다음 정수는 목표를 넘지 않는 다음 정수
감소: 현재 정수는 ceil(position), 다음 정수는 목표를 넘지 않는 이전 정수
fraction = abs(position - 현재 정수)
현재 글자 y = fraction * 칸 높이
다음 글자 y = (fraction - 1) * 칸 높이
```

값 위치 계산은 decimal을 사용한다. 정수 끝점은 long 범위에서 정확하게 처리한다. 자리 문자열이 같은 칸은 움직이지 않는다. 구분 기호와 부호는 회전시키지 않는다. 레이아웃은 시작·목표에 필요한 칸을 확보하고 재생 중 유지한다. 방향을 바꾸면 현재 위치를 새 시작점으로 삼되 글자 전환의 방향은 항상 위쪽이다.

### E. 검증 범위

공용 숫자와 전투 연동은 격리된 Edit 검사 56항목, 기존 HP/IP/EXP/랭크 회귀 검사 43항목 및 실제 카드의 정적 렌더로 확인했다. 비활성화 콜백은 Edit 검사에서 직접 호출해 검증했다. 탐사 씬의 실제 이벤트 연결과 Play 검수는 별도이며 KJ가 적용 후 확인해야 한다. 실제 Play 실행은 사용자 담당이다.
