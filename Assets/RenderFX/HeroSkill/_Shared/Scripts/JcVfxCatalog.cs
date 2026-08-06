using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// VFX 부품 대장 — <b>「바깥이 부를 이름」 ↔ 「그 이름이 가리키는 부품」</b>의 단일 정본.
    ///
    /// ★소유: JC. 부품을 만든 쪽이 이름을 짓는다.
    ///   바깥(전투 시스템·애니메이션 클립)은 이 표를 <b>읽기만</b> 한다. 반대 방향은 없다.
    ///
    /// ★규칙
    ///   - 이름 하나 = 부품 하나. 한 이름이 여러 부품을 부르지 않는다.
    ///   - 반대로 한 부품이 여러 이름을 갖는 것은 <b>재사용</b>이라 정상이다.
    ///     (예: ProjectileOrb ← heal_fire · lfl_fire · lfl_chain)
    ///   - 이름은 <c>&lt;스킬약칭&gt;_&lt;마디&gt;</c>. 접두는 강제, 접미는 흔한 마디만 권장 어휘.
    ///     권장 접미: charge · fire · impact · land · end · on/off
    ///
    /// ★부품이 「누가 시전했나 / 어디로 날아가나」를 아는 방법
    ///   부품은 <b>받는 구멍</b>만 갖는다. 값을 넣는 것은 바깥의 일이고,
    ///   테스트베드에서는 가짜 값이 들어온다. 부품은 누가 넣었는지 몰라도 된다.
    ///
    /// Prefab이 비어 있는 항목 = <b>아직 안 만든 부품</b>. 대장은 계획도 함께 담는다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC/VFX/부품 대장", fileName = "JcVfxCatalog")]
    public class JcVfxCatalog : ScriptableObject
    {
        /// <summary>
        /// 같은 이름의 <b>택일</b> 갈래. 「동시에 여럿」이 아니므로 이름 하나=부품 하나 규칙과 어긋나지 않는다.
        /// Base = 변종이 없거나 기본형.
        /// </summary>
        public enum Variant
        {
            Base,       // 기본(바닐라)
            Alt,        // 강화판·색 변종
            Miss,       // 실패 분기 (예: PawForYou 확률 미스테이크 = 녹색)
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("바깥이 부를 이름. 클립의 AniEvent_PresentationCue 인자와 일치시킨다.")]
            public string cueName;

            [Tooltip("같은 이름의 택일 갈래. 변종이 없으면 Base.")]
            public Variant variant = Variant.Base;

            [Tooltip("그 이름이 가리키는 부품. 비어 있으면 아직 안 만든 것.")]
            public GameObject prefab;

            [Tooltip("소속 캐릭터 (분류·검색용).")]
            public string character;

            [Tooltip("소속 스킬 표시명 (분류·검색용).")]
            public string skill;

            [Tooltip("이 부품이 무엇인지 한 줄로.")]
            public string note;

            /// <summary>trim + 소문자. 조회 키의 이름 부분.</summary>
            public string NormalizedName =>
                string.IsNullOrWhiteSpace(cueName) ? string.Empty : cueName.Trim().ToLowerInvariant();

            /// <summary>조회 키 = 이름 + 변종.</summary>
            public string Key => $"{NormalizedName}#{variant}";

            public bool IsReady => prefab != null;
        }

        [SerializeField] private Entry[] entries = new Entry[0];

        public IReadOnlyList<Entry> Entries => entries;

        private Dictionary<string, Entry> map;

        private void BuildMap()
        {
            map = new Dictionary<string, Entry>();
            if (entries == null) return;
            foreach (Entry e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.NormalizedName)) continue;
                if (!map.ContainsKey(e.Key)) map[e.Key] = e;
            }
        }

        /// <summary>
        /// 이름 + 변종으로 부품을 찾는다. 해당 변종이 없으면 <b>Base로 폴백</b>한다.
        /// (예: Miss 변종을 안 만든 이름은 Base가 나온다 — 조용히 아무것도 안 나오는 것보다 낫다)
        /// </summary>
        public GameObject Get(string cueName, Variant variant = Variant.Base)
        {
            return TryGet(cueName, variant, out Entry e) ? e.prefab : null;
        }

        public bool TryGet(string cueName, Variant variant, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(cueName)) return false;
            if (map == null) BuildMap();
            string n = cueName.Trim().ToLowerInvariant();
            if (map.TryGetValue($"{n}#{variant}", out entry)) return true;
            return variant != Variant.Base && map.TryGetValue($"{n}#{Variant.Base}", out entry);
        }

        public bool TryGet(string cueName, out Entry entry) => TryGet(cueName, Variant.Base, out entry);

        private void OnEnable() => map = null;

        private void OnValidate()
        {
            map = null;
            if (entries == null) return;

            // (이름+변종) 중복 — 대장의 존재 이유가 깨지는 유일한 오류라 에러로 띄운다.
            // ★같은 이름이라도 변종이 다르면 정상이다(택일이므로).
            var seen = new HashSet<string>();
            foreach (Entry e in entries)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.NormalizedName))
                {
                    Debug.LogWarning($"[VFX대장] {name}: 이름이 빈 항목이 있습니다.", this);
                    continue;
                }
                if (!seen.Add(e.Key))
                    Debug.LogError($"[VFX대장] {name}: 중복 '{e.cueName}' ({e.variant}) — " +
                                   "이름+변종 조합은 하나뿐이어야 합니다.", this);
            }
        }
    }
}
