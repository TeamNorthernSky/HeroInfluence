using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    public sealed class JcBossBackgroundMaterials : MonoBehaviour
    {
        public Material GlassProfile { get; private set; }
        public Material GlassDraft { get; private set; }
        readonly List<Renderer> panes = new List<Renderer>();

        public void Initialize(GameObject stage, Material profile)
        {
            GlassProfile = profile;
            GlassDraft = new Material(profile) { hideFlags = HideFlags.DontSave, name = "보스전 유리 · 조정 중" };
            foreach (var pane in stage.GetComponentsInChildren<Renderer>(true))
            {
                if (pane.sharedMaterial != profile) continue;
                panes.Add(pane);
                pane.sharedMaterial = GlassDraft;
            }
        }

        void OnDestroy()
        {
            foreach (var pane in panes)
                if (pane && pane.sharedMaterial == GlassDraft) pane.sharedMaterial = GlassProfile;
            if (GlassDraft) Destroy(GlassDraft);
        }
    }
}
