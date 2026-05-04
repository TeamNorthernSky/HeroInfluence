using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// UI 버튼 입력을 원본 Assembly-CSharp의 InputHandler.BeginPendingAction으로 릴레이.
/// 씬 컴포넌트의 m_Script GUID가 원본을 가리키고 있어 dup 타입으로는 참조/호출이 불가하므로 리플렉션을 경유한다.
/// 원본 시그니처가 변경되면 Awake에서 null 경고가 뜨도록 설계 — 즉시 감지해 이 파일을 동기화한다.
/// enum 정수 값 변경은 정적으로 감지되지 않으므로 원본 PendingActionType 순서/값 변동 시 수동 확인 필요.
/// </summary>
[DisallowMultipleComponent]
public class BattleInputRouter : MonoBehaviour
{
    [SerializeField] private GameObject inputHandlerObject;

    private Component originalInputHandler;
    private MethodInfo beginPendingActionMethod;
    private Type originalEnumType;

    private void Awake()
    {
        Type originalType = Type.GetType("InputHandler, Assembly-CSharp");
        originalEnumType = Type.GetType("PendingActionType, Assembly-CSharp");
        if (originalType == null || originalEnumType == null)
        {
            Debug.LogError("[BattleInputRouter] 원본 Assembly-CSharp의 InputHandler/PendingActionType 타입을 찾지 못했습니다.");
            return;
        }

        originalInputHandler = inputHandlerObject != null
            ? inputHandlerObject.GetComponent(originalType)
            : null;
        beginPendingActionMethod = originalType.GetMethod(
            "BeginPendingAction",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (originalInputHandler == null)
        {
            Debug.LogError("[BattleInputRouter] inputHandlerObject에 원본 InputHandler 컴포넌트가 없거나 할당되지 않았습니다.");
        }
        if (beginPendingActionMethod == null)
        {
            Debug.LogError("[BattleInputRouter] InputHandler.BeginPendingAction(PendingActionType) 메서드를 찾지 못했습니다. 원본 시그니처 변경 여부를 확인하세요.");
        }
    }

    public void PressAction(PendingActionType actionType)
    {
        if (originalInputHandler == null || beginPendingActionMethod == null || originalEnumType == null)
        {
            Debug.LogWarning("[BattleInputRouter] 초기화 미완료 상태에서 PressAction 호출. Awake 로그를 확인하세요.");
            return;
        }
        object originalEnumValue = Enum.ToObject(originalEnumType, (int)actionType);
        beginPendingActionMethod.Invoke(originalInputHandler, new object[] { originalEnumValue });
    }
}
