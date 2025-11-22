using UnityEngine;

public class DebugTextureQuad : MonoBehaviour
{
    public Camera targetCamera;          // leave null to use Camera.main
    public Material debugMaterial;       // material using Unlit/DebugChannelURP
    public RenderTexture sourceTexture;  // your heightMapRT or Dy_Dxz, etc.

    [Header("Quad placement")]
    public float distance = 2f;          // how far in front of camera
    public Vector2 size = new Vector2(1, 1);

    [Header("Channel")]
    [Range(0, 3)] public int channel = 0;
    public float valueScale = 1f;
    public float valueOffset = 0f;

    GameObject quadInstance;
    Material runtimeMat;

    void Start()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera)
        {
            Debug.LogError("DebugTextureQuad: No camera found.");
            enabled = false;
            return;
        }

        if (!debugMaterial)
        {
            Debug.LogError("DebugTextureQuad: Assign a material with Unlit/DebugChannelURP.");
            enabled = false;
            return;
        }

        // Create a quad and parent it to the camera
        quadInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadInstance.name = "DebugTextureQuad";
        quadInstance.transform.SetParent(targetCamera.transform, false);
        quadInstance.transform.localPosition = new Vector3(0, 0, distance);
        quadInstance.transform.localRotation = Quaternion.identity;
        quadInstance.transform.localScale = new Vector3(size.x, size.y, 1);

        // Use our own material instance so we don't overwrite the shared one
        runtimeMat = new Material(debugMaterial);
        var mr = quadInstance.GetComponent<MeshRenderer>();
        mr.sharedMaterial = runtimeMat;

        UpdateMaterial();
    }

    void Update()
    {

        UpdateMaterial();
    }

    void UpdateMaterial()
    {
        if (!runtimeMat) return;

        if (sourceTexture)
            runtimeMat.SetTexture("_MainTex", sourceTexture);

        runtimeMat.SetFloat("_Channel", channel);
        runtimeMat.SetFloat("_Scale", valueScale);
        runtimeMat.SetFloat("_Offset", valueOffset);
    }

    // Let other scripts set the texture & channel programmatically
    public void SetTexture(RenderTexture tex) => sourceTexture = tex;
    public void SetChannel(int c) => channel = Mathf.Clamp(c, 0, 3);
}
