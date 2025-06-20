/*
* Copyright (c) 2024 InterDigital R&D France
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.IO;

namespace IDCC.V3CDecoder.Editor
{
    public class V3CDecoderPackageEditorUtils : MonoBehaviour
    {
        private static Dictionary<BuildTarget, List<GraphicsDeviceType>> m_supportedApis = new Dictionary<BuildTarget, List<GraphicsDeviceType>> 
        {
            { BuildTarget.StandaloneWindows, new List<GraphicsDeviceType>{GraphicsDeviceType.OpenGLCore, GraphicsDeviceType.OpenGLES3} },
            { BuildTarget.Android, new List<GraphicsDeviceType>{GraphicsDeviceType.OpenGLES3 }}
        };

        private enum LogLevel { Silent, Log, Warning, Error }

        [MenuItem("V3CDecoder/Check Settings")]
        public static bool CheckSettings()
        {
            var APIs_win = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows);
            CheckAPI(BuildTarget.StandaloneWindows, APIs_win);

            var APIs_and = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            CheckAPI(BuildTarget.Android, APIs_and);

            var scripting = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android);
            var architecture = PlayerSettings.Android.targetArchitectures;

            if (scripting != ScriptingImplementation.IL2CPP)
            {
                Log($"Invalid scripting backend {scripting} for Android. Please select IL2CPP and select arm64", LogLevel.Error);
                return false;
            }
            else
            {
                if (architecture != AndroidArchitecture.ARM64)
                {
                    if (architecture.HasFlag(AndroidArchitecture.ARM64))
                    {
                        Log("V3C Decoder Package: For Android, only ARM64 is supported. It is recommanded you remove all other build target", LogLevel.Warning);
                    }

                    Log($"Invalid architecture {architecture} for Android. Please select ARM64 only", LogLevel.Error);
                    return false;
                }
            }
            return true;
        }

        [MenuItem("V3CDecoder/Check Plugins")]
        public static void CheckPlugins()
        {
            var path = "Packages/com.interdigital-philips.v3cdecoder/Runtime/Plugins";

            var libs = new List<string>();
            var dll_win = Directory.GetFiles(path, "*.dll");
            var synths_win = Directory.GetFiles(path, "*Synthesizer*.dll");
            var dash_win = Directory.GetFiles(path, "v3c_dash_streamer.dll");
            var hapt_win = Directory.GetFiles(path, "V3CImmersiveDecoderHaptic.dll");
            var iloj_win = Directory.GetFiles(path, "iloj_avcodec_*.dll");

            if (CheckArray(dll_win, "No plugins found for windows!", LogLevel.Warning))
            {

                CheckAndAddArrayToList(libs, synths_win, "No synthesizer plugin found for windows", LogLevel.Error);
                CheckAndAddArrayToList(libs, dash_win, "No Dash plugin found for windows", LogLevel.Warning);
                CheckAndAddArrayToList(libs, hapt_win, "No haptic plugin found for windows", LogLevel.Warning);
                CheckAndAddArrayToList(libs, iloj_win, "No iloj plugin found for windows", LogLevel.Warning);

                foreach (var l in libs)
                {
                    var plugin_importer = AssetImporter.GetAtPath(l) as PluginImporter;

                    if (!plugin_importer)
                    {
                        Debug.LogError($"Plugin Importer not found for file {l}");
                        continue;
                    }
                    if (plugin_importer.isPreloaded != true)
                    {
                        plugin_importer.isPreloaded = true;
                        EditorUtility.SetDirty(plugin_importer);
                    }

                }

                libs.Clear();

                CheckAndAddArrayToList(libs, dll_win, "It's not supposed to happen, but we lost track of all the windows dll at some point", LogLevel.Error);

                foreach (var l in libs)
                {
                    var plugin_importer = AssetImporter.GetAtPath(l) as PluginImporter;

                    if (!plugin_importer)
                    {
                        Debug.LogError($"Plugin Importer not found for file {l}");
                        continue;
                    }

                    if (plugin_importer.GetCompatibleWithPlatform(BuildTarget.Android) != false)
                    {
                        plugin_importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                        EditorUtility.SetDirty(plugin_importer);
                    }
                    if (plugin_importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows) != true)
                    {
                        plugin_importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, true);
                        EditorUtility.SetDirty(plugin_importer);
                    }
                    plugin_importer.SaveAndReimport();
                }

                libs.Clear();
            }
            path = "Packages/com.interdigital-philips.v3cdecoder/Runtime/Plugins/Android/libs/arm64-v8a";

            var so_and = Directory.GetFiles(path, "*.so");
            var synths_and = Directory.GetFiles(path, "*Synthesizer*.so");
            var dash_and = Directory.GetFiles(path, "libv3c_dash_streamer.so");
            var hapt_and = Directory.GetFiles(path, "libV3CImmersiveDecoderHaptic.so");
            var iloj_and = Directory.GetFiles(path, "libiloj_avcodec_*.so");

            if (CheckArray(so_and, "No plugins found for android!", LogLevel.Warning))
            {

                CheckAndAddArrayToList(libs, synths_and, "No synthesizer plugin found for android", LogLevel.Error);
                CheckAndAddArrayToList(libs, dash_and, "No Dash plugin found for android", LogLevel.Warning);
                CheckAndAddArrayToList(libs, hapt_and, "No haptic plugin found for android", LogLevel.Warning);
                CheckAndAddArrayToList(libs, iloj_and, "No iloj plugin found for android", LogLevel.Warning);


                foreach (var l in libs)
                {
                    var plugin_importer = AssetImporter.GetAtPath(l) as PluginImporter;

                    if (!plugin_importer)
                    {
                        Debug.LogError($"Plugin Importer not found for file {l}");
                        continue;
                    }
                    if (plugin_importer.isPreloaded != true)
                    {
                        plugin_importer.isPreloaded = true;
                        EditorUtility.SetDirty(plugin_importer);
                    }
                }

                libs.Clear();

                CheckAndAddArrayToList(libs, so_and, "It's not supposed to happen, but we lost track of all the android .so at some point", LogLevel.Error);

                foreach (var l in libs)
                {
                    var plugin_importer = AssetImporter.GetAtPath(l) as PluginImporter;

                    if (!plugin_importer)
                    {
                        Debug.LogError($"Plugin Importer not found for file {l}");
                        continue;
                    }

                    if (plugin_importer.GetCompatibleWithPlatform(BuildTarget.Android) == false)
                    {
                        plugin_importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
                        EditorUtility.SetDirty(plugin_importer);
                    }
                    if (plugin_importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows) == true)
                    {
                        plugin_importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                        EditorUtility.SetDirty(plugin_importer);
                    }
                    if ( plugin_importer.GetPlatformData(BuildTarget.Android, "CPU") != "ARM64")
                    {
                        plugin_importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
                        EditorUtility.SetDirty(plugin_importer);
                    }
                    plugin_importer.SaveAndReimport();
                }
                libs.Clear();
            }
        }

        private static void CheckAPI(BuildTarget target, GraphicsDeviceType[] api_list)
        {
            if (m_supportedApis.ContainsKey(target))
            {
                bool found = false;
                foreach (var api in m_supportedApis[target])
                {
                    if (api_list[0] == api)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    var a = CollectionToString(m_supportedApis[target]);
                    var b = CollectionToString(api_list);
                    Log($"Invalid API selected for target {target}. Supported APIs are: {a}, but current APIs are: {b}", LogLevel.Error);
                }
            }
            else
            {
                var keys = CollectionToString(m_supportedApis.Keys);
                Log($"Build target {target} not supported.\nSupported targets: {keys}", LogLevel.Error);
            }
        }

        private static void Log(string msg, LogLevel lvl)
        {
            switch (lvl)
            {
                case LogLevel.Silent:
                    break;
                case LogLevel.Log:
                    Debug.Log("V3C Decoder Package:" + msg);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning("V3C Decoder Package:" + msg);
                    break;
                case LogLevel.Error:
                    Debug.LogError("V3C Decoder Package:" + msg);
                    break;
                default:
                    break;
            }
        }

        private static void CheckAndAddArrayToList<T>(List<T> list, T[] array, string error_message = "", LogLevel lvl = LogLevel.Log)
        {
            if (CheckArray(array, error_message, lvl))
            {
                foreach (var s in array) { 
                    list.Add(s); 
                }
            }
        }

        private static bool CheckArray<T>(T[] array, string error_message, LogLevel lvl)
        {
            if (array == null || array.Length == 0)
            {
                Log(error_message, lvl);
                return false;
            }
            return true;
        }
        

        private static string CollectionToString<T>(ICollection<T> collection)
        {
            string elem = "";
            int num_elem = collection.Count;
            int elem_count = 0;
            foreach (var e in collection)
            {
                elem += e.ToString();
                elem_count++;
                if (elem_count < num_elem)
                {
                    elem += ", ";
                }
            }
            return elem;
        }

    }
}
