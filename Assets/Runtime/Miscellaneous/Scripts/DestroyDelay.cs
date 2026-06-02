using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyDelay : MonoBehaviour
{
    [SerializeField] private float delay = 5;

    private void Start()
    {
        Destroy(gameObject, delay);
    }
}
