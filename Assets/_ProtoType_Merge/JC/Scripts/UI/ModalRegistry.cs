using System.Collections.Generic;
using UnityEngine;

public static class ModalRegistry
{
    private static readonly List<GameObject> _stack = new List<GameObject>();

    public static int Count => _stack.Count;
    public static bool HasAny => _stack.Count > 0;

    public static GameObject Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

    public static void Register(GameObject modal)
    {
        if (modal == null) return;
        _stack.Remove(modal);
        _stack.Add(modal);
    }

    public static void Unregister(GameObject modal)
    {
        if (modal == null) return;
        _stack.Remove(modal);
    }

    public static void CloseTop()
    {
        var top = Top;
        if (top != null) top.SetActive(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _stack.Clear();
    }
}
