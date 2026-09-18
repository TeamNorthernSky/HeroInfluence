# 전투씬 하단 우측 고정 설명 — c084bd161c

- 목표/승인: 2026-09-15 사용자 “남은 구현 모두 진행” 요청. 합의된 고정 UI 7단계 구현이며 이전에 보류한 데이터·전용 설명·팝업·최종 삭제를 포함하지 않는다.
- 대상: TmpBattleScene.unity, 기존 SkillButtonController.cs / SkillButtonTooltip.cs, BattleSceneUI.md / BattleSceneUI_FollowUps.md.
- 구현: 씬의 SkillDescriptionPanel에 Title·Body·세 태그 부모/Label을 선배치. 기존 컨트롤러가 호버→선택→안내 순으로 표시. 적 턴 임시 문구, 종료·비활성 초기화. 다섯 스킬 버튼은 고정 설명으로 전달하고 다른 씬의 공용 DDOL 경로는 유지.
- 데이터: 기존 ClassSkillTooltipText / WeaponTooltipText, 기존 레벨 조회, 실제 스킬 분류 사용. 데이터·강화·시전 로직을 수정하지 않음.
- 보존: 새 런타임 스크립트·프리팹·리소스 파일 없음. 기존 모든 Transform 값 보존. 기존 UI 블록 삭제 없음. ASB 코드·원본 XLSX·SO·공용 툴팁 수정 없음. 무관 파일 중 NotoSansKR-Light SDF.asset 1개만 검증 중 Unity 자동 저장으로 해시 변경(10:24:52)을 감지했다. 최초 비교는 동일했으나 마지막 diff_check.py에서 이 예외를 검출하여 전체 해시 보존 주장을 정정했다. 기존 사용자 수정 파일이므로 통째로 원복하지 않았으며, 나머지 무관 파일은 동일하다.
- 검증: Unity 컴파일 확인. 격리 PreviewScene에서 38항목 통과(씬 참조·호버/선택/잠금/취소/코어 전환·턴 변경·적 턴·종료·구독 해제), 실제 카탈로그의 4캐릭터·안내·코어·적 턴·긴 문장 렌더링. 에이전트는 Play에 들어가지 않음. 검증 중 잘못된 OpenPreviewScene API를 쓴 임시 검사 스크립트 컴파일 오류 1건은 OpenScene/Additive로 고친 후 재검증했고 제품 코드 오류는 아님.
- 백업/검증 자료: C:/Dev/_scratchpad/battle-description-c084bd161c/ 아래 before(이번 작업 대상 2개 코드·씬·2문서), baseline.json, verify.cs, diff_check.py 및 렌더 PNG. 프로젝트 안에 테스트 스크립트 없음.
- 현재 상태: 구현·비플레이 검증·구조/후속 문서 갱신 완료. 사용자 플레이 검수 대기. 커밋·push 없음.
- 다음 확인: 탐사 조우 전투에서 안내 → 히어로/코어 호버 → 선택 후 다른 버튼 호버/이탈 → 잠금 버튼·우클릭 취소 → 적 턴 문구. 전용 문구와 이벤트 적의 잘못된 3번 스킬은 R04/R11 후속 목록 유지.
- 폰트 추적: 시작 SHA256 46cbc5635a4f4f48a66178b2d12ec7197290d0889004f0484d0829d0f2307ead → 검증 후 f25e6353dd6e9a6f6ac723398e3e6362f10f0a0e6d986ce3b4c7851ced541f32. Git 차이에는 TMP 글리프·아틀라스 직렬화가 포함된다. 작업 전 파일은 해시만 수집했으므로 이 턴의 자동 저장 부분만 정확히 역산하여 복원할 근거는 없다. 별도 수동 폰트 편집은 하지 않았다.
