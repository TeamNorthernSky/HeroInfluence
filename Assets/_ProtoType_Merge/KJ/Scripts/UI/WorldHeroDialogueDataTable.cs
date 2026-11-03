using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldHeroDialogueData
{
    public int hero_id;
    [TextArea] public string out_of_action_range_dialogue;
    [TextArea] public string funds_acquired_dialogue;
    [TextArea] public string crystal_acquired_dialogue;
    [TextArea] public string medal_acquired_dialogue;
    [TextArea] public string materials_acquired_dialogue;
    [TextArea] public string gate_opened_dialogue;
    [TextArea] public string idle_5_seconds_dialogue;
    [TextArea] public string base_arrival_dialogue;

    public string GetDialogue(int column) => column switch
    {
        1 => out_of_action_range_dialogue,
        2 => funds_acquired_dialogue,
        3 => crystal_acquired_dialogue,
        4 => medal_acquired_dialogue,
        5 => materials_acquired_dialogue,
        6 => gate_opened_dialogue,
        7 => idle_5_seconds_dialogue,
        8 => base_arrival_dialogue,
        _ => null
    };
}

[CreateAssetMenu(fileName = "WorldHeroDialogueDataTable", menuName = "DataTable/WorldHeroDialogue")]
public class WorldHeroDialogueDataTable : ScriptableObject
{
    public List<WorldHeroDialogueData> DataList = new List<WorldHeroDialogueData>();
}
