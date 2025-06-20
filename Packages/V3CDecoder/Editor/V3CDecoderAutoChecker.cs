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

namespace IDCC.V3CDecoder.Editor
{
    [InitializeOnLoad]
    public class V3CDecoderAutoChecker : MonoBehaviour
    {
        static V3CDecoderAutoChecker()
        {
            Debug.Log("Checking project settings for V3C Decoder package compatibility");
            bool b = V3CDecoderPackageEditorUtils.CheckSettings();
            if (b) Debug.Log("No issue detected for V3C Decoder package compatibility");

            V3CDecoderPackageEditorUtils.CheckPlugins();
            
        }
    }
}