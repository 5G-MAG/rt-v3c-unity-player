/*
* Copyright (c) 2025 InterDigital CE Patent Holdings SASU
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using UnityEditor;
using UnityEditor.Build;
using System;
using System.Collections.Generic;

class Build
{
    static void Android()
    {
        var args = System.Environment.GetCommandLineArgs();

        string path = null;
        string name = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].ToLower() == "-path")
            {
                if (i + 1 < args.Length)
                {
                    path = args[i + 1];
                }
            }
            if (args[i].ToLower() == "-name")
            {
                if (i + 1 < args.Length)
                {
                    name = args[i + 1];
                }
            }
        }

        if (path == null)
        {
            System.Console.WriteLine("Error while building project, no valid path was provided\nAborting...");
        }
        else
        {
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = GetEditorScenes();
            buildPlayerOptions.locationPathName = path;
            buildPlayerOptions.target = BuildTarget.Android;
            buildPlayerOptions.options = BuildOptions.None;
            string old_name = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (name != null)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.InterDigital."+name);
            }
            try
            {
                BuildPipeline.BuildPlayer(buildPlayerOptions);
            }
            catch (Exception e)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, old_name);
                Console.WriteLine($"Build failed: {e.Message}");
            }
            Console.WriteLine("Build complete \\o/");
        }
    }

    private static string[] GetEditorScenes()
    {
        List<string> scenes = new List<string>();
        foreach(var s in EditorBuildSettings.scenes) 
        {
            if (s.enabled)
            {
                scenes.Add(s.path);
            }
        }

        return scenes.ToArray();
    }
}
