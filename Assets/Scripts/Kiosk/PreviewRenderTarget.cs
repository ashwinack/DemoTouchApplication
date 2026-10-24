using UnityEngine;
using UnityEngine.UI;

namespace DemoTouch
{
    /// <summary>
    /// Renders the 3D preview camera into a RenderTexture sized for Display 2, then shows that texture
    /// full screen on Display 2 and in the small live preview on Display 1 — both always show the same frame.
    /// </summary>
    public class PreviewRenderTarget : MonoBehaviour
    {
        [SerializeField] Camera sourceCamera;
        [SerializeField, Tooltip("Every RawImage that should show the preview (Display 2 full screen, Display 1 window).")]
        RawImage[] outputs;
        [SerializeField, Tooltip("The display whose resolution the texture should match.")]
        int displayIndex = 1;
        [SerializeField] Vector2Int fallbackSize = new Vector2Int(1920, 1080);

        RenderTexture texture;

        public RenderTexture Texture => texture;

        void Awake()
        {
            var size = fallbackSize;
            if (!Application.isEditor && displayIndex < Display.displays.Length)
            {
                var display = Display.displays[displayIndex];
                size = new Vector2Int(display.systemWidth, display.systemHeight);
            }

            texture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Preview Render Texture",
                antiAliasing = 1,
            };
            texture.Create();

            sourceCamera.targetTexture = texture;
            foreach (var output in outputs)
                if (output != null) output.texture = texture;
        }

        void OnDestroy()
        {
            if (sourceCamera != null) sourceCamera.targetTexture = null;
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
