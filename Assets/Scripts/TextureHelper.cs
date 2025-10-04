using UnityEngine;

public class TextureHelper : MonoBehaviour
{


    public Color[,] pixelData; // Your 2D array of Color data

    public static Texture2D CreateTextureFrom2DArray(Color[,] pixelArray)
    {
        int width = pixelArray.GetLength(0);
        int height = pixelArray.GetLength(1);
        Texture2D texture = new Texture2D(width, height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                texture.SetPixel(x, y, pixelArray[x, y]);
            }
        }
        texture.Apply();
        return texture;
    }

    // expose this as a singleton
    public static TextureHelper Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetTextureToMaterial(Texture2D texture, Renderer renderer)
    {
        if (renderer != null)
        {
            renderer.material.SetTexture("_MainTex", texture);
        }
    }

    void Start()
    {
        // Example: Initialize pixelData with some colors
        Texture2D img = Resources.Load<Texture2D>("luffy_wano"); // Load your image from Resources folder

        Renderer planeRenderer = GetComponent<Renderer>();
        if (planeRenderer != null)
        {
            planeRenderer.material.SetTexture("_MainTex", img); // Replace "_MainTex" with your Shader Graph property reference name
        }
    }


    void Update()
    {

    }
}
