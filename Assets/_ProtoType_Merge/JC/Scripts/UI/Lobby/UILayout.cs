using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 260628 / 일반화 260630] 씬-무관 UI 구성 매니페스트. 컴포저가 prefabs를 순서대로 Instantiate.
/// 기능 추가/제거 = 이 .asset 편집(씬 무변경). 레이어 배정은 각 프리팹 UIModulePlacer가 처리.
/// </summary>
[CreateAssetMenu(fileName = "UILayout", menuName = "HI/UI/UI Layout")]
public class UILayout : ScriptableObject
{
    [Tooltip("스폰할 번들 프리팹(순서 = Instantiate 순서).")]
    public List<GameObject> prefabs = new List<GameObject>();
}
