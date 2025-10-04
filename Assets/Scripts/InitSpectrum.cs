using UnityEngine;

[System.Serializable]
public class InitSpectrum
{
    [System.Serializable]
    public struct InitSpectrumStruct
    {
        public int _kernelBuildKGrid;
        public int _kernelBuildH0Tex;
        public RenderTexture WaveTex; // wave texture
        public RenderTexture h0Tex2f;    // h0 texture
        public RenderTexture h0Tex4f;    // h0 texture + conjugate: h0(k), Conjugate(h0(-k))
    }


    public readonly OceanSettings _oceanSettings;
    public readonly WaveSettings _waveSettings;

    public readonly ComputeShader _compute_h0k; // assign BuildKGrid.compute
    public InitSpectrumStruct _data;

    public InitSpectrum(OceanSettings _oceanSettings, WaveSettings waveSettings, ComputeShader _compute_h0k)
    {
        this._oceanSettings = _oceanSettings;
        this._waveSettings = waveSettings;
        this._compute_h0k = _compute_h0k;

        SetupTextures();
        Generate();
    }


    public void SetupTextures()
    {
        _data._kernelBuildKGrid = _compute_h0k.FindKernel("BuildKGrid");
        _data._kernelBuildH0Tex = _compute_h0k.FindKernel("BuildH0Tex");

        _data.h0Tex2f = Utilities.CreateRenderTexture(_oceanSettings._size);
        _data.WaveTex = Utilities.CreateRenderTexture(_oceanSettings._size, RenderTextureFormat.ARGBFloat, false);
        _data.h0Tex4f = Utilities.CreateRenderTexture(_oceanSettings._size, RenderTextureFormat.ARGBFloat, false);
    }

    private void Generate()
    {
        OceanSettings _o = this._oceanSettings;
        WaveSettings _w = this._waveSettings;

        float fetch = _o._distanceToShore * 1000;
        //    Generate the h0 texture and the k grid texture
        _compute_h0k.SetInt("Size", _o._size);
        _compute_h0k.SetInt("OceanDepth", (int)_o._oceanDepth);
        _compute_h0k.SetFloat("L", _w._lengthScale);
        _compute_h0k.SetFloat("alpha", 0.076f * Mathf.Pow(_o._windSpeed * _o._windSpeed / (_o._GRAVITY * fetch), 0.22f));
        _compute_h0k.SetFloat("gamma", 3.3f);
        _compute_h0k.SetFloat("dispersion_peak", 22f * Mathf.Pow(_o._GRAVITY * _o._GRAVITY / (_o._windSpeed * fetch), 1f / 3f));
        _compute_h0k.SetFloat("LowCutoff", 0.0f);
        _compute_h0k.SetFloat("HighCutoff", _w._highCutoff);
        _compute_h0k.SetTexture(_data._kernelBuildKGrid, "InitGrid", _data.h0Tex2f);
        _compute_h0k.SetTexture(_data._kernelBuildKGrid, "WaveTex", _data.WaveTex);
        _compute_h0k.SetTexture(_data._kernelBuildKGrid, "NoiseTex", GenerateBoxMullerTexture());

        _compute_h0k.Dispatch(_data._kernelBuildKGrid, _o._size / 8, _o._size / 8, 1);
        SaveTextureToFileUtility.SaveRenderTextureToFile(_data.h0Tex2f, "Images/h0Tex2f", SaveTextureToFileUtility.SaveTextureFileFormat.PNG);

        // build final h0 texture with conjugates
        _compute_h0k.SetTexture(_data._kernelBuildH0Tex, "InitGrid", _data.h0Tex2f);
        _compute_h0k.SetTexture(_data._kernelBuildH0Tex, "h0Tex", _data.h0Tex4f);

        _compute_h0k.Dispatch(_data._kernelBuildH0Tex, _o._size / 8, _o._size / 8, 1);
        SaveTextureToFileUtility.SaveRenderTextureToFile(_data.h0Tex4f, "Images/h0Tex4f", SaveTextureToFileUtility.SaveTextureFileFormat.PNG);
        Debug.Log("H0 spectrum generated");
    }

    private Texture2D GenerateBoxMullerTexture()
    {
        int size = _oceanSettings._size;
        Texture2D noiseTex = new Texture2D(size, size, TextureFormat.RGFloat, false);
        noiseTex.filterMode = FilterMode.Point;
        noiseTex.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Box-Muller transform
                float u1 = Random.value;
                float u2 = Random.value;
                float mag = Mathf.Sqrt(-2.0f * Mathf.Log(u1));
                float z0 = mag * Mathf.Cos(2.0f * Mathf.PI * u2);
                float z1 = mag * Mathf.Sin(2.0f * Mathf.PI * u2);
                noiseTex.SetPixel(x, y, new Color(z0, z1, 0));
            }
        }
        noiseTex.Apply();
        return noiseTex;
    }

    public void Release()
    {
        if (_data.WaveTex != null)
            _data.WaveTex.Release();
        if (_data.h0Tex2f != null)
            _data.h0Tex2f.Release();
        if (_data.h0Tex4f != null)
            _data.h0Tex4f.Release();
    }

}
