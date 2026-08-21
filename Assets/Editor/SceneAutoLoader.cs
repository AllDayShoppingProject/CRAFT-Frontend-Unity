#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SceneAutoLoader
{
    private const string MenuPath = "Tools/Scene Auto Loader/Enabled";
    private const string MenuItem = MenuPath + " &%#l";
    private const string ScenePath = "Assets/Scenes/00_Intro/Intro.unity";
    private static readonly string s_editorPrefsKey = $"{Application.dataPath}.SceneAutoLoader.IsEnabled";

    private static bool s_isEnabled;

    static SceneAutoLoader()
    {
        s_isEnabled = EditorPrefs.GetBool(s_editorPrefsKey, false);
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem(MenuItem)]
    private static void Toggle()
    {
        s_isEnabled = !s_isEnabled;
        EditorPrefs.SetBool(s_editorPrefsKey, s_isEnabled);
        Menu.SetChecked(MenuPath, s_isEnabled);
        SetPlayModeStartScene();
        ShowStateNotification();
    }

    [MenuItem(MenuItem, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, s_isEnabled);
        return true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SetPlayModeStartScene();
        }
    }

    private static void SetPlayModeStartScene()
    {
        EditorSceneManager.playModeStartScene = s_isEnabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)
            : null;
    }

    private static void ShowStateNotification()
    {
        EditorWindow targetWindow = EditorWindow.focusedWindow ?? SceneView.lastActiveSceneView;
        if (targetWindow == null)
        {
            return;
        }

        string state = s_isEnabled ? "ON" : "OFF";
        string playModeDescription = s_isEnabled
            ? "Start 씬에서 플레이합니다."
            : "현재 씬에서 플레이합니다.";

        targetWindow.ShowNotification(
            new GUIContent($"Scene Auto Loader: {state}\n{playModeDescription}"),
            2d);
    }
}
#endif