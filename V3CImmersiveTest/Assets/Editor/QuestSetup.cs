/*
 * License: 5G-MAG Public License (v1.0)
 * Author: Jordi J. Gimenez (5G-MAG)
 * Copyright: (C) 2026 5G-MAG Association
 *
 * For full license terms please see the LICENSE file distributed with this
 * program. If this file is missing then the license can be retrieved from
 * https://www.5g-mag.com/license.
 */

using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

// Sets the project up for a Meta Quest build. The phone build is the default and is not changed until this is run:
//   Unity -batchmode -nographics -quit -projectPath V3CImmersiveTest -buildTarget Android -executeMethod QuestSetup.Run
// then start Unity again for the build, because the input handling setting only applies after a restart.
//
// What it sets: OpenGL ES 3 only (the decoder plug-in draws with GL), minimum Android 10, linear colour space (OpenXR
// on OpenGL ES refuses gamma), both input handlers (the controllers and hands are read through the Input System), the
// OpenXR loader with the Meta Quest feature and the Touch controller profiles, and multi-pass stereo.
public static class QuestSetup
{
    public static void Run()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        SetActiveInputHandling(2);      // 2 = both

        XRGeneralSettingsPerBuildTarget perBuild;
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perBuild) || perBuild == null)
        {
            perBuild = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(perBuild, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuild, true);
        }

        const BuildTargetGroup Android = BuildTargetGroup.Android;
        if (!perBuild.HasSettingsForBuildTarget(Android)) perBuild.CreateDefaultSettingsForBuildTarget(Android);
        if (!perBuild.HasManagerSettingsForBuildTarget(Android)) perBuild.CreateDefaultManagerSettingsForBuildTarget(Android);
        perBuild.SettingsForBuildTarget(Android).InitManagerOnStart = true;
        var manager = perBuild.ManagerSettingsForBuildTarget(Android);
        bool assigned = XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", Android);
        Debug.Log($"QuestSetup: OpenXR loader assigned={assigned}");
        EditorUtility.SetDirty(perBuild);
        AssetDatabase.SaveAssets();

        UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(Android);   // creates the feature objects
        var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(Android);
        if (xr == null)
        {
            Debug.LogError("QuestSetup: no OpenXR settings for Android");
            return;
        }
        xr.renderMode = OpenXRSettings.RenderMode.MultiPass;
        var quest = xr.GetFeature<MetaQuestFeature>();
        Enable(quest);
        DisableTargetDevice(quest, "cambria");   // Quest Pro: it makes the manifest require eye tracking, which other headsets lack
        Enable(xr.GetFeature<OculusTouchControllerProfile>());
        Enable(xr.GetFeature<MetaQuestTouchProControllerProfile>());
        Enable(xr.GetFeature<MetaQuestTouchPlusControllerProfile>());
        EditorUtility.SetDirty(xr);
        AssetDatabase.SaveAssets();
        Debug.Log("QuestSetup: done");
    }

    // PlayerSettings has no property for this one; it is the "activeInputHandler" field of ProjectSettings.asset
    private static void SetActiveInputHandling(int value)
    {
        var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (settings == null || settings.Length == 0) { Debug.LogError("QuestSetup: ProjectSettings.asset not found"); return; }
        var so = new SerializedObject(settings[0]);
        var property = so.FindProperty("activeInputHandler");
        if (property == null) { Debug.LogError("QuestSetup: activeInputHandler not found"); return; }
        property.intValue = value;
        so.ApplyModifiedProperties();
    }

    private static void DisableTargetDevice(MetaQuestFeature feature, string manifestName)
    {
        if (feature == null) return;
        var so = new SerializedObject(feature);
        var devices = so.FindProperty("targetDevices");
        for (int i = 0; devices != null && i < devices.arraySize; i++)
        {
            var d = devices.GetArrayElementAtIndex(i);
            if (d.FindPropertyRelative("manifestName").stringValue == manifestName) d.FindPropertyRelative("enabled").boolValue = false;
        }
        so.ApplyModifiedProperties();
    }

    private static void Enable(UnityEngine.XR.OpenXR.Features.OpenXRFeature feature)
    {
        if (feature == null) { Debug.LogWarning("QuestSetup: a feature was not found"); return; }
        feature.enabled = true;
        EditorUtility.SetDirty(feature);
    }
}
