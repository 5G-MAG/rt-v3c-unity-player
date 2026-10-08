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

// Product name and launcher icon for the player (Assets/Branding/V3CIcon.png).
public static class BrandingSetup
{
    private const string IconPath = "Assets/Branding/V3CIcon.png";

    // batch mode: Unity -executeMethod BrandingSetup.Run
    public static void Run()
    {
        PlayerSettings.productName = "V3C Immersive Player";
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (tex == null)
        {
            Debug.LogError("Branding setup: icon not found");
            return;
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        imp.isReadable = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
#pragma warning disable CS0618
        int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android);
        var icons = new Texture2D[sizes.Length];
        for (int i = 0; i < icons.Length; i++) icons[i] = tex;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, icons);
#pragma warning restore CS0618
        AssetDatabase.SaveAssets();
        Debug.Log($"Branding setup: product name and {sizes.Length} icon slots set");
    }
}
