using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// VFX가 「어디를 겨누는가」를 받는 최소 단위. 다중 대상 이펙트(LFL 연쇄·Tao 광선 다발 등) 공용.
    ///
    /// ★트랜스폼이 우선이고 좌표는 대비책이다.
    ///   착지 힐 오라처럼 <b>유닛 위에 얹히는</b> 연출은 대상이 움직이면 따라가야 한다(넉백·회피).
    ///   반면 빈 칸·지면 표식처럼 트랜스폼이 없는 대상도 있어서 좌표만으로도 지정할 수 있어야 한다.
    ///   그래서 둘을 한 자루에 담고, 읽는 쪽은 <see cref="Position"/> 하나만 본다.
    ///
    /// ★대상이 도중에 파괴되면 <see cref="anchor"/> 가 null 이 되지만 <see cref="fallbackPos"/> 는 남는다.
    ///   연출이 허공에서 끊기지 않게 하려는 것이므로, 주입할 때 좌표를 함께 채워 두는 편이 안전하다
    ///   (<see cref="Of(Transform)"/> 가 그렇게 만든다).
    /// </summary>
    [System.Serializable]
    public struct VfxTarget
    {
        [Tooltip("추종할 대상. 있으면 이쪽이 우선.")]
        public Transform anchor;

        [Tooltip("트랜스폼이 없거나 파괴됐을 때 쓸 고정 좌표.")]
        public Vector3 fallbackPos;

        public Vector3 Position => anchor != null ? anchor.position : fallbackPos;

        /// <summary>대상이 살아 있는가(=추종 가능한가). 좌표만 있는 항목은 false.</summary>
        public bool HasAnchor => anchor != null;

        /// <summary>트랜스폼으로 만든다. 현재 좌표를 대비책으로 함께 기록한다.</summary>
        public static VfxTarget Of(Transform t) =>
            new VfxTarget { anchor = t, fallbackPos = t != null ? t.position : Vector3.zero };

        /// <summary>좌표만으로 만든다.</summary>
        public static VfxTarget Of(Vector3 pos) =>
            new VfxTarget { anchor = null, fallbackPos = pos };

        public override string ToString() =>
            anchor != null ? $"{anchor.name}@{Position}" : $"(pos){fallbackPos}";
    }
}
