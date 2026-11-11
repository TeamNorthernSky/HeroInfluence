using System;
using UnityEngine;

// JC | 2026-11-11 | 기존 연출 카탈로그에서 만든 Play·빌드 공통 시각 준비 데이터이며 게임 스크립트는 복제하지 않습니다.
public sealed class BattleEffectWarmupData : ScriptableObject
{
    // JC | 2026-11-11 | 원본이 변경된 에디터에서 오래된 시각 템플릿을 재사용하지 않기 위한 생성 입력 서명입니다.
    [HideInInspector] public string SourceFingerprint;

    [Serializable]
    public sealed class Recipe
    {
        public GameObject Source;
        public GameObject Visual;
        public GameObject[] Dependencies;
    }

    public SkillPresentationCatalog[] Catalogs;
    public EffectRegistry[] Registries;
    public GameObject[] CommonEffects;
    public Recipe[] Recipes;
}
