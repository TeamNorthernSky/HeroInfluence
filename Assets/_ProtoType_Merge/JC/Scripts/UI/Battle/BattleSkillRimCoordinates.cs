using UnityEngine;
using UnityEngine.UI;

/// <summary>Preserves the selection Image's normalized coordinates through all Canvas batching modes.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class BattleSkillRimCoordinates : BaseMeshEffect
{
    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureChannel();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnsureChannel();
        if (graphic != null) graphic.SetVerticesDirty();
    }

    private void EnsureChannel()
    {
        if (graphic != null && graphic.canvas != null)
            graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || graphic == null) return;
        EnsureChannel();
        var rect = graphic.GetPixelAdjustedRect();
        float width = Mathf.Max(.001f, rect.width);
        float height = Mathf.Max(.001f, rect.height);
        var vertex = default(UIVertex);
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            vertex.uv1 = new Vector4((vertex.position.x - rect.xMin) / width,
                (vertex.position.y - rect.yMin) / height, width / height, 1f);
            vertices.SetUIVertex(vertex, i);
        }
    }
}
