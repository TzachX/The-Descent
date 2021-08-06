using System.Collections.Generic;
using UnityEngine;

namespace LvlGen.Scripts
{
    public class Bounds : MonoBehaviour
    {
            public IEnumerable<Collider> Colliders => GetComponentsInChildren<Collider>();
    }
}
