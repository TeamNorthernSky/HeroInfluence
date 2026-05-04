using UnityEngine;
using UnityEngine.Rendering;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("Map Settings")]
    [Tooltip("지도의 중심 좌표 설정")]
    public Vector2 mapCenter = Vector2.zero;
    [Tooltip("지도의 실제 크기 (World X, Z)")]
    public Vector2 mapSize = new Vector2(100f, 100f);
    [Tooltip("시야 렌더 타겟의 해상도")]
    public int resolution = 1024;
    
    [Header("Dependencies")]
    [Tooltip("원형 시야를 그릴 마스크 Material (FogOfWarMask.shader)")]
    public Material visionMaskMaterial; 
    [Tooltip("누적 메모리 블렌딩을 시킬 Material (FogOfWarAccumulate.shader)")]
    public Material accumulateMaterial; 
    
    private RenderTexture _fogCurrentRT;
    private RenderTexture _fogVisitedRT;
    public RenderTexture FogVisitedRT => _fogVisitedRT;
    private CommandBuffer _cb;
    private Mesh _quadMesh;

    private void Awake()
    {
        Instance = this;
        InitializeFog();
    }

    private void InitializeFog()
    {
        // 1. 실시간 시야와 누적 시야 RT 생성
        _fogCurrentRT = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RHalf);
        _fogCurrentRT.name = "_FogCurrentRT";
        
        _fogVisitedRT = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RHalf);
        _fogVisitedRT.name = "_FogVisitedRT";
        
        // 2. 누적 기억 RT 검은색(0, 안보임)으로 초기화 
        RenderTexture.active = _fogVisitedRT;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = null;

        _cb = new CommandBuffer { name = "FogOfWar_DrawVision" };
        _quadMesh = CreateQuad();
    }

    private void LateUpdate()
    {
        if (_cb == null) return;
        
        _cb.Clear();
        
        // 1. 매 프레임 Current 시야 RT 클리어 (현재 영역 초기화)
        _cb.SetRenderTarget(_fogCurrentRT);
        _cb.ClearRenderTarget(false, true, Color.black);
        
        // 2. GPU Projection 보정 제거, 순수 identity 사용 (NDC 직접 매핑)
        _cb.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        
        float mapMinX = mapCenter.x - mapSize.x * 0.5f;
        float mapMinZ = mapCenter.y - mapSize.y * 0.5f;

        // 3. 등록된 모든 시야 보유 유닛에 대해 Quad(마스크) 그리기
        foreach(var unit in FogOfWarUnit.AllUnits)
        {
            float r = unit.visionRadius;
            Vector3 pos = unit.transform.position;
            
            // 월드 XZ 좌표 → NDC (-1 ~ 1)로 직접 매핑
            float ndcX = (pos.x - mapMinX) / mapSize.x * 2f - 1f;
            float ndcY = (pos.z - mapMinZ) / mapSize.y * 2f - 1f;
            float ndcScaleX = (r * 2f) / mapSize.x * 2f;
            float ndcScaleY = (r * 2f) / mapSize.y * 2f;

            Matrix4x4 trs = Matrix4x4.TRS(
                new Vector3(ndcX, ndcY, 0f),
                Quaternion.identity,
                new Vector3(ndcScaleX, ndcScaleY, 1f)
            );
            _cb.DrawMesh(_quadMesh, trs, visionMaskMaterial);
        }
        
        // 4. Current 시야를 Visited 누적 RT에 Max Blend 기법으로 덮어씌움
        _cb.Blit(_fogCurrentRT, _fogVisitedRT, accumulateMaterial);
        
        // 커맨드 버퍼 실행 시점
        Graphics.ExecuteCommandBuffer(_cb);
        
        // 5. 후처리 셰이더용 Global 변수 전달
        Shader.SetGlobalTexture("_FogCurrentRT", _fogCurrentRT);
        Shader.SetGlobalTexture("_FogVisitedRT", _fogVisitedRT);
        
        Vector4 mapBounds = new Vector4(mapCenter.x - mapSize.x * 0.5f, mapCenter.y - mapSize.y * 0.5f, mapSize.x, mapSize.y);
        Shader.SetGlobalVector("_FogMapBounds", mapBounds);
    }

    private Mesh CreateQuad()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0)
        };
        mesh.uv = new Vector2[] {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        return mesh;
    }

    private void OnDestroy()
    {
        if (_fogCurrentRT) _fogCurrentRT.Release();
        if (_fogVisitedRT) _fogVisitedRT.Release();
        if (_cb != null) _cb.Release();
    }
}
