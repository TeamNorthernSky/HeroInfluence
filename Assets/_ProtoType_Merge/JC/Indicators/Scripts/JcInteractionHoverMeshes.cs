using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Indicators
{
    [DisallowMultipleComponent]
    public sealed class JcInteractionHoverMeshes : MonoBehaviour
    {
        private Renderer[] meshes;
        private Component group;
        private static readonly List<JcInteractionHoverMeshes> groups = new List<JcInteractionHoverMeshes>();

        private void OnEnable()
        {
            group = (Component)GetComponent<SubEventObject>() ?? GetComponent<WorldEventObject>()
                ?? (Component)GetComponent<MapEventObject>();
            if (group != null) groups.Add(this);
        }

        // 일반 이벤트는 각 모델의 범위와 모델 사이 중앙을 모두 같은 이벤트로 판정합니다.
        public static Component PickGroupCenter(Scene scene, Ray ray, float maxDistance, out float nearest)
        {
            nearest = maxDistance;
            Component target = null;
            foreach (var entry in groups)
            {
                if (entry.gameObject.scene != scene || !(entry.group is Behaviour owner) || !owner.isActiveAndEnabled)
                    continue;
                float distance = entry.GetHoverDistance(ray);
                if (distance >= nearest) continue;
                nearest = distance;
                target = owner;
            }
            return target;
        }

        private float GetHoverDistance(Ray ray)
        {
            Bounds combined = default;
            float nearest = float.PositiveInfinity;
            int count = 0;
            if (meshes == null) meshes = GetComponentsInChildren<Renderer>(true);
            foreach (var mesh in meshes)
            {
                if (!(mesh is MeshRenderer || mesh is SkinnedMeshRenderer) || !mesh.enabled ||
                    !mesh.gameObject.activeInHierarchy || mesh.forceRenderingOff) continue;
                var bounds = mesh.bounds;
                if (bounds.IntersectRay(ray, out float memberDistance))
                    nearest = Mathf.Min(nearest, memberDistance);
                if (count++ == 0) combined = bounds;
                else combined.Encapsulate(bounds);
            }
            if (count > 1)
            {
                combined.size = Vector3.Scale(combined.size, new Vector3(.7f, .8f, .7f));
                if (combined.IntersectRay(ray, out float centerDistance))
                    nearest = Mathf.Min(nearest, centerDistance);
            }
            return nearest;
        }

        public static Renderer[] Prepare(Transform root)
        {
            var cache = root.GetComponent<JcInteractionHoverMeshes>() ?? root.gameObject.AddComponent<JcInteractionHoverMeshes>();
            return cache.meshes ?? (cache.meshes = root.GetComponentsInChildren<Renderer>(true));
        }

        public static void Invalidate(Transform root)
        {
            if (root.TryGetComponent<JcInteractionHoverMeshes>(out var cache)) cache.meshes = null;
        }

        private void OnTransformChildrenChanged() => meshes = null;
        private void OnDisable()
        {
            groups.Remove(this);
            meshes = null;
        }
    }
}
