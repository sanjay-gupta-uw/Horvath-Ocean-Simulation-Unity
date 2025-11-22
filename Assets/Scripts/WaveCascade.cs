using UnityEngine;


[System.Serializable]
public class WaveCascade
{
    // public readonly WaveSettings _cascadeSettings;

    readonly int size;
    readonly WaveSettings waveSettings;
    readonly OceanSettings oceanSettings;

    readonly ComputeShader COMPUTE_INIT_SPECTRUM;
    readonly ComputeShader COMPUTE_TIME_DEPENDENT_SPECTRUM;
    readonly ComputeShader COMPUTE_WRAPPER;
    readonly FFT _fft;

    // render textures 
    readonly RenderTexture WAVE_MAP; // kx, ky, len(k), omega(k)
    readonly RenderTexture H_TEMP_MAP;
    readonly RenderTexture H0_MAP;
    readonly RenderTexture HEIGHT_MAP;
    readonly RenderTexture NORMAL_MAP;

    // Getters for external access
    public RenderTexture NormalMap => NORMAL_MAP;
    public RenderTexture HeightMap => HEIGHT_MAP;

    DebugTextureQuad debugQuad;


    public WaveCascade(OceanSettings oceanSettings,
                       WaveSettings waveSettings,
                       FFT fft,
                       ComputeShader initSpectrumCompute,
                       ComputeShader timeDependentSpectrumCompute,
                       ComputeShader wrapperCompute,
                       DebugTextureQuad debugQuad)
    {
        this.size = oceanSettings._size;
        this.oceanSettings = oceanSettings;
        this.waveSettings = waveSettings;
        this._fft = fft;
        this.COMPUTE_INIT_SPECTRUM = initSpectrumCompute;
        this.COMPUTE_TIME_DEPENDENT_SPECTRUM = timeDependentSpectrumCompute;
        this.COMPUTE_WRAPPER = wrapperCompute;
        this.debugQuad = debugQuad;

        // assign kernel IDs (points to function within compute shader files)
        KERNEL_BUILD_KGRID = COMPUTE_INIT_SPECTRUM.FindKernel("BuildKGrid");
        KERNEL_INIT_SPECTRUM = COMPUTE_INIT_SPECTRUM.FindKernel("BuildH0Tex");
        KERNEL_EVOLVE_SPECTRUM = COMPUTE_TIME_DEPENDENT_SPECTRUM.FindKernel("EvolveSpectrum");
        KERNEL_WRAPPER = COMPUTE_WRAPPER.FindKernel("ComputeWrapper");

        // setup render textures
        WAVE_MAP = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, false);
        H_TEMP_MAP = Utilities.CreateRenderTexture(size, RenderTextureFormat.RGFloat, false);
        H0_MAP = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, false);
        HEIGHT_MAP = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, false);
        NORMAL_MAP = Utilities.CreateRenderTexture(size, RenderTextureFormat.ARGBFloat, false);

        // calculate initial spectrum and wave data
        CalculateInitials();
    }


    private void CalculateInitials()
    {
        oceanSettings._distanceToShore = 1000 * oceanSettings._distanceToShore;
        // generate initial spectrum data
        COMPUTE_INIT_SPECTRUM.SetInt("Size", size);
        COMPUTE_INIT_SPECTRUM.SetInt("OceanDepth", (int)oceanSettings._oceanDepth); // placeholder -- serialize later
        COMPUTE_INIT_SPECTRUM.SetFloat("L", waveSettings._lengthScale); // placeholder -- serialize later

        COMPUTE_INIT_SPECTRUM.SetFloat("alpha", 0.076f * Mathf.Pow(oceanSettings._windSpeed * oceanSettings._windSpeed / (oceanSettings._GRAVITY * oceanSettings._distanceToShore), 0.22f));
        COMPUTE_INIT_SPECTRUM.SetFloat("gamma", 3.3f);
        COMPUTE_INIT_SPECTRUM.SetFloat("dispersion_peak", 22f * Mathf.Pow(oceanSettings._GRAVITY * oceanSettings._GRAVITY / (oceanSettings._windSpeed * oceanSettings._distanceToShore), 1f / 3f));

        COMPUTE_INIT_SPECTRUM.SetFloat("LowCutoff", waveSettings._lowCutoff);
        COMPUTE_INIT_SPECTRUM.SetFloat("HighCutoff", waveSettings._highCutoff);

        COMPUTE_INIT_SPECTRUM.SetTexture(KERNEL_BUILD_KGRID, "WaveTex", WAVE_MAP);
        COMPUTE_INIT_SPECTRUM.SetTexture(KERNEL_BUILD_KGRID, "hTemp", H_TEMP_MAP);
        COMPUTE_INIT_SPECTRUM.SetTexture(KERNEL_BUILD_KGRID, "NoiseTex", Utilities.GenerateBoxMullerTexture(size));
        COMPUTE_INIT_SPECTRUM.Dispatch(KERNEL_BUILD_KGRID, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);

        // now build h0 texture
        COMPUTE_INIT_SPECTRUM.SetTexture(KERNEL_INIT_SPECTRUM, "hTemp", H_TEMP_MAP);
        COMPUTE_INIT_SPECTRUM.SetTexture(KERNEL_INIT_SPECTRUM, "h0Tex", H0_MAP);
        COMPUTE_INIT_SPECTRUM.Dispatch(KERNEL_INIT_SPECTRUM, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);

        Debug.Log("Initial spectrum calculated.");
    }

    private void Generate(float time = 0f)
    {
        // time dependent spectrum evolution
        COMPUTE_TIME_DEPENDENT_SPECTRUM.SetFloat("Time", time);
        COMPUTE_TIME_DEPENDENT_SPECTRUM.SetTexture(KERNEL_EVOLVE_SPECTRUM, "h0Tex", H0_MAP);
        COMPUTE_TIME_DEPENDENT_SPECTRUM.SetTexture(KERNEL_EVOLVE_SPECTRUM, "WaveTex", WAVE_MAP);
        COMPUTE_TIME_DEPENDENT_SPECTRUM.SetTexture(KERNEL_EVOLVE_SPECTRUM, "HeightMap", HEIGHT_MAP);
        COMPUTE_TIME_DEPENDENT_SPECTRUM.SetTexture(KERNEL_EVOLVE_SPECTRUM, "NormalMap", NORMAL_MAP);
        COMPUTE_TIME_DEPENDENT_SPECTRUM.Dispatch(KERNEL_EVOLVE_SPECTRUM, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
    }


    void GenerateHeightMap()
    {
        // values live in x,y channels of HEIGHT_MAP and NORMAL_MAP after inverse FFT
        _fft.IFFT(HEIGHT_MAP);
        _fft.IFFT(NORMAL_MAP);

        // now apply merger from gas giant
        COMPUTE_WRAPPER.SetTexture(KERNEL_WRAPPER, "NormalMap", NORMAL_MAP);
        COMPUTE_WRAPPER.Dispatch(KERNEL_WRAPPER, size / LOCAL_WORK_GROUPS_X, size / LOCAL_WORK_GROUPS_Y, 1);
        // debugQuad.SetTexture(NORMAL_MAP);
        debugQuad.SetTexture(HEIGHT_MAP);

    }


    public void Update(float time = 0f)
    {
        Generate(time);
        GenerateHeightMap();
    }

    public void Release()
    {
        if (WAVE_MAP != null) WAVE_MAP.Release();
        if (H0_MAP != null) H0_MAP.Release();
        if (HEIGHT_MAP != null) HEIGHT_MAP.Release();
        if (NORMAL_MAP != null) NORMAL_MAP.Release();
    }

    // Kernel IDs
    readonly int KERNEL_BUILD_KGRID;
    readonly int KERNEL_EVOLVE_SPECTRUM;
    readonly int KERNEL_INIT_SPECTRUM;
    readonly int KERNEL_WRAPPER;

    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;
}
