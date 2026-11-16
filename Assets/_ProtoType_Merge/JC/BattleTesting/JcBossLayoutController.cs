using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-16 | 보스 배치 오프셋은 최초 위치를 기준으로 적용하고 매 프레임 이동을 누적하지 않습니다.
    // 건축물은 고정하며 생성 좌표와 셀·유닛을 함께 이동합니다. 카메라는 성공한 배치 변경 알림을 받아 연동합니다.
    [DisallowMultipleComponent]
    public sealed class JcBossLayoutController : MonoBehaviour
    {
        public JcBossLayoutProfile Source { get; private set; }
        public JcBossLayoutProfile Draft { get; private set; }
        public float TileSize { get; private set; }
        public string Status { get; private set; } = "보스 전투 시작 후 조절할 수 있습니다.";
        public bool Pending { get; private set; }
        // JC | 2026-11-16 | 카메라는 입력 중인 초안이 아니라 실제 적용에 성공한 캐릭터 이동량만 참조합니다.
        public Vector3 AppliedCharacterOffset => appliedCharacters;
        // JC | 2026-11-16 | 실제 배치가 바뀐 순간만 알립니다. 카메라가 매 프레임 씬을 검색하거나 미적용 초안을 추적하지 않게 합니다.
        public event Action CharacterLayoutApplied;
        Transform player, enemy, desk, floor;
        Vector3 playerBase, enemyBase, deskBase, appliedCharacters;
        PlayerSpawner playerSpawner;
        EnemySpawner enemySpawner;
        BattleFlowManager flow;
        BattlePresentationController presentation;
        MeshFilter floorFilter;
        Mesh originalFloor, editableFloor;
        Vector3[] floorBase, floorWork;
        bool[] decorationVertex;
        int[] stoneTriangles;
        Vector2 lastAll, lastCharacters, lastObjects;
        bool initialized;

        public void Initialize(JcBossLayoutProfile source, Transform stage, Transform players, Transform enemies, float tileSize)
        {
            if (initialized) return;
            if (!source || !stage || !players || !enemies) throw new ArgumentException("보스 배치 프로필/무대/양 진형 연결이 필요합니다.");
            Source = source; Draft = Instantiate(source); Draft.hideFlags = HideFlags.DontSave;
            TileSize = Mathf.Max(.5f, tileSize);
            player = players; enemy = enemies;
            playerBase = player.position; enemyBase = enemy.position;
            playerSpawner = player.GetComponent<PlayerSpawner>(); enemySpawner = enemy.GetComponent<EnemySpawner>();
            var roots = gameObject.scene.GetRootGameObjects();
            flow = roots.SelectMany(r => r.GetComponentsInChildren<BattleFlowManager>(true)).FirstOrDefault();
            presentation = BattlePresentationController.FindInScene(gameObject.scene);
            desk = stage.Find("FinalStage/Desk"); floor = stage.Find("FinalStage/Floor");
            if (!desk || !floor) throw new InvalidOperationException("보스 책상/바닥 연결을 확인하세요.");
            deskBase = desk.position;
            PrepareFloor();
            initialized = true;
            // JC | 2026-11-16 | 저장 배치는 첫 스폰 전에 반영합니다. 입장 HUD 대기 때문에 뒤늦게 진형이 튀지 않게 합니다.
            ApplyDraft(true);
        }

        void PrepareFloor()
        {
            // JC | 2026-11-16 | 기존 바닥의 문양 슬롯(1)과 맞닿은 타일 꼭짓점을 함께 이동해 구멍·겹침을 만들지 않습니다.
            // 원본 메시/재질/계층은 보존하며 20개 꼭짓점의 전용 런타임 사본만 사용합니다.
            floorFilter = floor.GetComponent<MeshFilter>();
            originalFloor = floorFilter ? floorFilter.sharedMesh : null;
            if (!originalFloor || !originalFloor.isReadable || originalFloor.subMeshCount != 2)
                throw new InvalidOperationException("보스 바닥의 타일/문양 2개 슬롯을 확인하세요.");
            floorBase = originalFloor.vertices; floorWork = (Vector3[])floorBase.Clone();
            var decoration = new HashSet<int>(originalFloor.GetIndices(1));
            decorationVertex = new bool[floorBase.Length];
            for (int i = 0; i < floorBase.Length; i++)
                decorationVertex[i] = decoration.Any(d => (floorBase[i] - floorBase[d]).sqrMagnitude < 1e-14f);
            stoneTriangles = originalFloor.GetTriangles(0);
            editableFloor = Instantiate(originalFloor); editableFloor.name = originalFloor.name + " (JC 보스 배치)";
            editableFloor.hideFlags = HideFlags.DontSave;
            floorFilter.sharedMesh = editableFloor;
        }

        public bool ApplyDraft() => ApplyDraft(false);

        bool ApplyDraft(bool initialLoad)
        {
            if (!initialized || !Draft || !isActiveAndEnabled) return false;
            // JC | 2026-11-16 | 공격 중에는 스킬이 저장한 왕복 좌표가 있으므로 종료까지 초안만 보관합니다.
            // 전투 진행을 중단하거나 카메라를 덮어쓰지 않고, 대기 중에는 변경 즉시 반영합니다.
            if (!initialLoad && ((flow && (flow.IsActionInProgress || flow.IsTurnPresentationPending)) ||
                (presentation && presentation.IsAttackCameraActive)))
            {
                Pending = true; Status = "스킬·턴 연출 종료 후 배치를 적용합니다."; return false;
            }
            Vector3 characters = ToWorld(Draft.allOffset + Draft.characterOffset);
            Vector3 objects = ToWorld(Draft.allOffset + Draft.objectOffset);
            if (!Finite(characters) || !Finite(objects)) { Pending = false; Status = "유효한 이동값을 입력하세요."; return false; }
            var localDelta = floor.InverseTransformVector(objects);
            for (int i = 0; i < floorWork.Length; i++) floorWork[i] = floorBase[i] + (decorationVertex[i] ? localDelta : Vector3.zero);
            for (int i = 0; i < stoneTriangles.Length; i += 3)
            {
                int a = stoneTriangles[i], b = stoneTriangles[i+1], c = stoneTriangles[i+2];
                var before = Vector3.Cross(floorBase[b]-floorBase[a], floorBase[c]-floorBase[a]);
                var after = Vector3.Cross(floorWork[b]-floorWork[a], floorWork[c]-floorWork[a]);
                if (Vector3.Dot(before,after) <= before.sqrMagnitude * .001f)
                { Pending = false; Status = "바닥 문양이 바닥 경계를 넘습니다. 오브젝트 이동량을 줄이세요."; RememberDraft(); return false; }
            }
            var previousCharacters = appliedCharacters;
            MoveCharacters(characters);
            desk.position = deskBase + objects;
            editableFloor.vertices = floorWork; editableFloor.RecalculateBounds();
            Physics.SyncTransforms();
            Pending = false; RememberDraft(); Status = "적용됨 · 저장 전까지 현재 전투의 임시 값입니다.";
            // JC | 2026-11-16 | 바닥/유닛/생성 좌표 갱신이 성공한 뒤 기본 카메라를 갱신합니다. 오브젝트만 이동하면 알리지 않습니다.
            if ((appliedCharacters - previousCharacters).sqrMagnitude > 1e-12f) CharacterLayoutApplied?.Invoke();
            return true;
        }

        void MoveCharacters(Vector3 offset)
        {
            if (!player || !enemy) return;
            var delta = offset - appliedCharacters;
            if (delta.sqrMagnitude < 1e-12f) return;
            // JC | 2026-11-16 | 보통 유닛은 셀 자식입니다. 분리된 시체도 자기 점유 셀이 진형 안에 있을 때만 함께 옮깁니다.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var unit in root.GetComponentsInChildren<BattleCharactor>(true))
            {
                if (!unit.OccupiedCell || unit.transform.IsChildOf(player) || unit.transform.IsChildOf(enemy)) continue;
                var cell = unit.OccupiedCell.transform;
                if (cell.IsChildOf(player) || cell.IsChildOf(enemy)) unit.transform.position += delta;
            }
            player.position = playerBase + offset; enemy.position = enemyBase + offset;
            appliedCharacters = offset;
            if (playerSpawner) playerSpawner.RefreshGridWorldPositions();
            if (enemySpawner) enemySpawner.RefreshGridWorldPositions();
        }

        Vector3 ToWorld(Vector2 tiles) => new Vector3(tiles.x * TileSize, 0, tiles.y * TileSize);
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        void RememberDraft() { lastAll = Draft.allOffset; lastCharacters = Draft.characterOffset; lastObjects = Draft.objectOffset; }
        public void ReloadDraft()
        {
            if (!Source || !Draft) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(Source), Draft); ApplyDraft();
        }
        void LateUpdate()
        {
            if (initialized && Draft && (Pending || Draft.allOffset != lastAll || Draft.characterOffset != lastCharacters || Draft.objectOffset != lastObjects)) ApplyDraft();
        }
        void OnDestroy()
        {
            // JC | 2026-11-16 | 씬 종료/조절기 제거 시 원본 위치·메시를 되돌리고 소유한 임시 자산만 해제합니다.
            if (initialized) MoveCharacters(Vector3.zero);
            if (desk) desk.position = deskBase;
            if (floorFilter && originalFloor) floorFilter.sharedMesh = originalFloor;
            Release(editableFloor); Release(Draft);
        }
        static void Release(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
