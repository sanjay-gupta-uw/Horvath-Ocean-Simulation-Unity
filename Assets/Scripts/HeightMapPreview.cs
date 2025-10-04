using UnityEngine;

public class HeightmapPreview : MonoBehaviour
{
    public RenderTexture heightRT;  // assign TimeDependentSpectrum.HeightMapTex
    [Range(-2f, 2f)] public float bias = 0.5f;
    [Range(0f, 2f)] public float scale = 0.1f;
    [Range(0, 3)] public int channel = 0;
    [SerializeField] Material material;

    MeshRenderer _mr;

    void Awake()
    {
        Debug.Log("HeightmapPreview Awake");
        // make a quad
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Heightmap Preview Quad";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero + Vector3.up * 4f;
        go.transform.localScale = new Vector3(1, 1, 1);

        _mr = go.GetComponent<MeshRenderer>();
        _mr.sharedMaterial = material;
    }

    public void SetRenderTexture(RenderTexture rt)
    {
        heightRT = rt;
    }

    void LateUpdate()
    {
        if (heightRT == null || material == null) return;
        material.SetTexture("_HeightTex", heightRT);
        material.SetFloat("_Scale", scale);
        material.SetFloat("_Bias", bias);
        material.SetInt("_Channel", channel);
    }
}
