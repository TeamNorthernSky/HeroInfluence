/// <summary>
/// [JC 신설 260706] fog 렌더 패스의 런타임 게이트.
/// 직렬화 필드 없이 static 상태만 가진다 — 에디터 플레이 중 토글해도
/// 커밋된 렌더러 .asset에 흔적이 남지 않게 하기 위한 설계.
/// DHFogOfWarFeature.AddRenderPasses가 ShouldRender를 조회한다.
/// </summary>
public static class FogRenderGate
{
    /// <summary>유저 토글. Shift+F 치트와 RenderFXManager 인스펙터가 조작.</summary>
    public static bool UserEnabled = true;

    /// <summary>현재 씬에 FogRenderManager가 존재하는가. RenderFXManager가 씬 로드/언로드마다 갱신.</summary>
    public static bool SceneHasFog = false;

    public static bool ShouldRender => UserEnabled && SceneHasFog;
}
