/*
 * License: 5G-MAG Public License (v1.0)
 * Author: Jordi J. Gimenez (5G-MAG)
 * Copyright: (C) 2026 5G-MAG Association
 *
 * For full license terms please see the LICENSE file distributed with this
 * program. If this file is missing then the license can be retrieved from
 * https://www.5g-mag.com/license.
 */

using UnityEngine;
using IDCC.V3CDecoder.Demo;

namespace IDCC.V3CDecoder
{
    // Shows a floodlit football stadium as the sky while a title that names "Environment": "stadium"
    // in library.json plays, and puts the scene's own sky back for every other title.
    public class StadiumBackdrop : MonoBehaviour
    {
        private const string EnvironmentName = "stadium";

        private Material m_defaultSky;
        private Material m_stadiumSky;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<DemoSceneController>() == null) return;
            new GameObject("StadiumBackdrop").AddComponent<StadiumBackdrop>();
        }

        private void Start()
        {
            m_defaultSky = RenderSettings.skybox;
            m_stadiumSky = Resources.Load<Material>("Env/StadiumSky");
            V3CPlayerHud.EnvironmentChanged += Apply;
            Apply(V3CPlayerHud.CurrentEnvironment);
        }

        private void OnDestroy()
        {
            V3CPlayerHud.EnvironmentChanged -= Apply;
        }

        private void Apply(string environment)
        {
            bool stadium = m_stadiumSky != null && string.Equals(environment, EnvironmentName, System.StringComparison.OrdinalIgnoreCase);
            RenderSettings.skybox = stadium ? m_stadiumSky : m_defaultSky;
        }
    }
}
