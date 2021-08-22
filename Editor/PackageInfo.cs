using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SLIDDES.PackageManager
{
    [System.Serializable]
    public class PackageInfo
    {
        /// <summary>
        /// Name of the package (com.sliddes.name)
        /// </summary>
        public string name;
        /// <summary>
        /// The display name of the package
        /// </summary>
        public string displayName;
        /// <summary>
        /// Current version of the package
        /// </summary>
        public string version;
        /// <summary>
        /// Latest available version (1.2.3)
        /// </summary>
        public string versionLatest;
        /// <summary>
        /// The url to the git repository
        /// </summary>
        public string giturl;

        public PackageInfo(string name, string displayName, string version, string versionLatest, string giturl)
        {
            this.name = name;
            this.displayName = displayName;
            this.version = version;
            this.versionLatest = versionLatest;
            this.giturl = giturl;
        }
    }
}