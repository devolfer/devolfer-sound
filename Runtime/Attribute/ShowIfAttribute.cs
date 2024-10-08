using UnityEngine;

namespace Devolfer.Sound
{
    public class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionFieldName { get; }

        public ShowIfAttribute(string conditionFieldName) => ConditionFieldName = conditionFieldName;
    }
}