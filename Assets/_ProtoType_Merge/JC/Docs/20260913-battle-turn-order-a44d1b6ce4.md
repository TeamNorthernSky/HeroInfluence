# 턴 순서 UI 개편

- 작업: 20260913-a44d1b6ce4 / 2026-09-13 / JC
- 승인: 사용자의 다음 단계 턴 순서 UI 개편 요청. 기존 상세 계획의 5슬롯 씬 선배치·수동 배치 적용.
- 상태: 구현 및 비플레이 검증 완료, 사용자 플레이 검수 대기.

## 현재 구현

실제 구조의 기준 문서는 `C:\Dev\HeroInfluence\Assets\_ProtoType_Merge\JC\Docs\BattleSceneUI.md` 4절이다. 이번 작업은 해당 문서를 갱신했다.

- TmpBattleScene/TurnOrderPanel에 기존 TurnSlotUI 프리팹 인스턴스 5개를 미리 배치했다. 위치·크기는 RectTransform으로 직접 조절한다. Scale=1.
- 파랑 턴 패널 이미지, 기존 진영 프레임·초상화 표시 코드를 사용한다. 현재 턴 라벨은 기존 highlightFrame 참조에 연결한다.
- TurnOrderUI에 sceneSlots 참조를 추가했다. 설정된 씬에서는 생성하지 않고 그 슬롯을 갱신한다. 미개편 씬은 기존 slotPrefab/slotParent 생성 경로 유지.
- 기존 TurnSlotUI.cs 및 프리팹 원본, BattleFlowManager/BattleManager/InputHandler는 수정하지 않았다. 새 프로젝트 스크립트는 없다.
- SlotParent의 HorizontalLayoutGroup은 비활성 보존했다. 최종 검증 후 삭제 후보이며 지금 삭제하지 않았다.
- 적 호버 툴팁은 후속 범위다. 이번에는 추가하지 않았다.

## 검증

- Unity Edit Mode에서 컴파일 완료 및 저장본 재열기 확인.
- 격리된 PreviewScene에서 5개 씬 참조, Scale1/자동 Layout 비활성, 1유닛 반복 5표시, 첫 칸 현재 턴, 빈 순서 숨김, 반복 갱신 중 중복 생성 없음, 미개편 씬의 기존 생성 경로 유지 확인.
- 시각 미리보기: `C:\Dev\_scratchpad\battle-turn-order-a44d1b6ce4\turn-order-preview.png`.
- Play Mode는 실행하지 않았다. 실제 전투 중 초상화/진영 프레임·턴 변경·전투 종료 숨김은 사용자 플레이 검수 대상이다.
- 도구 초기 시도에서 Sprite가 아닌 원본 이미지 경로로 인해 중단되었고, 이미 Sprite로 임포트된 기존 Resources 경로를 사용해 해결했다. 임포트 설정 변경/리소스 복사 없음.
- 미리보기 생성이 씬 dirty 표시를 남겨 검증 가드가 한 번 중단되었다. 별도 저장 복사본이 디스크와 동일함을 확인 후 재열어 검증 통과. 이 도구 오류 기록은 콘솔에 남아 있으므로 전체 콘솔 오류 0이라고 보고하지 않는다.
- 씬 저장이 추가한 무관한 BattleSceneManager/EnemySpawner 기본값 및 기존 버튼 공백 변경은 작업 시작 저장본과 대조해 원상 유지. TMP 두 폰트의 부수 변경도 시작 바이트로 복구했다.
- 기존 우상단 버튼 작업 및 재사용 비주얼 파일 보존. 커밋·push 없음.

## 다음

사용자가 턴 순서 UI를 검수한 후 다음 고정 UI 단위로 진행한다. 원본 삭제/호버 툴팁/다른 기능 변경을 자동으로 시작하지 않는다.

## 사용자 후속 배치 수정 반영

- 사용자 요청: 현재 턴 표시는 패널 고정 자식으로 이관. 사용자가 키운 패널에 맞게 슬롯 하위 크기와 배치 조절.
- 기준 저장본: scratchpad의 resize-before.unity. 사용자 변경은 패널600×178.2/위치41.2,-32, 슬롯 중심101.6~501.6/-94.6, 첫 슬롯85×85, CurrentTurn66.7×19.5, Label 글자15.
- 위 사용자 설정을 보존하고 5슬롯/Frame/PortraitMask를85×85, Portrait를74.375×74.375/중앙Y5.3125로 통일했다.
- 사용자 CurrentTurn/Label을 TurnOrderPanel 직속으로 이동, 화면 위치 유지. 5슬롯 highlightFrame 참조는 모두 해제했다. 해당 표시는 슬롯과 독립된 고정 장식이며 패널과 함께 숨겨진다.
- 나머지4개 표시는 비활성 LegacyCurrentTurnMarkers 아래로 이동하여 보관. 삭제 후보로 문서에 기록. 스크립트·프리팹 원본 수정 없음.
- 저장본 재열기 및 참조해제, 비활성 보관, 크기/Scale/Layout 확인 통과. 미리보기 turn-order-resized.png 확인. Play 미실행.
- 검증의 dirty 가드 중단은 별도 저장 복사본과 디스크 동일 확인 후 재열기로 해소했다. 사용자 미저장 변경을 버린 것이 아니다.
- 폰트 부수 저장은 이번 후속 작업 시작 바이트로 복원했다. 구조 문서 갱신.
