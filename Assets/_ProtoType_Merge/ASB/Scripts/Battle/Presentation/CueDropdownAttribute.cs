using UnityEngine;

/// <summary>
/// string 필드를 Cue 어휘(PresentationCues.Presets) 드롭다운 + 커스텀 입력으로 표시(에디터 전용 UX).
/// 저장값은 문자열 그대로. 런타임에서 정규화(trim+소문자)해 매칭한다.
/// </summary>
public class CueDropdownAttribute : PropertyAttribute
{
}
