using UnityEngine;

/// <summary>
/// string 필드를 base Animator Controller의 state 이름 드롭다운으로 표시합니다(에디터 전용 UX).
/// 저장값은 그대로 state 이름 문자열이라 런타임 로직에는 영향이 없습니다.
/// </summary>
public class AnimatorStateDropdownAttribute : PropertyAttribute
{
    /// <summary>state 목록을 읽어올 base AnimatorController 에셋 이름.</summary>
    public readonly string ControllerName;

    public AnimatorStateDropdownAttribute(string controllerName = "Base_Ani_Controller")
    {
        ControllerName = controllerName;
    }
}
