using UnityEngine;

namespace BTreeEditor 
{
    public class BlackBoardTest : BlackBoard
    {
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private int health = 100;
        [SerializeField] private float time = 0;

        public override void RegisterFields()
        {
            blackBoardValues[BlackBoardFields.TARGET_POSITION_VECTOR3] = targetPosition;
            blackBoardValues[BlackBoardFields.HEALTH_INT] = health;
            blackBoardValues[BlackBoardFields.TEST_TIME] = time;
        }
    }

}

