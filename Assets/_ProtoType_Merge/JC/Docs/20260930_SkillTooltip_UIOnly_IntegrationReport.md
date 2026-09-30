# 스킬 설명 UI 변경 및 로직 통합 검토 보고서

작성일: 2026-09-30. 작업: 20260930-fbeeb89e58.

## 승인 범위와 완료 내용

사용자 기획 변경: 리스크 시스템은 잠정 폐기, 코어스킬 IP 소모는 현재 없음. 이번 실행 범위는 UI·텍스트에만 한정하며 실제 동작 불일치와 통합 필요 사항을 이 보고서에 남긴다.

- ClassSkillTooltipText의 템플릿 경로와 카탈로그 미로드 대체 경로에서 ` · 리스크 확률 n%`를 제거했다.
- 협회 강화 `기본/+n` 표시는 요청 범위 밖의 삭제를 피하기 위해 유지했다. 설명의 피해·회복 계수와 백분율 치환도 유지한다.
- WeaponTooltipText의 코어스킬 설명 두 경로와 무기 레벨 설명 머리말에서 `(IP n)`을 제거했다. 비용이 0인 경우도 표시하지 않으며 제거한 행의 빈 줄도 남기지 않는다.
- 공용 생성기를 사용하므로 전투 우측 설명, 기존 스킬 호버, 히어로 정보, 연구소, 코어 선택창의 관련 설명이 함께 일치한다. 무기 레벨 설명의 스탯 표시는 유지한다.
- 씬·프리팹·데이터 테이블·저장 데이터·전투 비용/확률/강화 로직·버튼 사용 조건은 수정하지 않았다. 실제 Play를 실행하지 않았다.

수정 코드: [ClassSkillTooltipText.cs](../Scripts/UI/Lobby/ClassSkillTooltipText.cs), [WeaponTooltipText.cs](../Scripts/UI/Lobby/WeaponTooltipText.cs).

## 남는 불일치와 통합 검토 항목 — 이번에는 미구현

### 1. 코어 비용 표시 제거와 실제 비용 처리의 차이

현재 DHScene_3가 참조하는 V5.0 PlayerWeaponDataTable과 V5.1 테이블에서 HC001~HC005의 IPCost는 모두0이다. 따라서 확인한 데이터 기준으로 코어는 무료지만, 코드가 코어 무료를 강제하는 것은 아니다.

WeaponData.ToSkillData는 원본 IPCost를 그대로 복사한다. InputHandler의 입력 허용 검사, AutoBattleController의 자동전투 후보, SkillButtonController의 사용 가능 시각 표시, BattleManager의 IP 부족 검사 및 TryConsumeSkillInfluence 차감은 계속 비용을 읽는다. 이후 데이터에 양수 비용이 들어오면 UI에 비용이 보이지 않는 상태로 사용 제한·소모가 발생할 수 있다.

후속 담당 접점: ASB의 WeaponData/InputHandler/AutoBattleController/BattleManager, KJ의 SkillButtonController, DH 데이터 카탈로그·코어 원본 테이블. 코어 무료 정책을 데이터0 유지로 운영할지, 실행 시 일관되게 보장할지 결정한 뒤 각 경로의 판정을 맞춰야 한다. 일반 스킬 IP 정책까지 함께 변경하지 않도록 분리할 필요가 있다.

### 2. 리스크 확률 계산과 식별 데이터 잔존

HeroSkillRules.RiskChance는 기본35%, 협회 강화마다7%p 감소, +5에서0%를 계속 계산한다. SkillData의 riskKey/RiskChance, LabManager.GetSkillRiskChance, DHClassSkillTemplate.SkillRiskKey와 테이블 SkillRiskIndex 및 복사 경로도 유지했다.

Assets 전체 C# 검색에서 이 확률을 읽어 실제 확률 판정·오발·실패를 발생시키는 연결은 확인하지 못했다. 이는 검색 시점의 소스 근거이며 런타임의 모든 가능성을 증명한 것은 아니다. 일부 루미나 VFX에는 리스크 상황을 가정한 주석과 시험 기능이 남는다.

후속 검토: 잠정 폐기 상태에서 호출을 차단하는 정책과 데이터/저장 호환을 정리한다. 필드·테이블 열·VFX를 일괄 삭제하는 작업은 이번에 하지 않았으며, 일반 타깃/연출 기능까지 제거하면 안 된다.

### 3. 협회 강화의 효용과 재화 차감

현행 히어로 스킬에 대해 DHCsvTemplateCatalog.GetClassSkillValueAtLevel/GetClassSkillSubValueAtLevel은 변형별 고정 Lv1 계수를 반환한다. 협회 강화 단계가 올라가도 이 경로의 위력은 증가하지 않는다.

반면 LabManager의 강화 가능 검사·단계 증가와 LabModalController의 자금/칩 차감은 유지된다. 리스크를 폐기하면 강화 효과가 사용자에게 실질적으로 제공되지 않으면서 재화만 소모할 가능성이 있다. 이번에는 강화 버튼, 비용, 단계, 저장 값을 변경하지 않았다.

후속 결정: 협회 강화를 잠정 중단할지, 새로운 효과를 설계할지, 기존 강화 기록·소모 재화의 처리 기준을 정해야 한다. 일반 레벨업으로 얻는 기본판/강화판 스킬 습득은 협회 강화와 다른 기능이므로 함께 제거하지 않는다. UI에 남긴 `협회 강화 기본/+n`과 기존 Lv 표기도 이 결정에 맞춰 별도로 검토한다.

### 4. 다른 설명 생성 경로와 원본 문구

KJ SkillDescriptionBuilder에는 IP 토큰 치환과 `IP 소모:` 자동 추가 코드가 남아 있다. 이번 우측 설명의 실제 호출은 ClassSkillTooltipText/WeaponTooltipText이며, 조사한 C#에서 SkillDescriptionBuilder의 호출은 발견하지 못했다. 공용 일반 스킬용 기능일 수 있어 삭제하지 않았다.

이번 변경은 확인된 자동 부가 문구만 제거한다. 원본 테이블의 설명에 향후 리스크/IP 문구가 직접 들어오면 별도 정합 검토가 필요하다. 모든 IP 텍스트를 정규식으로 지우거나 일반 스킬·자원 UI까지 숨기는 처리는 하지 않았다.

## 검증과 보존

소스 차이는 두 UI 생성기의 문자열2곳 교체와 코어 IP문구3곳 제거에 한정한다. 기존 설명/계수/스탯 생성 경로는 유지한다. Unity 컴파일과 기존 씬 상태를 확인하며, 결과는 아래 완료 기록을 따른다. 실제 스킬 비용·확률·강화의 전투 검증은 이번 범위가 아니다.

기존 Light/Regular SDF 자동 캐시 변경은 별도 기존 변경으로 보존한다. 작업 시작의 추적 C# 및 폰트 해시는 C:/Dev/_scratchpad/tooltip-fbeeb89e58/before-hashes.json에 보관한다. 프로젝트 전체를 정리하거나 커밋하지 않는다.

완료 확인: Unity Edit mode, compiling=false, compileFailed=false. 기존 DHScene_3 dirty=false 유지. 시작 해시 대조에서 추적 C#/폰트 1080개 중 위 UI 코드2개만 변경됐으며 실제 로직과 기존 폰트 내용은 보존됐다. 실제 Play 검증은 수행하지 않았다.
