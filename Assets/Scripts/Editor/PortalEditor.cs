using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Portal))]
public class PortalEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Portal portal = (Portal)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("portalType"));
        EditorGUILayout.Space();

        if (portal.portalType == Portal.PortalType.Real)
        {
            EditorGUILayout.LabelField("Real Portal Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneToLoad"));
        }
        else
        {
            EditorGUILayout.LabelField("Fake Portal Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("videoPlayer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("respawnPoint"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("holdToSkipTime"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fade Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useFade"));

        if (portal.useFade)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeCanvas"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeImage"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeInDuration"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Flashbang Sound", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useFlashbangSound"));

            if (portal.useFlashbangSound)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("flashbangAudioSource"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("flashbangClip"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("audioFadeOutTime"));
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetCamera"));

        serializedObject.ApplyModifiedProperties();
    }
}
