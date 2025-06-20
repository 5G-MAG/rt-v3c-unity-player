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

public abstract class CameraController : ScriptableObject
{
    public bool enable = true;
    
    protected Camera cam = null;
    protected float referenceFOV = 60.0f;

    new public string name = "";

    public virtual void Init(Camera camera) { cam = camera; referenceFOV = cam.fieldOfView; }
    public abstract void UpdateCam();
    public virtual void SetReferenceFOV(float FOV) { referenceFOV = FOV; Debug.Log($"Reference FOV = {referenceFOV}°"); }
    public virtual string GetName() { return name; }
}
