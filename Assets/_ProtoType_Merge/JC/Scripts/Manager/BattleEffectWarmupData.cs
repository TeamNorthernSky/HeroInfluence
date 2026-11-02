using System;
using UnityEngine;

// Build-time rendering recipes, derived from the existing presentation catalogs.
// No gameplay component is copied into the visual templates.
public sealed class BattleEffectWarmupData : ScriptableObject
{
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
