# 하단 현재 행동 유닛 정보 UI

- 작업: 20260913-3672ec1bbd / 2026-09-13 / JC
- 승인: 3번 하단 배경 확인 후 다음 단계 진행 요청. 합의된4번 초상화/이름/HP/IP/임시랭크 구현.
- 상태: 구현·비플레이 검증 완료, 사용자 플레이 검수 대기.

## 현재 변경

- 기존 HeroInfoPanel.cs 재사용. portraitImage 직렬화 참조가 있으면 런타임 생성하지 않음. showMaxIp 기본true로 미개편 씬 IP형식 유지, 새정보는false. 활성화 시 CurrentUnit 초기갱신, 적 초상화 라이브러리 연결, 빈대상 초상화숨김. HP 갱신 디버그로그 제거. 한국어 Inspector Tooltip 추가.
- TmpBattleScene/BottomPanel/ActorInfoPanel에 기존 컴포넌트를 부착하고 씬 Image/TMP 참조 연결. 기존 스크립트 파일 신설 없음.
- PortraitArea/Portrait, StatsArea의 ActorName/Rank/HpLabel/HpBar/Fill/HpValue/IpIcon/IpValue 배치. Scale1, 수동RectTransform. 이름우측 임시랭크. HP=현재/최대, IP=현재만.
- HeroInfoWindow와 별도Rank GameObject 비활성보존. 옛 컴포넌트와 참조 유지, 신규 활성 컨트롤러1개. 월드 UnitHPBar/전투흐름/스킬/공용라이브러리 미수정.
- 프로젝트 기존 Bar/Stat Sprite 사용. 리소스 복사/임포트 설정 변경 없음.
- 현재 구조·수치·이벤트·기존씬 호환·삭제후보는 프로젝트 BattleSceneUI.md에 반영.

## 검증

- Unity 컴파일 새코드 로드 완료, 저장본 재열기 및 씬참조 검사.
- 격리한 PreviewScene에 복제한 정보 UI와 임시 유닛으로 검증: 씬초상화 중복생성 없음, 활성화 즉시 현재유닛, HP올림/게이지비율, IP현재수치, HP/IP이벤트, 적초상화, 턴전환 이전구독해제, 기존 최대IP형식, 빈대상 초기화, 비활성구독해제 통과.
- 검증 도구에서 PreviewScene을 ActiveScene으로 지정하는 지원되지 않는 호출이 한번 실패함. PreviewScene으로 임시 오브젝트를 명시적으로 이동하는 방식으로 바꿔 재검증 통과. 프로젝트 코드 오류가 아님. 해당 도구 오류 콘솔 기록은 남김.
- 시각 미리보기: C:\Dev\_scratchpad\battle-actor-3672ec1bbd\actor-preview.png. 초상화가 테두리를 가리지 않도록200×200 내부여백 조정 후 재확인.
- Play Mode 미실행. 사용자 검수 항목: 실제 전투 턴에 맞는 이름/초상화/HP/IP, 플레이어와 적 전환, 구형UI 숨김, 레이아웃과 긴이름/큰숫자 가독성.
- 시작 씬 대비 신규37개 YAML블록/삭제0. 기존 변경은 정보부모 자식목록/컴포넌트 연결, 옛2개GameObject 비활성, 기존 HeroInfoPanel 새필드 기본값 직렬화뿐. 그 외 기존블록 보존. 폰트 부수변경은 이번 시작 바이트로 복구.
- 커밋·push 없음. 새 프로젝트 테스트 파일 없음. 검증 코드는 scratchpad에만 저장.

## 후속

사용자 검수 후 5번 코어스킬 출력으로 진행한다. 스킬 기능이나 적 실제 시전 설명을 이번 작업에 추가하지 않는다.
