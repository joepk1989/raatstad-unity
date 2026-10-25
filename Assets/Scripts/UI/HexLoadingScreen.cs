using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Laadscherm met animerende gele zeshoeken op een grid, donkere achtergrond.
// Zet dit script op een lege GameObject in je startscène. Het bouwt zelf het scherm op.
public class HexLoadingScreen : MonoBehaviour
{
    [Header("Kleuren")]
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
    public Color hexColor = new Color(1f, 0.82f, 0.1f, 1f);

    [Header("Grid")]
    public float hexRadius = 36f;   // straal van een hexagoon in pixels
    public float gap = 6f;          // ruimte tussen de hexagonen

    [Header("Animatie")]
    public float waveSpeed = 2f;
    public float waveFrequency = 0.01f; // hoe snel de golf over het scherm loopt
    public float minScale = 0.25f;
    public float maxScale = 1f;
    public float minAlpha = 0.1f;

    [Header("Gedrag")]
    public bool startVisible = true;
    public float fadeDuration = 0.5f;

    const float Sqrt3 = 1.7320508f;

    GameObject root;
    CanvasGroup group;
    readonly List<RectTransform> hexRects = new List<RectTransform>();
    readonly List<Image> hexImages = new List<Image>();
    readonly List<Vector2> hexPositions = new List<Vector2>();
    Coroutine fadeRoutine;

    void Awake()
    {
        BuildScreen();
        if (!startVisible) root.SetActive(false);
    }

    void Update()
    {
        if (!root.activeSelf) return;

        // Unscaled time, zodat de animatie ook doorloopt als Time.timeScale op 0 staat
        float time = Time.unscaledTime * waveSpeed;

        for (int i = 0; i < hexRects.Count; i++)
        {
            float dist = hexPositions[i].magnitude;
            float wave = Mathf.Sin(time - dist * waveFrequency); // -1..1
            float p = wave * 0.5f + 0.5f;                       // 0..1

            hexRects[i].localScale = Vector3.one * Mathf.Lerp(minScale, maxScale, p);
            hexImages[i].color = new Color(hexColor.r, hexColor.g, hexColor.b,
                                           Mathf.Lerp(minAlpha, 1f, p));
        }
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
    }

    // ---- Publieke API ----

    public void Show()
    {
        root.SetActive(true);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(1f));
    }

    public void Hide()
    {
        if (!root.activeSelf) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(0f));
    }

    // ---- Opbouw ----

    void BuildScreen()
    {
        root = new GameObject("HexLoadingScreen");
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        group = root.AddComponent<CanvasGroup>();

        // Donkere achtergrond over het hele scherm
        var bg = CreateImage("Background", root.transform, backgroundColor, null);
        Stretch(bg.rectTransform);

        Sprite hexSprite = CreateHexSprite(128);

        float w = Screen.width;
        float h = Screen.height;

        // Flat-top hexagonen: kolommen lopen 1.5R uit elkaar, oneven kolommen zijn verschoven
        float dx = 1.5f * hexRadius + gap;
        float dy = Sqrt3 * hexRadius + gap;
        int cols = Mathf.CeilToInt(w * 0.5f / dx) + 2;
        int rows = Mathf.CeilToInt(h * 0.5f / dy) + 2;

        for (int c = -cols; c <= cols; c++)
        {
            for (int r = -rows; r <= rows; r++)
            {
                float x = c * dx;
                float y = r * dy + ((c & 1) != 0 ? dy * 0.5f : 0f);

                var img = CreateImage("Hex", root.transform, hexColor, hexSprite);
                var rect = img.rectTransform;
                rect.sizeDelta = new Vector2(hexRadius * 2f, hexRadius * 2f);
                rect.anchoredPosition = new Vector2(x, y);

                hexRects.Add(rect);
                hexImages.Add(img);
                hexPositions.Add(new Vector2(x, y));
            }
        }
    }

    IEnumerator Fade(float target)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;
        if (target <= 0f) root.SetActive(false);
    }

    Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Maakt een wit, gevulde zeshoek als sprite zodat de Image-kleur hem kan tinten
    static Sprite CreateHexSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        float rad = half - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float p = (x + 0.5f - half) / rad;
                float q = (y + 0.5f - half) / rad;
                bool inside = Mathf.Abs(q) <= Sqrt3 * 0.5f &&
                              Sqrt3 * Mathf.Abs(p) + Mathf.Abs(q) <= Sqrt3;
                pixels[y * size + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
