using System;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-17 | 편집용 원본과 분리한 공용 LOD 사본입니다. 도시 복사본은 같은 메시 에셋을 참조합니다.
    public sealed class JcBossCityLodLibrary : ScriptableObject
    {
        [Serializable] public sealed class Part { public Mesh high, medium, low; }
        [Serializable] public sealed class Entry { public Mesh source; public Part[] parts; }
        public Entry[] entries = Array.Empty<Entry>();
        // JC | 2026-11-17 | 실제 생성 메시의 강도를 기록해 값만 저장되고 메시가 예전 상태로 남는 것을 막습니다.
        public float bakedMediumSimplification=.0125f, bakedLowSimplification=.03f;
    }
}
