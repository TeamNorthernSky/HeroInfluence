using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 260628] HQLobby UI 구성 매니페스트. 컴포저가 prefabs를 순서대로 Instantiate한다.
/// 기능 추가/제거 = 이 .asset 편집(씬 무변경). 레이어 배정은 각 프리팹의 LobbyUIModulePlacer가 처리.
/// </summary>
[CreateAssetMenu(fileName = "LobbyUILayout", menuName = "HI/UI/Lobby UI Layout")]
public class LobbyUILayout : ScriptableObject
{
    [Tooltip("스폰할 번들 프리팹(순서 = Instantiate 순서).")]
    public List<GameObject> prefabs = new List<GameObject>();
}
