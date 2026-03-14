using UnityEngine;

public class BlackBoardTest : BlackBoard
{
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private int health = 100;

    public override void RegisterFields()
    {
        blackBoardValues[BlackBoardFields.TARGET_POSITION_VECTOR3] = targetPosition;
        blackBoardValues[BlackBoardFields.HEALTH_INT] = health;
    }
}
