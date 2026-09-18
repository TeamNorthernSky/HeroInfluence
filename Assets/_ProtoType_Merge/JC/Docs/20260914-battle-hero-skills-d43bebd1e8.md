# 히어로 4슬롯 UI — 20260914-d43bebd1e8

## 목표와 승인 범위
- 코어스킬 사용자 동작 확인 후 6번 히어로 스킬 UI 구현 요청.
- 기존 실행 연결 유지, 매 시전 수동 선택, 미결선 신규 기능 구현 제외.
- BattleManager.cs 편집 제외. 새 프로젝트 스크립트/테스트 파일 생성 제외. Play Mode 실행 제외.
- 이후 사용자가 피해량·적/캐릭터 스펙 조사 중단 지시. 해당 조사는 중단했으며 UI 작업만 마무리했다.

## 현재 상태
- 구현·컴파일·비플레이 검증·구조 문서 갱신 완료. 사용자 플레이 검수 대기.
- 수정: SkillButtonController.cs, SkillButtonTooltip.cs, InputHandler.cs, TmpBattleScene.unity, BattleSceneUI.md.
- 기존 버튼을 HeroSkillPanel 첫 슬롯으로 이관하고 3버튼 추가. 신규 C#·프로젝트 테스트·프리팹 원본 생성 없음. 삭제 없음.
- LockedOverlay 기존 프리팹 인스턴스를 4슬롯에 배치. 직접 RectTransform 배치, Scale1, LayoutGroup 없음.
- SkillButtonController 내부 직렬화 항목으로 슬롯별 Toggle/Button/Image/TMP/잠금/미결선 안내 참조. 별도 컴포넌트 파일 아님.
- InputHandler 실제 PendingAction + SelectionChanged 이벤트로 선택/취소 상태 동기화. 새 씬 selectSkillOnTurnStart=false. 기존 미개편 씬 기본 true 유지.
- 자동전투/시전중 재선택 무시, 새 수동모드의 레벨미달 연결 스킬 숫자키 선택 차단. 기존 단축키 유지.
- 기존 연결 스킬을 실제 기본판/강화판대로 표시. 다른 습득 슬롯은 연결 대기 비활성. 레벨 미달은 잠금, 비활성 버튼 툴팁 없음.
- 공용 스킬 툴팁/DDOL 유지. 7번 우측 설명 패널은 다음 단위.

## 검증 및 보존
- Unity 컴파일 완료 및 Edit Mode 확인. 저장본 재열기 확인, 마지막 Unity 조회 TmpBattleScene clean/Play false/compile false.
- 격리 PreviewScene: 현재 카탈로그의 4캐릭터 Lv1 사용가능 1슬롯/잠금3슬롯, Lv5/8 강화판 동일슬롯, 실제 연결 기본판 보존, 미결선 구분 확인.
- 선택/코어전환/취소 상태 동기화, 턴 시작 미선택, 자동전투/시전중 재선택 차단, 레벨 잠금 단축키 차단, 적턴 비활성, 구독해제 확인.
- 선택 성공 상태의 UI 동기화는 격리 입력 상태 주입으로 검사했다. 실제 타겟 클릭·시전·턴 진행은 Play 미실행이며 사용자 검수 대상.
- 하단 전체 편집 모드 미리보기 확인. 기존 씬 블록 중 의미 있는 변경 20개는 대상 UI/입력 참조뿐, 기존 삭제 0개.
- Light 폰트는 시작 당시 변경 상태를 바이트 그대로 복구. Regular 폰트는 시작 Git clean, 미리보기 후 발생한 자동 atlas 변경만 제거하여 HEAD와 내용 동일 확인.
- 씬 저장의 무관한 공백 변경 정리 후 git diff --check 통과.
- Git 커밋/push/브랜치 변경 없음. SVN 리소스 복사나 원본 Excel 수정 없음.

## 유지보수 및 다음 확인
- 저장소 BattleSceneUI.md가 현재 구조의 정본이며 다른 PC에서 이 로컬 기록을 요구하지 않는다.
- 현재 데이터의 습득 순서와 최신 기획 불일치, 강화 표현 및 추가 실행 결선은 후속 확인 대상으로 기록. 전투 데이터는 수정하지 않음.
- 사용자 확인: 시작/다음 턴 미선택 → 기존 연결 스킬 클릭 → 적 클릭 시전 → 선택 해제, 코어 전환/시전 후 해제, 잠금 클릭 불가.
- 사용자가 확인하기 전 7번 구현으로 확장하지 않는다.
- 임시 검증/백업: C:\Dev\_scratchpad\battle-skills-d43bebd1e8. 프로젝트 외부이며 커밋 대상 아님.
