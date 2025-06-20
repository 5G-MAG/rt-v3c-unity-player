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
