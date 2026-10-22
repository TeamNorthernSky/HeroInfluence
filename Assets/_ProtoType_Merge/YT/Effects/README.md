# 전투 이펙트 작업 규칙

- 사용자가 이펙트를 지정하면 원본을 직접 수정하거나 작업 프리팹에 원본을 중첩하지 않는다.
- 먼저 작업용 사본을 만든 뒤 크기, 색상, 방향, 입자 및 재생 동작을 수정한다.
- 원본 재질, 텍스처, 메시, Shader Graph도 작업용 사본으로 연결한다. 공용 실행 스크립트와 Unity/패키지 리소스는 공유한다.
- 같은 작업용 리소스는 재사용하고, 원본과의 프리팹·재질 연결이 남아 있지 않은지 확인한다.

`SourceCopies/VFX_Klaus/`에는 현재 전투 이펙트가 사용하는 원본 리소스의 독립 사본이 있다.
스킬에서 사용하는 작업 프리팹은 기존 `RenderFX/HeroSkill/Justice`와 `Resources/BattleEffects` 경로를 유지한다.

저스티스 일반 피격의 색 편집 대상은 `SourceCopies/VFX_Klaus/Prefabs/Hyper Casual FX/HCFX_Hit_03.prefab`이다.
`Resources/BattleEffects/HCFX_Hit_03_Justice_Normal.prefab`은 이 작업용 사본의 Variant이며 색을 덮어쓰지 않는다.
색을 수정하고 저장하면 이후 생성되는 일반 피격에 반영된다. 이 연결을 다시 독립 복사로 끊지 않는다.
