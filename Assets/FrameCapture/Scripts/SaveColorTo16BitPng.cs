using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

[AddComponentMenu("FrameCapturer/SaveColorTo16BitPng")]
[RequireComponent(typeof(Camera))]
public class SaveColorTo16BitPng : MonoBehaviour {
    [DllImport("PngSaveDLLDx11")]
    private static extern int Save16BitPngFromDXColorTexture(int image_width, int image_height, IntPtr color_tex_ptr, string path);

    public string m_output_directory = "PngOutput";
    public Shader m_sh_copy;
    public int m_begin_frame = 0;
    public int m_end_frame = 10;
    public string m_filename = "color";
    RenderTexture m_color_rt;
    Camera m_cam;
    int m_frame;
    Material m_mat_copy;
    Mesh m_quad;
    CommandBuffer m_cb;

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
        m_quad = CreateFullscreenQuad();
        m_mat_copy = new Material(m_sh_copy);
        if (m_cam.targetTexture != null)
        {
            m_mat_copy.EnableKeyword("OFFSCREEN");
        }

        int tid = Shader.PropertyToID("_TmpFrameBuffer");
        m_cb = new CommandBuffer();
        m_cb.name = "ExrCapturer: copy frame buffer";
        m_cb.GetTemporaryRT(tid, -1, -1, 0, FilterMode.Point);
        m_cb.Blit(BuiltinRenderTextureType.CurrentActive, tid);
        // tid は意図的に開放しない
        m_cam.AddCommandBuffer(CameraEvent.AfterEverything, m_cb);

        m_color_rt = new RenderTexture(m_cam.pixelWidth, m_cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat);
        m_color_rt.wrapMode = TextureWrapMode.Repeat;
        m_color_rt.Create();

    }

    void OnDisable()
    {

        if (m_color_rt != null)
        {
            m_color_rt.Release();
            m_color_rt = null;
        }
    }

    IEnumerator OnPostRender()
    {
        int frame = m_frame++;
        if (frame >= m_begin_frame && frame <= m_end_frame)
        {
            Debug.Log("SaveColorTo16BitPng: frame " + frame);

            yield return new WaitForEndOfFrame();

            m_mat_copy.SetPass(0);
            Graphics.SetRenderTarget(m_color_rt);
            Graphics.DrawMeshNow(m_quad, Matrix4x4.identity);

            Graphics.SetRenderTarget(null);

            string path = m_output_directory + "/" + m_filename + "_" + frame.ToString("0000") + ".png";

            Save16BitPngFromDXColorTexture(m_color_rt.width, m_color_rt.height, m_color_rt.GetNativeTexturePtr(), path);

            Debug.Log(path);
            
        }
    }
}
