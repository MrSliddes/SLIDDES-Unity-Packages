using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SLIDDES.PackageManager
{
    [System.Serializable]
    public class PackageInfoRoot
    {
        // Important that this name is the same as the json array name
        public PackageInfo[] packageList;
    }
}