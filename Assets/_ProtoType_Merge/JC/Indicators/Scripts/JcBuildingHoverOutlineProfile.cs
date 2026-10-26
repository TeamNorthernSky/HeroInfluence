using System;
using UnityEngine;

namespace JC.Indicators
{
    [Serializable]
    public struct JcBuildingHoverOutlineValues
    {
        public Color outlineColor;
        public float outlineIntensity;
        public float highlightSpeed;
        public float highlightIntensity;
        public int shapeVersion;
        public float highlightRange;
        public float highlightSmoothing;
        public float whiteOutlineWidth;
        public float edgeSmoothing;
    }

    public sealed class JcBuildingHoverOutlineProfile : ScriptableObject
    {
        public JcBuildingHoverOutlineValues values;
    }
}
