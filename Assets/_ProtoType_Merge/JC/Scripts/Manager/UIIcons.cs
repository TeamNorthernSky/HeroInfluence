using UnityEngine;

/// <summary>
/// [JC 260625] <see cref="UIIconLibrary"/> 접근용 얇은 static facade. UI는 이 클래스만 호출한다.
/// SO 에셋(Resources/UIIconLibrary)이 있으면 로드, 없으면 기본 인스턴스로 폴백(경로 규약으로 동작).
///
/// 호출부는 Resources 경로·폴백·캐시를 알 필요 없이 enum 키만 넘긴다.
/// 미래에 아이콘 소유권이 다른 객체로 이전되어도 본 facade 구현만 바꾸면 된다(호출부 무변경).
/// </summary>
public static class UIIcons
{
    // Assets/**/Resources/UIIconLibrary.asset (폴더명이 Resources이면 위치 무관)
    private const string ResourcePath = "UIIconLibrary";

    private static UIIconLibrary _lib;
    private static UIIconLibrary _fallback;

    public static UIIconLibrary Library
    {
        get
        {
            if (_lib != null) return _lib;
            _lib = Resources.Load<UIIconLibrary>(ResourcePath);
            if (_lib != null) return _lib;
            if (_fallback == null) _fallback = ScriptableObject.CreateInstance<UIIconLibrary>();
            return _fallback;
        }
    }

    /// <summary>자원 아이콘(키=ResourceType).</summary>
    public static Sprite Resource(ResourceType type) => Library.GetResourceIcon(type);

    /// <summary>스테이터스 아이콘(키=UIStatusIconType).</summary>
    public static Sprite Status(UIStatusIconType type) => Library.GetStatusIcon(type);

    /// <summary>특수 건물(거점) 아이콘(키=UIBuildingIconType).</summary>
    public static Sprite Building(UIBuildingIconType type) => Library.GetBuildingIcon(type);
}
