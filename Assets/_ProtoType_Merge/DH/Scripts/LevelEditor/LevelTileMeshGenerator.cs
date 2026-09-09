using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LevelTileMeshGenerator : MonoBehaviour
{
    private readonly struct TileBatchKey : System.IEquatable<TileBatchKey>
    {
        public readonly Vector2Int Chunk;
        public readonly Texture Texture;
        public readonly Material MaterialTemplate;
        public readonly LevelTileRenderMode RenderMode;

        public TileBatchKey(
            Vector2Int chunk,
            Texture texture,
            Material materialTemplate,
            LevelTileRenderMode renderMode)
        {
            Chunk = chunk;
            Texture = texture;
            MaterialTemplate = materialTemplate;
            RenderMode = renderMode;
        }

        public bool Equals(TileBatchKey other)
        {
            return Chunk == other.Chunk &&
                   Texture == other.Texture &&
                   MaterialTemplate == other.MaterialTemplate &&
                   RenderMode == other.RenderMode;
        }

        public override bool Equals(object obj)
        {
            return obj is TileBatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Chunk.GetHashCode();
                hash = (hash * 397) ^ (Texture != null ? Texture.GetHashCode() : 0);
                hash = (hash * 397) ^ (MaterialTemplate != null ? MaterialTemplate.GetHashCode() : 0);
                hash = (hash * 397) ^ (int)RenderMode;
                return hash;
            }
        }
    }

    private readonly struct MaterialCacheKey : System.IEquatable<MaterialCacheKey>
    {
        public readonly Texture Texture;
        public readonly Material Template;

        public MaterialCacheKey(Texture texture, Material template)
        {
            Texture = texture;
            Template = template;
        }

        public bool Equals(MaterialCacheKey other)
        {
            return Texture == other.Texture && Template == other.Template;
        }

        public override bool Equals(object obj)
        {
            return obj is MaterialCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Texture != null ? Texture.GetHashCode() : 0) * 397) ^
                       (Template != null ? Template.GetHashCode() : 0);
            }
        }
    }

    private sealed class TileBatch
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();
    }

    [Header("Tile Sources")]
    [SerializeField] private LevelTileRegistry tileRegistry;

    [Header("Grid")]
    [SerializeField] private GridManager gridManager;
    [SerializeField, Min(1)] private int chunkSize = 16;
    [SerializeField] private float yOffset = 0.01f;
    [SerializeField] private float loweredSpriteYOffset = -0.6f;
    [SerializeField] private float raisedCubeHeight = 0.1f;

    [Header("Obstacle Tiles")]
    [SerializeField] private string obstacleTileKey = "Obstacle";
    [SerializeField] private float obstacleYOffset = 0.025f;

    [Header("Output")]
    [SerializeField] private Transform tileRoot;
    [SerializeField] private Material materialTemplate;
    [SerializeField] private string generatedRootName = "Generated Tile Meshes";
    [SerializeField] private bool markChunksReflectionProbeStatic = true;

    private readonly Dictionary<MaterialCacheKey, Material> materialCache = new Dictionary<MaterialCacheKey, Material>();

    public void Generate(LevelData levelData)
    {
        if (levelData == null || tileRegistry == null)
            return;

        ClearTileMeshes();
        Generate(levelData, Vector2Int.zero, false);
    }

    public void Generate(LevelData levelData, Vector2Int offset, bool clearBeforeGenerate)
    {
        if (levelData == null || tileRegistry == null)
            return;

        if (clearBeforeGenerate)
            ClearTileMeshes();

        ResolveReferences();

        IReadOnlyList<TilePlacementData> placements = levelData.GroundTilePlacements;
        if (placements == null || placements.Count == 0)
            return;

        Dictionary<TileBatchKey, TileBatch> batches = new Dictionary<TileBatchKey, TileBatch>();
        for (int i = 0; i < placements.Count; i++)
        {
            TilePlacementData placement = placements[i];
            if (!tileRegistry.TryGetSprite(placement.TileKey, out Sprite sprite) ||
                sprite == null ||
                sprite.texture == null)
            {
                continue;
            }

            Vector2Int grid = placement.GridPosition + offset;
            Vector2Int chunk = new Vector2Int(
                Mathf.FloorToInt((float)grid.x / chunkSize),
                Mathf.FloorToInt((float)grid.y / chunkSize));
            TileBatchKey key = new TileBatchKey(
                chunk,
                sprite.texture,
                ResolveMaterialTemplate(placement.MaterialKey),
                ResolveBatchRenderMode(placement.RenderMode));

            if (!batches.TryGetValue(key, out TileBatch batch))
            {
                batch = new TileBatch();
                batches.Add(key, batch);
            }

            if (placement.RenderMode == LevelTileRenderMode.FlatCube)
                AddTileCube(batch, grid, sprite, yOffset, loweredSpriteYOffset);
            else if (placement.RenderMode == LevelTileRenderMode.RaisedCube)
            {
                AddTileCube(batch, grid, sprite, raisedCubeHeight, loweredSpriteYOffset);
                gridManager?.RegisterCellSurfaceOffset(grid, raisedCubeHeight);
            }
            else
                AddTileQuad(batch, grid, sprite, ResolveTileYOffset(placement.RenderMode));
        }

        foreach (KeyValuePair<TileBatchKey, TileBatch> pair in batches)
            CreateBatchObject(pair.Key, pair.Value);
    }

    public void GenerateObstacleTiles(
        LevelData levelData,
        Vector2Int offset,
        Transform obstacleRoot,
        bool clearBeforeGenerate)
    {
        if (levelData == null || tileRegistry == null || obstacleRoot == null)
            return;

        if (string.IsNullOrWhiteSpace(obstacleTileKey))
            return;

        if (clearBeforeGenerate)
            ClearChildren(obstacleRoot);

        ResolveReferences();

        if (!tileRegistry.TryGetSprite(obstacleTileKey, out Sprite sprite) ||
            sprite == null ||
            sprite.texture == null)
        {
            return;
        }

        IReadOnlyList<Vector2Int> obstacleCells = levelData.ObstacleCells;
        if (obstacleCells == null || obstacleCells.Count == 0)
            return;

        Dictionary<TileBatchKey, TileBatch> batches = new Dictionary<TileBatchKey, TileBatch>();
        for (int i = 0; i < obstacleCells.Count; i++)
        {
            Vector2Int grid = obstacleCells[i] + offset;
            Vector2Int chunk = new Vector2Int(
                Mathf.FloorToInt((float)grid.x / chunkSize),
                Mathf.FloorToInt((float)grid.y / chunkSize));
            TileBatchKey key = new TileBatchKey(
                chunk,
                sprite.texture,
                materialTemplate,
                LevelTileRenderMode.FlatSprite);

            if (!batches.TryGetValue(key, out TileBatch batch))
            {
                batch = new TileBatch();
                batches.Add(key, batch);
            }

            AddTileQuad(batch, grid, sprite, obstacleYOffset);
        }

        foreach (KeyValuePair<TileBatchKey, TileBatch> pair in batches)
            CreateBatchObject(pair.Key, pair.Value, obstacleRoot, "ObstacleTileChunk");
    }

    public void ClearTileMeshes()
    {
        ResolveTileRoot();
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (gridManager != null)
            gridManager.ClearCellSurfaceOffsets();

        if (tileRoot == null)
            return;

        for (int i = tileRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = tileRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        foreach (Material material in materialCache.Values)
        {
            if (material == null)
                continue;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        materialCache.Clear();
    }

    private void ResolveReferences()
    {
        ResolveTileRoot();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void ResolveTileRoot()
    {
        if (tileRoot != null)
            return;

        Transform existing = transform.Find(generatedRootName);
        if (existing != null)
        {
            tileRoot = existing;
            return;
        }

        GameObject root = new GameObject(generatedRootName);
        tileRoot = root.transform;
        tileRoot.SetParent(transform, false);
    }

    private void AddTileQuad(TileBatch batch, Vector2Int grid, Sprite sprite, float tileYOffset)
    {
        float size = gridManager != null ? Mathf.Max(0.01f, gridManager.CellSize) : 1f;
        float half = size * 0.5f;
        Vector3 center = gridManager != null
            ? gridManager.GridToWorldCenter(grid)
            : new Vector3(grid.x * size, 0f, grid.y * size);
        center.y = gridManager != null ? gridManager.GetLandSurfaceY() + tileYOffset : tileYOffset;

        int start = batch.Vertices.Count;
        batch.Vertices.Add(new Vector3(center.x - half, center.y, center.z - half));
        batch.Vertices.Add(new Vector3(center.x + half, center.y, center.z - half));
        batch.Vertices.Add(new Vector3(center.x + half, center.y, center.z + half));
        batch.Vertices.Add(new Vector3(center.x - half, center.y, center.z + half));

        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;
        float xMin = rect.xMin / texture.width;
        float xMax = rect.xMax / texture.width;
        float yMin = rect.yMin / texture.height;
        float yMax = rect.yMax / texture.height;

        batch.Uvs.Add(new Vector2(xMin, yMin));
        batch.Uvs.Add(new Vector2(xMax, yMin));
        batch.Uvs.Add(new Vector2(xMax, yMax));
        batch.Uvs.Add(new Vector2(xMin, yMax));

        batch.Triangles.Add(start);
        batch.Triangles.Add(start + 2);
        batch.Triangles.Add(start + 1);
        batch.Triangles.Add(start);
        batch.Triangles.Add(start + 3);
        batch.Triangles.Add(start + 2);
    }

    private void AddTileCube(
        TileBatch batch,
        Vector2Int grid,
        Sprite sprite,
        float topYOffset,
        float bottomYOffset)
    {
        float size = gridManager != null ? Mathf.Max(0.01f, gridManager.CellSize) : 1f;
        float half = size * 0.5f;
        Vector3 center = gridManager != null
            ? gridManager.GridToWorldCenter(grid)
            : new Vector3(grid.x * size, 0f, grid.y * size);

        float surfaceY = gridManager != null ? gridManager.GetLandSurfaceY() : 0f;
        float topY = surfaceY + topYOffset;
        float bottomY = surfaceY + bottomYOffset;

        float left = center.x - half;
        float right = center.x + half;
        float back = center.z - half;
        float forward = center.z + half;

        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;
        Vector2 uvMin = new Vector2(rect.xMin / texture.width, rect.yMin / texture.height);
        Vector2 uvMax = new Vector2(rect.xMax / texture.width, rect.yMax / texture.height);

        AddQuad(
            batch,
            new Vector3(left, topY, back),
            new Vector3(right, topY, back),
            new Vector3(right, topY, forward),
            new Vector3(left, topY, forward),
            uvMin,
            uvMax);

        AddQuad(
            batch,
            new Vector3(left, bottomY, forward),
            new Vector3(left, topY, forward),
            new Vector3(right, topY, forward),
            new Vector3(right, bottomY, forward),
            uvMin,
            uvMax);

        AddQuad(
            batch,
            new Vector3(right, bottomY, back),
            new Vector3(right, topY, back),
            new Vector3(left, topY, back),
            new Vector3(left, bottomY, back),
            uvMin,
            uvMax);

        AddQuad(
            batch,
            new Vector3(left, bottomY, back),
            new Vector3(left, topY, back),
            new Vector3(left, topY, forward),
            new Vector3(left, bottomY, forward),
            uvMin,
            uvMax);

        AddQuad(
            batch,
            new Vector3(right, bottomY, forward),
            new Vector3(right, topY, forward),
            new Vector3(right, topY, back),
            new Vector3(right, bottomY, back),
            uvMin,
            uvMax);
    }

    private void AddQuad(
        TileBatch batch,
        Vector3 first,
        Vector3 second,
        Vector3 third,
        Vector3 fourth,
        Vector2 uvMin,
        Vector2 uvMax)
    {
        int start = batch.Vertices.Count;
        batch.Vertices.Add(first);
        batch.Vertices.Add(second);
        batch.Vertices.Add(third);
        batch.Vertices.Add(fourth);

        batch.Uvs.Add(new Vector2(uvMin.x, uvMin.y));
        batch.Uvs.Add(new Vector2(uvMax.x, uvMin.y));
        batch.Uvs.Add(new Vector2(uvMax.x, uvMax.y));
        batch.Uvs.Add(new Vector2(uvMin.x, uvMax.y));

        batch.Triangles.Add(start);
        batch.Triangles.Add(start + 2);
        batch.Triangles.Add(start + 1);
        batch.Triangles.Add(start);
        batch.Triangles.Add(start + 3);
        batch.Triangles.Add(start + 2);
    }

    private float ResolveTileYOffset(LevelTileRenderMode renderMode)
    {
        return renderMode == LevelTileRenderMode.LoweredSprite
            ? loweredSpriteYOffset
            : yOffset;
    }

    private static LevelTileRenderMode ResolveBatchRenderMode(LevelTileRenderMode renderMode)
    {
        if (renderMode == LevelTileRenderMode.FlatCube || renderMode == LevelTileRenderMode.RaisedCube)
            return renderMode;

        return LevelTileRenderMode.FlatSprite;
    }

    private void CreateBatchObject(TileBatchKey key, TileBatch batch)
    {
        CreateBatchObject(key, batch, tileRoot, "TileChunk");
    }

    private void CreateBatchObject(TileBatchKey key, TileBatch batch, Transform parent, string namePrefix)
    {
        if (batch == null || batch.Vertices.Count == 0)
            return;

        string textureName = key.Texture != null ? key.Texture.name : "NoTexture";
        string modeName = key.RenderMode == LevelTileRenderMode.FlatSprite ? string.Empty : $"_{key.RenderMode}";
        GameObject go = new GameObject($"{namePrefix}_{key.Chunk.x}_{key.Chunk.y}_{textureName}{modeName}");
        go.transform.SetParent(parent, false);
        if (key.RenderMode == LevelTileRenderMode.RaisedCube && gridManager != null && gridManager.LandTransform != null)
            go.layer = gridManager.LandTransform.gameObject.layer;

        ApplyStaticFlags(go);

        Mesh mesh = new Mesh
        {
            name = go.name
        };
        if (batch.Vertices.Count > 65000)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(batch.Vertices);
        mesh.SetUVs(0, batch.Uvs);
        mesh.SetTriangles(batch.Triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetMaterialForTexture(key.Texture, key.MaterialTemplate);

        if (key.RenderMode == LevelTileRenderMode.RaisedCube)
        {
            MeshCollider meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
        }
    }

    private void ApplyStaticFlags(GameObject target)
    {
        if (target == null || !markChunksReflectionProbeStatic)
            return;

#if UNITY_EDITOR
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(target);
        flags |= StaticEditorFlags.ReflectionProbeStatic;
        GameObjectUtility.SetStaticEditorFlags(target, flags);
#endif
    }

    private void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private Material ResolveMaterialTemplate(string materialKey)
    {
        if (!string.IsNullOrWhiteSpace(materialKey) &&
            tileRegistry != null &&
            tileRegistry.TryGetMaterialTemplate(materialKey, out Material selectedMaterial) &&
            selectedMaterial != null)
        {
            return selectedMaterial;
        }

        return materialTemplate;
    }

    private Material GetMaterialForTexture(Texture texture, Material template)
    {
        if (texture == null)
            return null;

        var cacheKey = new MaterialCacheKey(texture, template);
        if (materialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
            return cached;

        Material material = template != null
            ? new Material(template)
            : new Material(ResolveDefaultShader());
        material.name = $"Tile_{texture.name}";
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        materialCache.Add(cacheKey, material);
        return material;
    }

    private static Shader ResolveDefaultShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
            return shader;

        shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
            return shader;

        shader = Shader.Find("Unlit/Texture");
        if (shader != null)
            return shader;

        shader = Shader.Find("Sprites/Default");
        if (shader != null)
            return shader;

        return Shader.Find("Standard");
    }
}
