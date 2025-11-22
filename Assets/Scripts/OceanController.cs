using UnityEngine;

[System.Serializable]
public class OceanSettings
{
    [Delayed]
    public int _size = 256; // N x N grid
    [Min(0.1f)]
    public float _spacing = 1f; // spacing between grid points in world units
    public float _oceanDepth = 100f;
    public float _windSpeed = 10f;
    [Min(0f)]
    public float _distanceToShore = 1000f;
    public float _GRAVITY = 9.81f;
};

[System.Serializable]
public class WaveSettings
{
    [Min(0f)]
    public float _lowCutoff = 0f;
    [Min(0f)]
    public float _highCutoff = 9999f;
    [Min(0f)]
    public float _lengthScale = 512f; // k = 2pi / lengthScale -> smaller lengthScale means larger k values
};

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OceanController : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] public OceanSettings oceanSettings = new OceanSettings();
    [SerializeField] public WaveSettings waveSettings = new WaveSettings();

    [Header("Rendering")]
    public Material oceanMaterial; // assign in inspector

    [Header("Compute")]
    public ComputeShader H0kComputeShader;
    public ComputeShader TimeComputeShader;
    public ComputeShader FFTComputeShader;
    public ComputeShader WrapperComputeShader;

    [SerializeField] DebugTextureQuad debugQuad;

    WaveCascade cascade1;
    FFT _fft;
    float time = 0f;

    MeshFilter _meshFilter;
    MeshRenderer _meshRenderer;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        if (oceanMaterial != null)
        {
            _meshRenderer.sharedMaterial = oceanMaterial;
        }
    }

    void Start()
    {
        // 1. Build FFT + cascade
        _fft = new FFT(FFTComputeShader, oceanSettings._size);
        cascade1 = new WaveCascade(oceanSettings,
                                   waveSettings,
                                   _fft,
                                   H0kComputeShader,
                                   TimeComputeShader,
                                   WrapperComputeShader,
                                   debugQuad);

        // 2. Generate procedural grid mesh
        var gen = new OceanMeshGenerator();
        Mesh mesh = gen.GenerateGrid(oceanSettings._size, oceanSettings._spacing);
        mesh.name = "OceanOceanMesh";

        // 3. Assign to MeshFilter so MeshRenderer can draw it
        _meshFilter.sharedMesh = mesh;
        oceanMaterial.SetTexture("_HeightMap", cascade1.HeightMap);
        oceanMaterial.SetTexture("_NormalMap", cascade1.NormalMap);
    }

    void Update()
    {
        time += Time.deltaTime;
        cascade1.Update(time);
    }
}
