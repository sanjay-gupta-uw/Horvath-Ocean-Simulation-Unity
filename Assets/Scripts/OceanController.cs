using System;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UI;

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
public class OceanController : MonoBehaviour
{
    [Header("Grid")]

    [SerializeField] public OceanSettings oceanSettings = new OceanSettings();
    [SerializeField] public WaveSettings waveSettings = new WaveSettings();

    [Header("Compute")]
    public ComputeShader H0kComputeShader; // assign BuildKGrid.compute
    public ComputeShader TimeComputeShader; // assign SpectrumTime.compute
    public ComputeShader FFTComputeShader; // assign FFT.compute
    InitSpectrum initSpectrum;
    TimeDependentSpectrum timeDependentSpectrum;

    [SerializeField] bool _showHeightmap = false;
    MeshFilter _mf;
    MeshRenderer _mr;
    MaterialPropertyBlock _mpb;
    [SerializeField] Material _oceanMat; // has HLSL shader attached

    float time = 0f;

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mpb = new MaterialPropertyBlock();
    }
    void OnValidate()
    {
        // Only sanitize values here
        if (oceanSettings == null) oceanSettings = new OceanSettings();
        if (waveSettings == null) waveSettings = new WaveSettings();

        // clamp/snap
        // adapted from: http://stackoverflow.com/questions/31997707/rounding-value-to-nearest-power-of-two
        oceanSettings._size = Mathf.Max(8, Mathf.NextPowerOfTwo(oceanSettings._size));

        if (waveSettings._highCutoff < waveSettings._lowCutoff)
            waveSettings._highCutoff = waveSettings._lowCutoff;
        waveSettings._lengthScale = Mathf.Max(0.001f, waveSettings._lengthScale);


        if (!Application.isPlaying && oceanSettings._size > 1)
        {
            if (_mf == null)
            {
                _mf = GetComponent<MeshFilter>();
                if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();
            }

            var gen = new OceanMeshGenerator();
            Mesh mesh = gen.GenerateGrid(oceanSettings._size, oceanSettings._spacing);
            _mf.sharedMesh = mesh; // use sharedMesh in editor to avoid leaks --> use mesh when displacing at runtime
        }
    }
    void Start()
    {
        var gen = new OceanMeshGenerator();
        Mesh mesh = gen.GenerateGrid(oceanSettings._size, oceanSettings._spacing);

        // this line causes code to crash
        _mf.sharedMesh = mesh; // use sharedMesh in editor to avoid leaks --> use mesh when displacing at runtime

        if (H0kComputeShader == null) { Debug.LogError("Assign H0kComputeShader"); return; }
        initSpectrum = new InitSpectrum(oceanSettings, waveSettings, H0kComputeShader);
        timeDependentSpectrum = new TimeDependentSpectrum(oceanSettings, TimeComputeShader, FFTComputeShader, initSpectrum._data.WaveTex, initSpectrum._data.h0Tex4f);
        if (_showHeightmap)
        {
            var preview = FindFirstObjectByType<HeightmapPreview>();
            if (preview != null)
            {
                preview.SetRenderTexture(timeDependentSpectrum.HeightMapRT);
            }
        }
    }

    void OnDestroy() => initSpectrum?.Release();

    void Update()
    {
        time += Time.deltaTime;
        timeDependentSpectrum.Update(time);

        float tileLength = (oceanSettings._size - 1) * oceanSettings._spacing;
        _mr.GetPropertyBlock(_mpb);
        _mpb.SetTexture("_HeightTex", timeDependentSpectrum.HeightMapRT);    // RenderTexture
        _mpb.SetFloat("_TileLength", tileLength);            // meters per tile side
        _mpb.SetFloat("_Amplitude", 1.0f);                                 // meters per tex unit; tweak
        _mr.SetPropertyBlock(_mpb);
    }
}
