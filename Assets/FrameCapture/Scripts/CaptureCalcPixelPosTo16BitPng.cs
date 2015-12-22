using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

[AddComponentMenu("FrameCapturer/ExrCaptureAndCalcPixelPos")]
[RequireComponent(typeof(Camera))]
public class CaptureCalcPixelPosTo16BitPng : MonoBehaviour {
    [DllImport("PngSaveDLLDx11")]
    private static extern int Save16BitPngFromDXTexture(int image_width, int image_height, IntPtr world_tex_ptr, IntPtr mask_tex_ptr, string path, float xRangeMin, float xRangeMax, float yRangeMin, float yRangeMax, float zRangeMin, float zRangeMax);

    public string m_output_directory = "PngOutput";
    public Shader m_sh_copy;
    public int m_begin_frame = 0;
    public int m_end_frame = 100;
    public string m_filename = "world_pos";
    public float xRangeMin = -100 ;
    public float xRangeMax = 100 ;
    public float yRangeMin = -100 ;
    public float yRangeMax = 100 ;
    public float zRangeMin = -100 ;
    public float zRangeMax = 100 ;
    bool m_capture_gbuffer = true;
    RenderTexture m_world_pos;
    RenderTexture m_mask;
    Camera m_cam;
    int m_frame;
    Material m_mat_copy;
    Mesh m_quad;

    public static Mesh CreateFullscreenQuad()
    {
        Vector3[] vertices = new Vector3[4] {
                new Vector3( 1.0f, 1.0f, 0.0f),
                new Vector3(-1.0f, 1.0f, 0.0f),
                new Vector3(-1.0f,-1.0f, 0.0f),
                new Vector3( 1.0f,-1.0f, 0.0f),
            };
        int[] indices = new int[6] { 0, 1, 2, 2, 3, 0 };

        Mesh r = new Mesh();
        r.vertices = vertices;
        r.triangles = indices;
        return r;
    }


#if UNITY_EDITOR
    void Reset()
    {
        m_sh_copy = AssetDatabase.LoadAssetAtPath("Assets/FrameCapturer/Shaders/CopyFrameAndCalcWorldPosition.shader", typeof(Shader)) as Shader;
    }
#endif // UNITY_EDITOR

    void OnEnable()
    {
        System.IO.Directory.CreateDirectory(m_output_directory);
        m_cam = GetComponent<Camera>();
        m_cam.depthTextureMode |= DepthTextureMode.Depth;
        m_cam.depthTextureMode |= DepthTextureMode.DepthNormals;
        m_quad = CreateFullscreenQuad();
        m_mat_copy = new Material(m_sh_copy);
        if (m_cam.targetTexture != null)
        {
            m_mat_copy.EnableKeyword("OFFSCREEN");
        }

        if ( m_cam.renderingPath != RenderingPath.DeferredShading &&
           (m_cam.renderingPath == RenderingPath.UsePlayerSettings && PlayerSettings.renderingPath != RenderingPath.DeferredShading))
        {
            Debug.Log("ExrCapturer: Rendering path must be deferred to use capture_gbuffer mode.");
            m_capture_gbuffer = false;
        }

        if (m_capture_gbuffer)
        {
           m_world_pos = new RenderTexture(m_cam.pixelWidth, m_cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat);
           m_world_pos.filterMode = FilterMode.Point;
           m_world_pos.Create();

            m_mask = new RenderTexture(m_cam.pixelWidth, m_cam.pixelHeight, 0, RenderTextureFormat.RFloat);
            m_mask.filterMode = FilterMode.Point;
            m_mask.Create();
        }
    }

    void OnDisable()
    {

        if (m_world_pos != null)
        {
            m_world_pos.Release();
            m_world_pos = null;
        }
        if (m_mask != null)
        {
            m_mask.Release();
            m_mask = null;
        }
    }

    IEnumerator OnPostRender()
    {
        int frame = m_frame++;
        if (frame >= m_begin_frame && frame <= m_end_frame)
        {
            Debug.Log("ExrCapturer: frame " + frame);

            if (m_capture_gbuffer)
            {

                bool d3d = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;
                Matrix4x4 V = m_cam.worldToCameraMatrix;
                Matrix4x4 P = m_cam.projectionMatrix;

                if (d3d)
                {
                    // Invert Y for rendering to a render texture
                    //for (int i = 0; i < 4; i++)
                    //{
                    //    P[1, i] = -P[1, i];
                    //}
                    // Scale and bias from OpenGL -> D3D depth range
                    for (int i = 0; i < 4; i++)
                    {
                        P[2, i] = P[2, i] * 0.5f + P[3, i] * 0.5f;
                    }
                }

                Matrix4x4 VP = P * V;
                Matrix4x4 invVP = VP.inverse;

                // シェーダーにマトリクスを入れる
                m_mat_copy.SetMatrix("mat_vp_inv", invVP);
                m_mat_copy.SetPass(4);
                Graphics.SetRenderTarget(m_world_pos);
                Graphics.DrawMeshNow(m_quad, Matrix4x4.identity);
 
                // Zからマスクを計算
                m_mat_copy.SetPass(5);
                Graphics.SetRenderTarget(m_mask);
                Graphics.DrawMeshNow(m_quad, Matrix4x4.identity);

                Graphics.SetRenderTarget(null);


                string path = m_output_directory + "/" + m_filename + "_" + frame.ToString("0000") + ".png";

                Save16BitPngFromDXTexture(m_world_pos.width, m_world_pos.height, m_world_pos.GetNativeTexturePtr(), m_mask.GetNativeTexturePtr(), path,
                    xRangeMin, xRangeMax, yRangeMin, yRangeMax, zRangeMin, zRangeMax);

                Debug.Log(path);

            }

            yield return new WaitForEndOfFrame();
        }
    }
}
