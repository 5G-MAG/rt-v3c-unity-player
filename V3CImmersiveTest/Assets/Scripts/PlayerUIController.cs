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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerUIController : MonoBehaviour
{

    [SerializeField]
    private GameObject uiWindow;

    [SerializeField]
    private bool m_alwaysShowMenu = false;

    [SerializeField]
    private bool m_showOnStartup = false;

    private Coroutine revealCoroutine = null;
    
    private float refWidth = 1920.0f;

    public void Start()
    {
        RevealUI(m_showOnStartup || m_alwaysShowMenu);
    }

    public void RevealUI(bool reveal)
    {
        if (!m_alwaysShowMenu)
        {
            Debug.Log($"RevealPlayerUI {reveal}");

            if (uiWindow == null)
            {
                return;
            }

            if (revealCoroutine != null)
            {
                StopCoroutine(revealCoroutine);
            }

            revealCoroutine = StartCoroutine(AnimateUI(reveal ? 0 : -100));
        }
    }


    IEnumerator AnimateUI(float targetPosition)
    {
        targetPosition *= Screen.width / refWidth;

        float animateTime = 0;
        float animationDuration = 0.2f;

        Vector3 originPos = uiWindow.transform.position;
        Vector3 targetPos = new Vector3(targetPosition, originPos.y, originPos.z);

        while (animateTime < animationDuration)
        {
            animateTime += Time.deltaTime;
            uiWindow.transform.position = Vector3.Lerp(originPos, targetPos, animateTime / animationDuration);
            yield return new WaitForEndOfFrame();
        }
        uiWindow.transform.position = targetPos;
        revealCoroutine = null;
    }
}
