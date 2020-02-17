using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArenaManager : MonoBehaviour
{
    [SerializeField]
    private GameObject cubePrefab;

    internal void SummonCube(string name)
    {
        int y = name[0] - 'A';
        y = 1 - y;

        int x = int.Parse(name[1].ToString()) - 1;

        var go = Instantiate(cubePrefab, transform);
        go.transform.localPosition = new Vector3(-1.5f + 0.75f * x, 0.6f, -1f + 0.75f * y);
    }
}
