using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★VFX 위치 프리셋 — 「부품을 앵커의 어디에 놓을 것인가」를 자산으로 소유한다 (260804 신설).
    ///
    /// 왜 자산인가:
    ///   위치가 씬(스테퍼 스텝)에 흩어져 있으면 ① 플레이모드에서 잡은 값이 종료와 함께 날아가고
    ///   ② 씬마다 따로 고쳐야 하며 ③ 실전 결선 쪽과 공유할 수 없다.
    ///   ScriptableObject 는 <b>플레이모드에서 고친 값이 그대로 남으므로</b>,
    ///   「플레이 중 조절 → 저장」이 자연스럽게 성립한다.
    ///
    /// 역할 분담:
    ///   앵커가 <b>누구인가</b>(시전자/대상/연쇄 n번째) = 시퀀스의 구조 → 스텝(호출자)이 갖는다.
    ///   앵커의 <b>어디인가</b>(소켓 + 오프셋) = 튜닝 값 → 이 자산이 갖는다.
    ///
    /// 키 규칙: 기본 = 큐 이름. 같은 큐를 다른 지점에서 두 번 부르면(paw_warp 출발/도착)
    ///   스텝의 placementKey 로 구분한다(예: paw_warp_out / paw_warp_in).
    ///   여러 스텝이 같은 키를 참조하면 <b>한 값으로 같이 움직인다</b>(heal_fire → heal_charge).
    ///
    /// ★오프셋은 미터 단위, 앵커 축 기준 — <b>회전만 적용하고 스케일은 무시</b>한다.
    ///   TransformPoint 를 쓰지 않는 이유: 믹사모 본 소켓은 lossyScale 이 100이라
    ///   로컬 오프셋이 100배로 튄다. 회전×미터 방식은 루트든 본이든 같은 감각으로 움직인다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC/VFX/위치 프리셋", fileName = "VfxPlacementPreset")]
    public class JcVfxPlacementPreset : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("위치 키. 기본은 큐 이름 그대로. 같은 큐를 두 지점에서 부르면 스텝의 placementKey 로 갈라 쓴다.")]
            public string key;

            [Tooltip("기준 소켓 이름(앵커 계층에서 이름 검색). 비면 앵커 루트 = 캐릭터 발밑 기준점.\n" +
                     "예: Socket_L_VFX / Socket_R_VFX / Socket_FrontProjectile")]
            public string socketName;

            [Tooltip("기준점으로부터의 오프셋(m). 기준점의 회전을 따르고 스케일은 무시한다.")]
            public Vector3 offset;

            [Tooltip("메모(어느 마디의 위치인지 등).")]
            public string note;

            public string NormalizedKey => string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }

        [SerializeField] private Entry[] entries = new Entry[0];
        public IReadOnlyList<Entry> Entries => entries;

        [NonSerialized] private Dictionary<string, Entry> _map;

        public bool TryGet(string key, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (_map == null) BuildMap();
            return _map.TryGetValue(key.Trim().ToLowerInvariant(), out entry);
        }

        /// <summary>
        /// 앵커와 항목으로 최종 월드 좌표를 낸다.
        /// 소켓이 지정됐는데 못 찾으면 앵커 루트로 폴백한다(경고는 호출자 몫 — 매 프레임 스팸 방지).
        /// </summary>
        public static Vector3 Resolve(Transform anchor, Entry entry)
        {
            if (entry == null) return anchor != null ? anchor.position : Vector3.zero;
            return Resolve(anchor, entry.socketName, entry.offset);
        }

        /// <summary>
        /// 소켓+오프셋 → 월드 좌표. 부품 프리셋이 위치를 소유하는 경우(1·2번 프리셋 등)도
        /// 같은 규칙을 쓰도록 공개한 원형 — 위치 계산 규칙은 이 한 곳에만 둔다.
        /// </summary>
        public static Vector3 Resolve(Transform anchor, string socketName, Vector3 offset)
        {
            Transform basis = anchor;
            if (anchor != null && !string.IsNullOrEmpty(socketName))
            {
                foreach (var t in anchor.GetComponentsInChildren<Transform>(true))
                    if (t.name == socketName) { basis = t; break; }
            }
            if (basis == null) return offset;

            // 회전만 적용, 스케일 무시 — 믹사모 본(lossyScale 100)에서도 오프셋이 미터로 유지된다.
            return basis.position + basis.rotation * offset;
        }

        private void BuildMap()
        {
            _map = new Dictionary<string, Entry>();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key)) continue;
                string k = e.NormalizedKey;
                if (_map.ContainsKey(k))
                {
                    Debug.LogError($"[JcVfxPlacementPreset] 위치 키 중복: '{k}' — 뒤 항목을 무시합니다.", this);
                    continue;
                }
                _map.Add(k, e);
            }
        }

        private void OnValidate()
        {
            _map = null;   // 인스펙터 수정 즉시 반영
            var seen = new HashSet<string>();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key)) continue;
                if (!seen.Add(e.NormalizedKey))
                    Debug.LogError($"[JcVfxPlacementPreset] 위치 키 중복: '{e.NormalizedKey}'", this);
            }
        }
    }
}
