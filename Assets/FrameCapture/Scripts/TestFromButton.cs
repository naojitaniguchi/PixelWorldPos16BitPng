using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine.UI;

public class TestFromButton : MonoBehaviour {
    [DllImport("PngSaveDLLDx11")]
    private static extern int Save16BitPngFromDXTexture(int image_width, int image_height, IntPtr world_tex_ptr, IntPtr mask_tex_ptr, string path);


    public Camera dispCamera;

    // Use this for initialization
    void Start () {
        Button button = this.GetComponent<Button>();
        button.onClick.AddListener(onClicked);

    }

    // Update is called once per frame
    void Update () {
	
	}

    protected virtual void onClicked()
    {
        // RenderTexture.active = dispCamera.targetTexture;
        Save16BitPngFromDXTexture(100, 100, dispCamera.targetTexture.GetNativeTexturePtr(), dispCamera.targetTexture.GetNativeTexturePtr(), "test.png");
    }

}
