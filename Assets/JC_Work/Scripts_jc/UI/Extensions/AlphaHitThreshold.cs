using UnityEngine;
using UnityEngine.UI;

namespace Orora.UI.Extensions
{
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    [AddComponentMenu("Orora/UI Extensions/Alpha Hit Threshold")]
    public class AlphaHitThreshold : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("이 값 이상의 알파를 가진 픽셀만 클릭(raycast) 감지됨. 0이면 사각형 전체 감지.")]
        public float threshold = 0.5f;

        void Awake() { Apply(); }
        void OnEnable() { Apply(); }
        void OnValidate() { Apply(); }

        public void Apply()
        {
            var img = GetComponent<Image>();
            if (img == null) return;
            if (img.sprite == null || img.sprite.texture == null) return;
            if (!img.sprite.texture.isReadable) return;
            img.alphaHitTestMinimumThreshold = threshold;
        }
    }
}
