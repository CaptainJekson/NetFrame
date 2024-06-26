#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace CustomEditor
{
    public class CopyToDotnetEditor : EditorWindow
    {
        [MenuItem("NetFrame/Migrate .NET project")]
        private static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CopyToDotnetEditor));
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Migrate .NET"))
            {
                Process.Start("/bin/bash", "/Users/evgeniyskvortsov/UnityProjects/NetFrame/Assets/sync_changes_dot_net.sh");
            }

            if (GUILayout.Button("Migrate Clone"))
            {
                Process.Start("/bin/bash", "/Users/evgeniyskvortsov/UnityProjects/NetFrame/Assets/sync_changes_clone.sh");
            }
        }
    }
}
#endif
