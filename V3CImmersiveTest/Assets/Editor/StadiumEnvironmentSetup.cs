/*
 * License: 5G-MAG Public License (v1.0)
 * Author: Jordi J. Gimenez (5G-MAG)
 * Copyright: (C) 2026 5G-MAG Association
 *
 * For full license terms please see the LICENSE file distributed with this
 * program. If this file is missing then the license can be retrieved from
 * https://www.5g-mag.com/license.
 */

using UnityEditor;
using UnityEngine;

// Prepares the stadium backdrop used behind football content: keeps the panorama at full resolution and
// creates the panoramic sky material the player switches to (Resources/Env/StadiumSky.mat).
public class StadiumTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains("Resources/Env/")) return;
        var imp = (TextureImporter)assetImporter;
        imp.textureShape = TextureImporterShape.Texture2D;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.wrapModeV = TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 8192;
        imp.sRGBTexture = true;
        var android = imp.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 8192;
        android.format = TextureImporterFormat.ASTC_6x6;
        imp.SetPlatformTextureSettings(android);
    }
}

public static class StadiumEnvironmentSetup
{
    private const string MaterialPath = "Assets/Resources/Env/StadiumSky.mat";
    private const string TexturePath = "Assets/Resources/Env/stadium.jpg";

    // batch mode: Unity -executeMethod StadiumEnvironmentSetup.Run
    public static void Run()
    {
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        var shader = Shader.Find("Skybox/Panoramic");
        if (tex == null || shader == null)
        {
            Debug.LogError($"Stadium setup: texture={(tex != null)} shader={(shader != null)}");
            return;
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.shader = shader;
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Mapping", 1f);
        mat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
        mat.SetFloat("_ImageType", 0f);
        mat.SetFloat("_Exposure", 1.0f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("Stadium setup: material ready");
    }
}
