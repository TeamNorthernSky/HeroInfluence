using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Timeline 안의 논리 구간 경계. 구간은 애니메이션 순서를 실행하지 않고, 실제 Timeline 시간 범위를
/// 이름으로 찾기 위한 메타데이터만 제공한다.
/// </summary>
public enum PresentationSectionBoundary
{
    Start,
    End
}

public sealed class PresentationSectionMarker : Marker
{
    [SerializeField] private string _sectionId = "Attack";
    [SerializeField] private PresentationSectionBoundary _boundary;

    public string SectionId => _sectionId != null ? _sectionId.Trim() : string.Empty;
    public PresentationSectionBoundary Boundary => _boundary;

    public void Configure(string sectionId, PresentationSectionBoundary boundary)
    {
        _sectionId = sectionId != null ? sectionId.Trim() : string.Empty;
        _boundary = boundary;
    }
}
