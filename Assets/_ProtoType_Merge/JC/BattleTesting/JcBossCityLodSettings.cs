using System;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-17 | 표시 전환과 생성 강도를 같은 저장값으로 관리하며 편집 초안은 별도로 복제합니다.
    [Serializable]
    public sealed class JcBossCityLodSettings
    {
        [Range(.02f,.95f)] public float highScreenHeight=.35f;
        [Range(.001f,.94f)] public float mediumScreenHeight=.16f;
        [Range(0,.1f)] public float mediumSimplification=.0125f;
        [Range(0,.1f)] public float lowSimplification=.03f;
        public JcBossCityLodSettings Copy() => JsonUtility.FromJson<JcBossCityLodSettings>(JsonUtility.ToJson(this));
        public void Validate()
        {
            highScreenHeight=Mathf.Clamp(highScreenHeight,.02f,.95f);
            mediumScreenHeight=Mathf.Clamp(mediumScreenHeight,.001f,highScreenHeight-.001f);
            mediumSimplification=Mathf.Clamp(mediumSimplification,0,.1f);
            lowSimplification=Mathf.Clamp(lowSimplification,mediumSimplification,.1f);
        }
    }
}
