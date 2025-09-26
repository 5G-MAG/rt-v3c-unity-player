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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TestRenderPrimitive : MonoBehaviour
{

    public Camera m_camera;
    public Material m_material;

    private RenderParams m_rp;
    private GraphicsBuffer m_indirectArgs;


    // Start is called before the first frame update
    void Start()
    {
        m_indirectArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawArgs.size);
        GraphicsBuffer.IndirectDrawArgs[] buff = new GraphicsBuffer.IndirectDrawArgs[1];
        buff[0].vertexCountPerInstance = 3;
        buff[0].instanceCount = 1;
        m_indirectArgs.SetData(buff);

        m_rp = new RenderParams();
    }

    // Update is called once per frame
    void Update()
    {
        m_rp.camera = m_camera;
        m_rp.material = m_material;
        Graphics.RenderPrimitivesIndirect(m_rp, MeshTopology.Triangles, m_indirectArgs);
    }
}
