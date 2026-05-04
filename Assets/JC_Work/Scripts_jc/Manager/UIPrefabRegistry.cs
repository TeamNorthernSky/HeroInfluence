using UnityEngine;

[DisallowMultipleComponent]
public class UIPrefabRegistry : MonoBehaviour
{
    [SerializeField] private GameObject sceneHistoryPanel;

    public GameObject SceneHistoryPanel => sceneHistoryPanel;
}
