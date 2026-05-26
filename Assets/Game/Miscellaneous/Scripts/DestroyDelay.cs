using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyDelay : MonoBehaviour
{
    [SerializeField] float delay = 5;

    void Start()
    {
        Destroy(gameObject, delay);
    }
}
