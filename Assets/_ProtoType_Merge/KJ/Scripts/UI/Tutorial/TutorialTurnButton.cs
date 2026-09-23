using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class TutorialTurnButton : MonoBehaviour
{
    private Button nextTurnButton;
    public TutorialTurnManager turnmanager;
    void Start()
    {
        nextTurnButton = GetComponent<Button>();
        nextTurnButton.onClick.AddListener(GoToNextTurn);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void GoToNextTurn()
    {
        turnmanager.EndTutorialTurn();
    }

    
}
