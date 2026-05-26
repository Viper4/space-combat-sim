using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IFilter
{
    float Evaluate(Vector3 point);
}
