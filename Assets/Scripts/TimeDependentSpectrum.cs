using System;
using UnityEngine;

[System.Serializable]
public class TimeDependentSpectrum
{
    [System.Serializable]
    public struct TimeDependentSpectrumStruct
    {
        public int _kernelEvolveSpectrum;

        public RenderTexture WaveTex; // wave texture
        public RenderTexture h0Tex;    // h0 texture + conjugate: h0(k), Conjugate(h0(-k))
        public RenderTexture TimeFreqTex;    // texture for storing time-dependent frequencies
        public RenderTexture HeightMapTex; // height map after IFFT
    }
    public RenderTexture HeightMapRT => _data.HeightMapTex;


    public readonly OceanSettings _oceanSettings;
    public readonly WaveSettings _waveSettings;

    public readonly ComputeShader _compute;
    public readonly ComputeShader _fftCompute;
    FFT _fft;
    TimeDependentSpectrumStruct _data;

    public TimeDependentSpectrum(OceanSettings _oceanSettings, ComputeShader _compute, ComputeShader _fftCompute, RenderTexture WaveTex, RenderTexture h0Tex)
    {
        this._oceanSettings = _oceanSettings;
        this._compute = _compute;
        this._fftCompute = _fftCompute;
        _data.WaveTex = WaveTex;
        _data.h0Tex = h0Tex;

        SetupTextures();
        Update();
    }


    public void SetupTextures()
    {
        _data._kernelEvolveSpectrum = _compute.FindKernel("EvolveSpectrum");
        _data.TimeFreqTex = Utilities.CreateRenderTexture(_oceanSettings._size, RenderTextureFormat.ARGBFloat, false);
        _data.HeightMapTex = Utilities.CreateRenderTexture(_oceanSettings._size, RenderTextureFormat.ARGBFloat, false);

        _fft = new FFT(_fftCompute, _oceanSettings._size);
    }

    private void Generate(float time = 0f)
    {
        _compute.SetFloat("Time", time);
        _compute.SetTexture(_data._kernelEvolveSpectrum, "h0Tex", _data.h0Tex);
        _compute.SetTexture(_data._kernelEvolveSpectrum, "WaveTex", _data.WaveTex);
        _compute.SetTexture(_data._kernelEvolveSpectrum, "TimeFreqTex", _data.TimeFreqTex);

        int groupSize = _oceanSettings._size / 8;
        _compute.Dispatch(_data._kernelEvolveSpectrum, groupSize, groupSize, 1);
    }

    void GenerateHeightMap()
    {
        _fft.IFFT(_data.TimeFreqTex, _data.HeightMapTex);
        // SaveTextureToFileUtility.SaveRenderTextureToFile(_data.HeightMapTex, "Images/HeightMapTex", SaveTextureToFileUtility.SaveTextureFileFormat.PNG);
    }

    public void Update(float time = 0f)
    {
        Generate(time);
        if (time == 0f)
        {
            SaveTextureToFileUtility.SaveRenderTextureToFile(_data.TimeFreqTex, "Images/TimeFreqTex", SaveTextureToFileUtility.SaveTextureFileFormat.PNG);
            Debug.Log("Time-dependent spectrum initialized");
        }
        GenerateHeightMap();

        if (time == 0f)
        {
            SaveTextureToFileUtility.SaveRenderTextureToFile(_data.HeightMapTex, "Images/HeightMapTex", SaveTextureToFileUtility.SaveTextureFileFormat.PNG);
            Debug.Log("Initial height map generated");
        }
    }

    public void Release()
    {
        if (_data.WaveTex != null) _data.WaveTex.Release();
        if (_data.h0Tex != null) _data.h0Tex.Release();
        if (_data.TimeFreqTex != null) _data.TimeFreqTex.Release();
    }
}
