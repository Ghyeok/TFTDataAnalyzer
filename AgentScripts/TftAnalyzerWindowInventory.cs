using System.Linq;
using UnityEditor;
using UnityEngine;

// Read-only inventory for comparing exact user window and native frame identities around a test run.
public static class TftAnalyzerWindowInventory
{
    public static object Read()
    {
        var containers = typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow", true);
        return new
        {
            WindowIds = Resources.FindObjectsOfTypeAll<EditorWindow>().Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray(),
            ContainerIds = Resources.FindObjectsOfTypeAll(containers).Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray(),
            FocusId = EditorWindow.focusedWindow == null ? 0 : EditorWindow.focusedWindow.GetInstanceID()
        };
    }
}
