using System;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>Presentation 마커를 용도별 레인에 배치하기 위한 트랙.</summary>
/// <remarks>역할은 표시 이름과 분리되어 있어 트랙 이름을 바꿔도 유지된다.</remarks>
public enum PresentationMarkerTrackRole
{
    Unassigned = 0,
    SectionHold = 1,
    Speed = 2,
    GameplayEvents = 3,
    Sfx = 4
}

[Serializable]
[TrackBindingType(typeof(PresentationSignalReceiver))]
public sealed class PresentationMarkerTrack : MarkerTrack
{
    [SerializeField] private PresentationMarkerTrackRole role;

    public PresentationMarkerTrackRole Role => role;

    public void SetRole(PresentationMarkerTrackRole value)
    {
        role = value;
    }
}
