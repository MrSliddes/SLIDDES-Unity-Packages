using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace SLIDDES.PackageManager
{
    /// <summary>
    /// For installing/updating SLIDDES/custom packages from a git url
    /// </summary>
    public class Packages : EditorWindow
    {
        private static AddRequest AddRequest;
        private static RemoveRequest RemoveRequest;
        private static ListRequest ListRequest;

        private readonly string DEBUG_PREFIX = "[Packages] ";
        private string EDITORPREF_PREFIX = "SLIDDES_Unity_Packages_";

        /// <summary>
        /// If packages is busy with something where the user has to wait before interacting again
        /// </summary>
        private bool IsBusy
        {
            get
            {
                if(finishedAddingPackage && finishedGetPackageList && finishedGetPackagesUnity && finishedRemovingPackage && finishedUpdatingPackages) return false;
                return true;
            }
        }
        /// <summary>
        /// If the packageListUrl is that of default (coming from SLIDDES)
        /// </summary>
        private bool IsSLIDDESPackages
        {
            get
            {
                return packageListUrl == packageListUrlDefault;
            }
        }

        private bool finishedAddingPackage = true;
        private bool finishedGetPackageList = true;
        private bool finishedGetPackagesUnity = true;
        private bool finishedRemovingPackage = true;
        private bool finishedUpdatingPackages = true;
        /// <summary>
        /// The url where the package list is stored on github
        /// </summary>
        private string packageListUrl;
        /// <summary>
        /// The default url where sliddes package list is stored
        /// </summary>
        private readonly string packageListUrlDefault = "MrSliddes/SLIDDES-Unity-Packages-List/main/list.json";

        /// <summary>
        /// All packages available to be installed
        /// </summary>
        private List<PackageInfo> packageListAvailable = new List<PackageInfo>();
        /// <summary>
        /// All packages installed in project from SLIDDES/Custom
        /// </summary>
        private List<PackageInfo> packageListInstalled = new List<PackageInfo>();
        /// <summary>
        /// All packages installed in project not from SLIDDES
        /// </summary>
        private List<PackageInfo> packagesInfoRest = new List<PackageInfo>();

        // Editor
        private bool foldoutPackagesInstalled = true;
        private bool foldoutPackagesAvailable = true;
        private bool foldoutUnityPackages;
        private bool foldoutSettings;
        private float loadingIconAngle = 0;
        private readonly float dropdownLineHeight = 20;
        private Vector2 windowScrollPosition;

        [MenuItem("Window/SLIDDES/Packages", false)]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EditorWindow window = GetWindow(typeof(Packages), false, "Packages", true);
            window.minSize = new Vector2(320, 160);
        }

        private void OnEnable()
        {
            Load();

            Events.registeredPackages += RegisteredPackagesEventHandler;
            RefreshPackages();
        }

        private void OnDisable()
        {
            Save();

            Events.registeredPackages -= RegisteredPackagesEventHandler;
        }

        private void OnDestroy()
        {
            Save();

            Events.registeredPackages -= RegisteredPackagesEventHandler;
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
            EditorGUILayout.Space();

            OnGUIRefreshButton();
            OnGUIPackageListInstalled();
            OnGUIPackageListAvailable();
            OnGUISettings();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        #region OnGUI Functions

        private void OnGUIRefreshButton()
        {
            GUI.enabled = !IsBusy;
            // Refresh button
            if(GUILayout.Button(new GUIContent("Refresh Packages", "Check for all packages in Unity and SLIDDES available packages"), GUILayout.Height(32)))
            {
                RefreshPackages();
            }
            // Loading icon
            if(IsBusy)
            {
                Rect R = new Rect(8, 12, 24, 24);
                var labelPosition = new Vector2(R.x + R.width * 0.5f, R.y + R.height * 0.5f);
                EditorGUIUtility.RotateAroundPivot(loadingIconAngle, labelPosition);
                loadingIconAngle += 36;
                GUI.Label(R, new GUIContent(EditorGUIUtility.IconContent("Loading@2x").image));
                GUI.matrix = Matrix4x4.identity;
            }

            // Update all button
            if(GUILayout.Button(new GUIContent("Update All Packages", "Updates all installed packages"), GUILayout.Height(32)))
            {
                UpdateAllPackages();
            }

            GUI.enabled = true;
            EditorGUILayout.Space();
        }

        private void OnGUIPackageListInstalled()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldoutPackagesInstalled = EditorGUILayout.Foldout(foldoutPackagesInstalled, IsSLIDDESPackages ? " SLIDDES Installed Packages" : " Custom Installed Packages", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if(foldoutPackagesInstalled)
            {
                EditorGUILayout.BeginHorizontal();
                // Name
                EditorGUILayout.BeginVertical();
                foreach(var package in packageListInstalled)
                {
                    EditorGUILayout.LabelField(package.displayName, GUILayout.Height(dropdownLineHeight));
                }
                EditorGUILayout.EndVertical();

                // Version
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginVertical();
                float preLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 64;
                foreach(var package in packageListInstalled)
                {
                    EditorGUILayout.LabelField(package.version, new GUILayoutOption[] { GUILayout.Width(new GUIStyle().CalcSize(new GUIContent(package.version)).x + 8), GUILayout.Height(dropdownLineHeight) });
                }
                EditorGUIUtility.labelWidth = preLabelWidth;
                EditorGUILayout.EndVertical();

                // Download/update/uptodate
                EditorGUILayout.BeginVertical();
                foreach(var package in packageListInstalled)
                {
                    // Get correct image for the button
                    int state = 0;
                    string img = "";
                    string tooltip = "";
                    if(package.version == package.versionLatest || string.IsNullOrEmpty(package.versionLatest))
                    {
                        // Up to date
                        img = "Installed";
                        tooltip = "Up to date";
                        state = 0;
                    }
                    else
                    {
                        // Download new one
                        img = "Update-Available"; //Download-Available
                        tooltip = "New version available";
                        state = 1;
                    }

                    GUI.enabled = !IsBusy;
                    EditorGUILayout.BeginHorizontal();
                    // Installed/update button
                    if(GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent(img).image, tooltip), new GUILayoutOption[] { GUILayout.Height(dropdownLineHeight), GUILayout.Width(32) }))
                    {
                        switch(state)
                        {
                            case 0: // Latest version
                                UnityEngine.Debug.Log(string.Format("{0}Name:{1} Version:{2} Version Latest:{3}", DEBUG_PREFIX, package.name, package.version, package.versionLatest));
                                break;
                            case 1: // New version available
                                // Are you sure
                                bool confirm = EditorUtility.DisplayDialog("SLIDDES Packages", string.Format("Are you sure you want to update {0} v{1} to v{2} ?", package.displayName, package.version, package.versionLatest), "Yes", "No");
                                if(confirm)
                                {
                                    UnityEngine.Debug.Log(string.Format("{0}Updating {1} v{2} to v{3} ...", DEBUG_PREFIX, package.name, package.version, package.versionLatest));
                                    AddPackage(package.giturl);
                                }
                                break;
                            default: break;
                        }
                    }

                    // Remove button
                    if(GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent("sv_icon_none").image, "Uninstall Package"), new GUILayoutOption[] { GUILayout.Height(dropdownLineHeight), GUILayout.Width(32) }))
                    {
                        RemovePackage(package.name);
                    }
                    EditorGUILayout.EndHorizontal();
                    GUI.enabled = true;
                }
                EditorGUIUtility.labelWidth = preLabelWidth;
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void OnGUIPackageListAvailable()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldoutPackagesAvailable = EditorGUILayout.Foldout(foldoutPackagesAvailable, IsSLIDDESPackages ? " SLIDDES Available Packages" : " Custom Available Packages", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if(foldoutPackagesAvailable)
            {
                EditorGUILayout.BeginHorizontal();
                // Name
                EditorGUILayout.BeginVertical();
                foreach(var package in packageListAvailable)
                {
                    EditorGUILayout.LabelField(package.displayName, GUILayout.Height(dropdownLineHeight));
                }
                EditorGUILayout.EndVertical();

                // Version
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginVertical();
                float preLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 64;
                foreach(var package in packageListAvailable)
                {
                    EditorGUILayout.LabelField(package.version, new GUILayoutOption[] { GUILayout.Width(new GUIStyle().CalcSize(new GUIContent(package.version)).x + 8), GUILayout.Height(dropdownLineHeight) });
                }
                EditorGUIUtility.labelWidth = preLabelWidth;
                EditorGUILayout.EndVertical();

                // Download
                EditorGUILayout.BeginVertical();
                foreach(var package in packageListAvailable)
                {
                    // Get correct image for the button
                    GUI.enabled = !IsBusy;
                    if(GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent("Download-Available").image, "Install package"), new GUILayoutOption[] { GUILayout.Height(dropdownLineHeight), GUILayout.Width(32) }))
                    {
                        AddPackage(package.giturl);
                    }
                    GUI.enabled = true;
                }
                EditorGUIUtility.labelWidth = preLabelWidth;
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            // Unity packages
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldoutUnityPackages = EditorGUILayout.Foldout(foldoutUnityPackages, " Unity Installed Packages", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if(foldoutUnityPackages)
            {
                foreach(var package in packagesInfoRest)
                {
                    EditorGUILayout.LabelField(package.displayName);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void OnGUISettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldoutSettings = EditorGUILayout.Foldout(foldoutSettings, " Settings", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if(foldoutSettings)
            {
                // packageListGitUrl
                string prePackageListUrl = packageListUrl;
                packageListUrl = EditorGUILayout.DelayedTextField(new GUIContent("Package List .json Url", "The github raw url of the package list .json file.\n\nDefault: " + packageListUrlDefault + ".\n\nYour own package list url would look like\n(https://raw.githubusercontent.com/) + \"User\"/\"RepositoryName\"/main/list.json"), packageListUrl);
                CheckPackageListUrl();
                if(prePackageListUrl != packageListUrl) RefreshPackages();
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        /// <summary>
        /// Start a background task with Ienumerator
        /// </summary>
        /// <param name="update"></param>
        /// <param name="end"></param>
        private static void StartBackgroundTask(IEnumerator update, Action end = null)
        {
            EditorApplication.CallbackFunction closureCallback = null;

            closureCallback = () =>
            {
                try
                {
                    if(update.MoveNext() == false)
                    {
                        if(end != null) end();
                        EditorApplication.update -= closureCallback;
                    }
                }
                catch(Exception ex)
                {
                    if(end != null) end();
                    UnityEngine.Debug.LogException(ex);
                    EditorApplication.update -= closureCallback;
                }
            };
            EditorApplication.update += closureCallback;
        }

        /// <summary>
        /// Convert a UnityEditor.PackageManager.PackageInfo to SLIDDES.PackageManager.Packages.PackageInfo
        /// </summary>
        private static PackageInfo ToPackageInfo(UnityEditor.PackageManager.PackageInfo p)
        {
            return new PackageInfo(p.name, p.displayName, p.version, p.versions.latest, "N/A");
        }

        /// <summary>
        /// Add a package to this unity project
        /// </summary>
        /// <param name="indentifier">
        /// A string representing the package to be added:
        /// - To install a specific version of a package, use a package identifier("name@version"). This is the only way to install a pre-release version.
        /// - To install the latest compatible (released) version of a package, specify only the package name.
        /// - To install a git package, specify a git url.
        /// - To install a local package, specify a value in the format "file:/path/to/package/folder".
        /// - To install a local tarball package, specify a value in the format "file:/path/to/package-file.tgz".
        /// ArgumentException is thrown if identifier is null or empty. 
        /// </param>
        private void AddPackage(string indentifier)
        {
            // If addrequest isnt busy
            if(AddRequest == null || AddRequest.IsCompleted)
            {
                finishedAddingPackage = false;
                UnityEngine.Debug.Log(DEBUG_PREFIX + "Adding package " + indentifier + "...");
                AddRequest = Client.Add(indentifier);
                EditorApplication.update += AwaitAddPackage;
            }
        }

        /// <summary>
        /// Wait until the listrequest is completed to get all project packages
        /// </summary>
        private void AwaitGetProjectPackages()
        {
            finishedGetPackagesUnity = false;
            if(ListRequest.IsCompleted)
            {
                if(ListRequest.Status == StatusCode.Success)
                {
                    foreach(var package in ListRequest.Result)
                    {
                        // Check for SLIDDES package or unity package
                        if(IsSLIDDESPackages)
                        {
                            if(package.name.Contains("com.sliddes.")) packageListInstalled.Add(ToPackageInfo(package)); else packagesInfoRest.Add(ToPackageInfo(package));
                        }
                        else
                        {
                            if(!package.name.Contains("com.unity.")) packageListInstalled.Add(ToPackageInfo(package)); else packagesInfoRest.Add(ToPackageInfo(package));
                        }
                    }

                    // Sort lists abc
                    packageListInstalled.Sort((x, y) => string.Compare(x.displayName, y.displayName));
                    packagesInfoRest.Sort((x, y) => string.Compare(x.displayName, y.displayName));
                }
                else if(ListRequest.Status >= StatusCode.Failure)
                {
                    Debug.Log(DEBUG_PREFIX + ListRequest.Error.message);
                }

                finishedGetPackagesUnity = true;
                EditorApplication.update -= AwaitGetProjectPackages;
            }
        }

        /// <summary>
        /// Wait until AddRequest is completed to refresh packages
        /// </summary>
        private void AwaitAddPackage()
        {
            if(AddRequest.IsCompleted)
            {
                if(AddRequest.Status == StatusCode.Success)
                {
                    Debug.Log(DEBUG_PREFIX + "Installed: " + AddRequest.Result.packageId);
                    // Refresh packages
                    RefreshPackages();
                }
                else if(AddRequest.Status >= StatusCode.Failure)
                {
                    Debug.Log(DEBUG_PREFIX + AddRequest.Error.message);
                }

                finishedAddingPackage = true;
                EditorApplication.update -= AwaitAddPackage;
            }
        }

        /// <summary>
        /// Wait until RemoveRequest is completed to refresh packages
        /// </summary>
        private void AwaitRemovePackage()
        {
            if(RemoveRequest.IsCompleted)
            {
                if(RemoveRequest.Status == StatusCode.Success)
                {
                    Debug.Log(DEBUG_PREFIX + "Removed: " + RemoveRequest.PackageIdOrName);
                    // Refresh packages
                    RefreshPackages();
                }
                else if(AddRequest.Status >= StatusCode.Failure)
                {
                    Debug.Log(DEBUG_PREFIX + RemoveRequest.Error.message);
                }

                finishedRemovingPackage = true;
                EditorApplication.update -= AwaitRemovePackage;
            }
        }

        /// <summary>
        /// Check if the packageListUrl is valid
        /// </summary>
        private void CheckPackageListUrl()
        {
            if(string.IsNullOrEmpty(packageListUrl))
            {
                packageListUrl = packageListUrlDefault;
            }
            else
            {
                if(packageListUrl.Contains("https://raw.githubusercontent.com/"))
                {
                    Debug.LogWarning(DEBUG_PREFIX + "packageListUrl contains https://raw.githubusercontent.com/ which does not have to be included. Only the UserName/Repository/main/fileName.json part.");
                    string prefix = "https://raw.githubusercontent.com/";
                    packageListUrl = packageListUrl.Substring(packageListUrl.IndexOf(prefix) + prefix.Length);
                }
            }
        }

        /// <summary>
        /// Get the packages from the packageListUrl .json file
        /// </summary>
        /// <returns></returns>
        private IEnumerator GetPackageList()
        {
            // Wait for listRequest to finish
            while(!ListRequest.IsCompleted) yield return null;

            string url = "https://sliddes.com/software/unity/get-packages.php?list=" + packageListUrl;
            string result = "";
            using(UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                yield return webRequest.SendWebRequest();

                while(!webRequest.isDone) yield return null;

                // Get the json from webRequest
                string[] pages = url.Split('/');
                int page = pages.Length - 1;
                switch(webRequest.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                        Debug.LogWarning(DEBUG_PREFIX + pages[page] + ": Error: " + webRequest.error);
                        result = "{\"result\":\"601\"}"; 
                        break;
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogWarning(DEBUG_PREFIX + pages[page] + ": HTTP Error: " + webRequest.error);
                        result = "{\"result\":\"602\"}"; 
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogWarning(DEBUG_PREFIX + pages[page] + ": HTTP Error: " + webRequest.error);
                        result = "{\"result\":\"603\"}"; 
                        break;
                    case UnityWebRequest.Result.Success:
                        // The html page contains <html> junk so we need to remove that
                        string s = webRequest.downloadHandler.text;
                        int i1 = s.IndexOf("<pre>") + 5; 
                        int i2 = s.IndexOf("</pre>");
                        s = s.Substring(i1, i2 - i1);
                        result = s.Trim();
                        break;
                }

                // Convert result json to class
                PackageInfoRoot root = JsonUtility.FromJson<PackageInfoRoot>(result);
                PackageInfo[] packages = root.packageList;
                if(packages.Length == 0) Debug.LogError(DEBUG_PREFIX + result);
                // Add class to list available
                foreach(var package in packages)
                {
                    // if already present in package info SLIDDES dont add it, else add it to SLIDDES available
                    PackageInfo packageInstalled = packageListInstalled.Find(x => x.name == package.name);
                    if(packageInstalled != null)
                    {
                        if(packageInstalled.version == null || package.versionLatest == null) continue; // Package not yet ready

                        // Already installed, check latest version for update
                        if(packageInstalled.version.CompareTo(package.versionLatest) < 0)
                        {
                            // Git package version is newer than installed package
                            packageInstalled.versionLatest = package.versionLatest;
                            packageInstalled.giturl = package.giturl;
                            packageInstalled.isLatestVersion = false;
                        }
                    }
                    else
                    {
                        // Package not installed
                        if(string.IsNullOrEmpty(package.displayName)) package.displayName = "Display Name: null";
                        packageListAvailable.Add(package);
                    }
                }
            }

            finishedGetPackageList = true;
        }

        /// <summary>
        /// Refresh the packages lists
        /// </summary>
        /// <see cref="AwaitGetProjectPackages"/>
        private void RefreshPackages()
        {
            if(!IsBusy)
            {
                // Reset
                finishedGetPackagesUnity = false;
                finishedGetPackageList = false;
                packageListInstalled.Clear();
                packageListAvailable.Clear();
                packagesInfoRest.Clear();

                // Request unity installed packages
                ListRequest = Client.List();
                EditorApplication.update += AwaitGetProjectPackages;

                // Request all available packages from packageListUrl
                StartBackgroundTask(GetPackageList());
            }
        }

        /// <summary>
        /// Called when the editor has finished compiling the new list of packages
        /// </summary>
        /// <param name="packageRegistrationEventArgs"></param>
        private void RegisteredPackagesEventHandler(PackageRegistrationEventArgs packageRegistrationEventArgs)
        {
            RefreshPackages();
        }

        /// <summary>
        /// Remove a package from unity
        /// </summary>
        /// <param name="indentifier"></param>
        private void RemovePackage(string indentifier)
        {
            if(RemoveRequest != null || RemoveRequest != null && !RemoveRequest.IsCompleted) return;

            if(EditorUtility.DisplayDialog("Packages", string.Format("Are you sure you want to delete {0}?", indentifier), "Delete", "No"))
            {
                finishedRemovingPackage = false;
                UnityEngine.Debug.Log(DEBUG_PREFIX + "Removing package " + indentifier + "...");
                RemoveRequest = Client.Remove(indentifier);
                EditorApplication.update += AwaitRemovePackage;
                
            }
        }

        /// <summary>
        /// Updates all SLIDDES/Custom installed packages
        /// </summary>
        private void UpdateAllPackages()
        {
            finishedUpdatingPackages = false;
            foreach(var package in packageListInstalled)
            {
                if(!package.isLatestVersion)
                {
                    // Update package, ask for convermation
                    if(EditorUtility.DisplayDialog("Packages", string.Format("Are you sure you want to update {0}{1} to {2}?", package.displayName, package.version, package.versionLatest), "Yes", "No"))
                    {
                        AddPackage(package.giturl);
                    }
                }
            }
            finishedUpdatingPackages = true;
            EditorUtility.DisplayDialog("Packages", "Finished updating packages.", "Okay");
        }


        private void Save()
        {
            EditorPrefs.SetString(EDITORPREF_PREFIX + "packageListUrl", packageListUrl);
        }

        private void Load()
        {
            packageListUrl = EditorPrefs.GetString(EDITORPREF_PREFIX + "packageListUrl", packageListUrlDefault);
            CheckPackageListUrl();
        }
    }
}
