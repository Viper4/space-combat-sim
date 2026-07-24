using UnityEngine;
using System;

//Original version of the ConditionalHideAttribute created by Brecht Lecluyse (www.brechtos.com)
//Modified by: Sebastian Lague

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
public class ConditionalHideAttribute : PropertyAttribute
{
    public string conditionalSourceField;
    public int enumIndex;
    public bool not;

    public ConditionalHideAttribute(string boolVariableName, bool not = false)
    {
        conditionalSourceField = boolVariableName;
        this.not = not;
    }

    public ConditionalHideAttribute(string enumVariableName, int enumIndex, bool not = false)
    {
        conditionalSourceField = enumVariableName;
        this.enumIndex = enumIndex;
        this.not = not;
    }
}