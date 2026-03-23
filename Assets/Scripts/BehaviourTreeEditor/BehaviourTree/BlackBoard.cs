using System.Collections.Generic;
using UnityEngine;

namespace BTreeEditor 
{
    public enum BlackBoardType
    {
        SELF = 0,
        SQUAD = 1,
    }

    public abstract class BlackBoard : MonoBehaviour
    {
        protected Dictionary<BlackBoardFields, object> blackBoardValues = new Dictionary<BlackBoardFields, object>();

        public abstract void RegisterFields();

        //Must think of ways to validate output and input.

        public object GetField(BlackBoardFields fieldType)
        {
            if (!blackBoardValues.ContainsKey(fieldType)) return null;

            return blackBoardValues[fieldType];
        }

        public void UpdateField(BlackBoardFields fieldType, object value)
        {
            if (!blackBoardValues.ContainsKey(fieldType)) return;

            blackBoardValues[fieldType] = value;
        }
    }

}
