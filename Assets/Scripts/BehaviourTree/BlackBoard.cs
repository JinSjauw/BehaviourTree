using System.Collections.Generic;
using UnityEngine;

public enum BlackBoardType 
{
    SELF = 0,
    SQUAD = 1,
}

public class BlackBoard : MonoBehaviour
{
    private Dictionary<BlackBoardFields, object> blackBoardValues = new Dictionary<BlackBoardFields, object>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
