using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Marks the Timeline interval that traverses SkillPresentationData.MovingAttack's
/// formation-relative Entry/Mid/Exit curve. The path stays in the presentation data
/// so Scene view edits and runtime playback use the same points.
/// </summary>
public sealed class PresentationMovingAttackMarker : Marker
{
    [SerializeField] private PresentationSectionBoundary _boundary = PresentationSectionBoundary.Start;

    public PresentationSectionBoundary Boundary => _boundary;

    public void Configure(PresentationSectionBoundary boundary) => _boundary = boundary;

}
