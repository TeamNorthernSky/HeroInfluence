// Auto Generated. Do not modify.
// Template Signature: dict=0|reward_id:Int:Comma|money:Int:Comma|medal:Int:Comma|gem:Int:Comma|Rumina_IP:Int:Comma|Rumina_ATK:Int:Comma|Rumina_MaxHP:Int:Comma|Rumina_Heal:Int:Comma|Rumina_DEF:Int:Comma|Justice_IP:Int:Comma|Justice_ATK:Int:Comma|Justice_MaxHP:Int:Comma|Justice_Heal:Int:Comma|Justice_Def:Int:Comma|BlackBullet_IP:Int:Comma|BlackBullet_ATK:Int:Comma|BlackBullet_MaxHP:Int:Comma|BlackBullet_Heal:Int:Comma|BlackBullet_Def:Int:Comma|Nekoming_IP:Int:Comma|Nekoming_ATK:Int:Comma|Nekoming_MaxHP:Int:Comma|Nekoming_Heal:Int:Comma|Nekoming_Def:Int:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WorldEventRewardData
{
    public int reward_id;
    public int money;
    public int medal;
    public int gem;
    public int Rumina_IP;
    public int Rumina_ATK;
    public int Rumina_MaxHP;
    public int Rumina_Heal;
    public int Rumina_DEF;
    public int Justice_IP;
    public int Justice_ATK;
    public int Justice_MaxHP;
    public int Justice_Heal;
    public int Justice_Def;
    public int BlackBullet_IP;
    public int BlackBullet_ATK;
    public int BlackBullet_MaxHP;
    public int BlackBullet_Heal;
    public int BlackBullet_Def;
    public int Nekoming_IP;
    public int Nekoming_ATK;
    public int Nekoming_MaxHP;
    public int Nekoming_Heal;
    public int Nekoming_Def;
}

[CreateAssetMenu(fileName="WorldEventRewardDataTable", menuName="DataTable/WorldEventReward")]
public class WorldEventRewardDataTable : ScriptableObject
{
    public List<WorldEventRewardData> DataList = new List<WorldEventRewardData>();
}
