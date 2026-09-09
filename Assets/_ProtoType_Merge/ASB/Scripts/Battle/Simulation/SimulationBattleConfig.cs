using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play 이전(에디트 모드)에 작성하는 모의 전투 프리셋.
/// 순수 설정 데이터만 담으며, 런타임에 원본을 수정하지 않는다.
/// 실제 유닛/무기 생성은 하지 않는다 — SimulationUnitFactory가 transient 데이터로 처리한다.
/// </summary>
[CreateAssetMenu(fileName = "SimBattleConfig", menuName = "Simulation/Battle Config", order = 0)]
public sealed class SimulationBattleConfig : ScriptableObject
{
    [Tooltip("아군 슬롯. 위에서 아래 순서가 전투 배치 순서다. 활성화된 아군 1~6명.")]
    public List<SimulationAllyInput> allies = new List<SimulationAllyInput>();

    [Tooltip("적 그룹 키(EnemyGroupData.EnemyIndex). 커스텀 인스펙터 드롭다운으로 선택한다.")]
    public string enemyGroupKey = string.Empty;

    [Min(1)]
    [Tooltip("적 레벨.")]
    public int enemyLevel = 1;
}
