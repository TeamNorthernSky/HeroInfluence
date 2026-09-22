# 저스티스 등장 — 도발 편집 기반

## 현재 연결

- 대상: `저스티스 등장` 기본·강화(1010, 1011)의 **원위치 복귀 후 도발**.
- `JC_EnterJustice_Taunt.blend`: 43개 뼈대와 도발 Action. 1~91프레임, 90fps, 1초.
- 원본 `Challenging_NoWeapon.fbx`의 3초 동작을 포즈 수를 유지하면서 3배속으로 변경했습니다.
- Blender 5.2.1 LTS / Unity 2022.3.62f3에서 확인했습니다.
- 현재 작업 파일은 애니메이션 뼈대입니다. 저스티스 외형 메시나 IK 조절용 리그까지 포함한 파일은 아닙니다. 외형·리타기팅 결과는 Unity 프리뷰 씬 2에서 확인합니다.

## 수정 → Unity 반영

1. `.blend`에서 뼈대의 Pose Mode / Action을 편집하고 저장합니다. 뼈 이름과 계층, Armature 객체의 기준 변환은 유지합니다.
2. Unity 플레이를 종료합니다. 프로젝트 루트에서 다음을 실행합니다.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python '.\SourceArt\Animation\Justice\export_animation.py' -- --source '.\SourceArt\Animation\Justice\JC_EnterJustice_Taunt.blend' --out '.\Assets\RenderFX\_Seam\Animation\Justice\Blender\JC_EnterJustice_Taunt.fbx' --rate 1
```

3. Unity 메뉴 `JC > Animation > Justice > Blender 도발을 작업 클립에 반영`을 실행합니다.
4. 프리뷰 씬 2에서 Q/W 선택 → 적 클릭으로 확인합니다.

작업 파일은 이미 3배속입니다. 다시 내보낼 때 `--rate 1`을 사용합니다. 이 도구는 선택한 활성 Action 하나를 내보내며 NLA, 여러 Action, 제어 리그 베이킹은 지원하지 않습니다. 실제 키프레임의 처음~끝을 출력 범위로 사용합니다.

## 파일 역할과 보존할 설정

- `Assets/RenderFX/_Seam/Animation/Justice/Blender/JC_EnterJustice_Taunt.fbx`: Blender 출력. `.meta`의 Humanoid 기준 자세 보정을 함께 보존합니다.
- `Assets/RenderFX/_Seam/Animation/Justice/JC_EnterJustice_Taunt.anim`: Unity 재생용. 위 메뉴는 GUID를 유지하고 내용만 갱신합니다. `cast` 이벤트는 원본 작업 도발에서 길이 비율에 맞춰 복사합니다.
- `JC_Fighter_SkillDetail.controller`: 공용 Base controller에서 분리한 저스티스 전용 controller. `JC_EnterJustice_Taunt` 상태만 추가했습니다. 향후 공용 controller의 상태/전이 구조를 수정한다면 이 사본에도 필요한 변경을 반영해야 합니다.
- `JC_FirstPass_Fighter.overrideController`: 기존 스킬 오버라이드는 유지하고 전용 controller에 연결합니다.
- 기존 `FP_Fighter_Taunt.anim`과 일반 `Taunt` 상태는 3초 그대로입니다.
- `JC_JusticeTaunt_Roundtrip.fbx`: 속도를 바꾸기 전 왕복 비교용 3초 기준 출력. 실제 스킬에서는 사용하지 않습니다.

Blender가 추가하는 Armature 상위 노드 때문에 원본 Avatar를 그대로 Copy하면 실패합니다. 전용 Avatar에 원본의 기준 T포즈를 맞춰 두었습니다. 또한 Root Transform의 원래 방향 유지·회전/Y 고정 설정을 바꾸면 같은 포즈도 다르게 재생됩니다. FBX의 Rig 설정 초기화나 `.meta` 삭제는 피합니다.

## 검증 기준

원본과 수정본의 같은 진행률에서 실제 Fighter 모델의 Humanoid 뼈를 91회 비교했습니다. 최대 위치 차이는 약 1.93mm, 회전 차이는 약 0.50도입니다. 저장한 `.blend`를 다시 열어 내보낸 후에도 같은 결과를 확인했습니다. 전진·타격·복귀 클립과 전투 배속 로직은 수정하지 않았습니다.
