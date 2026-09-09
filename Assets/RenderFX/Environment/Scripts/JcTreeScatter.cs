using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JC.Env
{
    /// <summary>
    /// 외곽 밴드 랜덤 산포기 (P3) — 맵 바운드 바깥 <see cref="bandWidth"/> 폭의 띠에 프리팹을 뿌린다.
    ///
    /// ★260908 결정: "근경"을 정밀 계산하지 않는다. 카메라가 맵에 클램프되므로 볼 수 있는 외곽의 합집합 = 맵 + 균일 밴드.
    ///   그 밖은 만들지 않는다(교수님: 근경만 하면 끝난다). 화면 밖은 프러스텀 컬링이 버리므로 연산 누수는 무시.
    /// 배치는 다트 던지기(최소 간격 리젝션) — 색은 좌표 셰이더가 정하므로 뿌리기만 하면 저절로 다양해진다(§4.1).
    /// 결과는 <see cref="root"/> 아래 자식으로 생성되고, 좌표 목록은 CSV 로 내보낼 수 있다(§4.4 교수님 제출 입력세트 4번).
    /// </summary>
    public class JcTreeScatter : MonoBehaviour
    {
        [Header("① 영역")]
        [Tooltip("게임플레이 맵 바운드(월드 XZ). 이 안에는 뿌리지 않는다. DHScene_3 = (-0.5,-0.5) ~ (102.5,79.5).")]
        public Rect mapBounds = new Rect(-0.5f, -0.5f, 103f, 80f);

        [Tooltip("맵 바깥으로 뿌릴 띠의 폭(유닛). 최대 줌아웃 뷰포트 깊이 ≈ 17 → 20 이면 어느 방향에서도 남는다.")]
        [Min(1f)] public float bandWidth = 20f;

        [Tooltip("추가 제외 영역(월드 XZ) — 도로 출구·바리케이드 자리 등.")]
        public List<Rect> exclusions = new List<Rect>();

        [Tooltip("테스트 씬처럼 맵 일부만 있을 때, 이 사각형 밖에는 뿌리지 않는다(빈 Rect = 제한 없음).")]
        public Rect clipToArea;

        [Header("② 밀도")]
        [Tooltip("나무 사이 최소 간격(유닛). 수관 폭 ~1.2~1.8 이면 1.6 이 빽빽한 숲.")]
        [Min(0.2f)] public float minSpacing = 1.6f;

        [Tooltip("배치 시도 상한 — 간격 조건을 만족하는 자리를 찾을 때까지 던지는 다트 수. 클수록 촘촘.")]
        [Range(100, 20000)] public int maxDarts = 6000;

        [Tooltip("최대 개수(0 = 제한 없음).")]
        [Min(0)] public int maxCount = 0;

        [Header("③ 변주")]
        public int seed = 260908;
        [Tooltip("균일 스케일 범위.")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);
        public bool randomYaw = true;
        [Tooltip("randomYaw 가 꺼졌을 때 모든 나무에 줄 고정 Y 회전(도). ★260908: 나무 정면(Blender +X+Y 대각)을 카메라(-Z) 로 향하게 하는 값 — 전 나무 동일(쓰리쿼터 뷰 테스트).")]
        public float fixedYaw = 0f;
        [Tooltip("바닥 높이(Y).")]
        public float groundY = 0f;

        [Header("④ 소스")]
        [Tooltip("뿌릴 프리팹(나무 5종). 비어 있으면 큐브 프리미티브로 대체(셰이더 검증용).")]
        public List<GameObject> prefabs = new List<GameObject>();
        [Tooltip("프리미티브 대체 시 씌울 재질.")]
        public Material fallbackMaterial;
        [Tooltip("생성 부모. 비우면 이 오브젝트.")]
        public Transform root;

        [Header("⑤ 런타임")]
        [Tooltip("플레이 시작 시 자동으로 뿌리고 정적 배칭한다. ★실씬(DHScene_3) 권장 ON — 나무 수천 그루를 씬 파일에 굽지 않아(YAML 3MB↑·머지 잡음) 팀 씬이 가벼워진다.\n" +
                 "에디터 프리뷰(▶ 뿌리기)는 별개 — 저장 전에 ✕ 지우기 권장.")]
        public bool scatterOnStart = false;

        [Header("⑥ 결과 (읽기 전용)")]
        public int lastCount;

        private Transform Root => root != null ? root : transform;

        private void Start()
        {
            if (!scatterOnStart || !Application.isPlaying) return;
            Scatter();
            // 정적 배칭: 같은 재질 나무들을 드로우콜 몇 개로 합친다(스케일·회전 제각각이어도 됨)
            StaticBatchingUtility.Combine(Root.gameObject);
        }

        /// <summary>이 점이 밴드 안(맵 밖, 맵+밴드 안, 제외 영역 밖)인가.</summary>
        public bool IsInBand(Vector2 p)
        {
            if (mapBounds.Contains(p)) return false;
            var outer = new Rect(mapBounds.xMin - bandWidth, mapBounds.yMin - bandWidth,
                                 mapBounds.width + bandWidth * 2f, mapBounds.height + bandWidth * 2f);
            if (!outer.Contains(p)) return false;
            if (clipToArea.width > 0f && clipToArea.height > 0f && !clipToArea.Contains(p)) return false;
            for (int i = 0; i < exclusions.Count; i++) if (exclusions[i].Contains(p)) return false;
            return true;
        }

        /// <summary>기존 결과를 지우고 다시 뿌린다.</summary>
        public void Scatter()
        {
            Clear();
            var rng = new System.Random(seed);
            var outer = new Rect(mapBounds.xMin - bandWidth, mapBounds.yMin - bandWidth,
                                 mapBounds.width + bandWidth * 2f, mapBounds.height + bandWidth * 2f);
            var sample = clipToArea.width > 0f && clipToArea.height > 0f ? Intersect(outer, clipToArea) : outer;

            // 공간 해시(셀 = minSpacing) 로 간격 검사 O(1)
            float cell = minSpacing;
            var grid = new Dictionary<(int, int), List<Vector2>>();
            var placed = new List<Vector2>();
            int accepted = 0;
            for (int d = 0; d < maxDarts; d++)
            {
                if (maxCount > 0 && accepted >= maxCount) break;
                var p = new Vector2(
                    sample.xMin + (float)rng.NextDouble() * sample.width,
                    sample.yMin + (float)rng.NextDouble() * sample.height);
                if (!IsInBand(p)) continue;
                int gx = Mathf.FloorToInt(p.x / cell), gz = Mathf.FloorToInt(p.y / cell);
                bool ok = true;
                for (int ox = -1; ox <= 1 && ok; ox++)
                    for (int oz = -1; oz <= 1 && ok; oz++)
                        if (grid.TryGetValue((gx + ox, gz + oz), out var list))
                            for (int i = 0; i < list.Count; i++)
                                if ((list[i] - p).sqrMagnitude < minSpacing * minSpacing) { ok = false; break; }
                if (!ok) continue;
                if (!grid.TryGetValue((gx, gz), out var l)) grid[(gx, gz)] = l = new List<Vector2>();
                l.Add(p); placed.Add(p); accepted++;
            }

            for (int i = 0; i < placed.Count; i++)
            {
                var p = placed[i];
                var go = Spawn(rng, i);
                go.transform.SetParent(Root, false);
                go.transform.position = new Vector3(p.x, groundY, p.y);
                go.transform.rotation = Quaternion.Euler(0f, randomYaw ? (float)rng.NextDouble() * 360f : fixedYaw, 0f);
                float s = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
                go.transform.localScale = Vector3.one * s;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.GameObjectUtility.SetStaticEditorFlags(go,
                    UnityEditor.StaticEditorFlags.BatchingStatic | UnityEditor.StaticEditorFlags.ReflectionProbeStatic | UnityEditor.StaticEditorFlags.OccludeeStatic);
#endif
            }
            lastCount = placed.Count;
        }

        private GameObject Spawn(System.Random rng, int index)
        {
            if (prefabs != null && prefabs.Count > 0)
            {
                var prefab = prefabs[rng.Next(prefabs.Count)];
#if UNITY_EDITOR
                if (prefab != null && !Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab))
                {
                    var inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, Root.gameObject.scene);
                    inst.name = $"{prefab.name}_{index:000}";
                    return inst;
                }
#endif
                if (prefab != null) { var g = Instantiate(prefab); g.name = $"{prefab.name}_{index:000}"; return g; }
            }
            // 폴백: 셰이더 검증용 박스(피벗 중앙 → 재질에서 _GradientBias 0.5, _GradientHeight 1 로 맞춘다)
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"ScatterCube_{index:000}";
            var c = cube.GetComponent<Collider>(); if (c != null) DestroyImmediate(c);
            cube.transform.localScale = new Vector3(1.2f, 2.4f, 1.2f);
            if (fallbackMaterial != null) cube.GetComponent<MeshRenderer>().sharedMaterial = fallbackMaterial;
            return cube;
        }

        /// <summary>생성물 전부 제거.</summary>
        public void Clear()
        {
            var r = Root;
            for (int i = r.childCount - 1; i >= 0; i--)
            {
                var ch = r.GetChild(i).gameObject;
                if (ch == gameObject) continue;
                DestroyImmediate(ch);
            }
            lastCount = 0;
        }

        /// <summary>좌표 목록 CSV — name,x,z,yaw,scale (교수님 제출 입력세트 4번).</summary>
        public string ToCsv()
        {
            var sb = new StringBuilder("name,x,z,yaw,scale\n");
            var r = Root;
            for (int i = 0; i < r.childCount; i++)
            {
                var t = r.GetChild(i);
                if (t.gameObject == gameObject) continue;
                sb.Append(t.name).Append(',')
                  .Append(t.position.x.ToString("0.00")).Append(',')
                  .Append(t.position.z.ToString("0.00")).Append(',')
                  .Append(t.eulerAngles.y.ToString("0.0")).Append(',')
                  .Append(t.localScale.x.ToString("0.00")).Append('\n');
            }
            return sb.ToString();
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            float x0 = Mathf.Max(a.xMin, b.xMin), z0 = Mathf.Max(a.yMin, b.yMin);
            float x1 = Mathf.Min(a.xMax, b.xMax), z1 = Mathf.Min(a.yMax, b.yMax);
            return new Rect(x0, z0, Mathf.Max(0f, x1 - x0), Mathf.Max(0f, z1 - z0));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(mapBounds.center.x, groundY, mapBounds.center.y), new Vector3(mapBounds.width, 0.01f, mapBounds.height));
            Gizmos.color = new Color(0.3f, 0.9f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(mapBounds.center.x, groundY, mapBounds.center.y),
                new Vector3(mapBounds.width + bandWidth * 2f, 0.01f, mapBounds.height + bandWidth * 2f));
            if (clipToArea.width > 0f)
            {
                Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.8f);
                Gizmos.DrawWireCube(new Vector3(clipToArea.center.x, groundY, clipToArea.center.y), new Vector3(clipToArea.width, 0.01f, clipToArea.height));
            }
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            foreach (var e in exclusions)
                Gizmos.DrawWireCube(new Vector3(e.center.x, groundY, e.center.y), new Vector3(e.width, 0.01f, e.height));
        }
    }
}
