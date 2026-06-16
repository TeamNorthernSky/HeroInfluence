using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleSkill : ToggleButton
{
    // Start is called before the first frame update
    void Start()
    {

    }

    void Update()
    {

    }

    protected override void OnClick()
    {
        if (IsOn) return;
        base.OnClick();
    }
}
