using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine;
// https://graphics-programming.org/blog/fft-bloom-optimized-to-the-bone-in-nabla
public class FFT
{
    private ComputeShader _compute;  // assign FFT.compute

    // butterfly texture

    private RenderTexture _butterflyTex;

    private RenderTexture _pingPong1;
    private int _butterflyComputeKernel;
    private int _horizontalOperationKernel;
    private int _verticalOperationKernel;
    private int _copyToPingPong1Kernel;
    private int _permuteAndScaleKernel;
    private int _size;
    private int _log2Size;

    public FFT(ComputeShader fftCompute, int size)
    {
        _compute = fftCompute;
        _size = size;
        _log2Size = (int)Mathf.Log(size, 2);

        _butterflyComputeKernel = _compute.FindKernel("ComputeButterflyTexture");
        _horizontalOperationKernel = _compute.FindKernel("HorizontalOperation");
        _verticalOperationKernel = _compute.FindKernel("VerticalOperation");
        _copyToPingPong1Kernel = _compute.FindKernel("CopyToPingPong1");
        _permuteAndScaleKernel = _compute.FindKernel("PermuteAndScale");


        // find kernel handle

        // --- Allocate butterfly texture ---
        _butterflyTex = Utilities.CreateRenderTexture(_log2Size, _size, RenderTextureFormat.ARGBFloat, false);
        _pingPong1 = Utilities.CreateRenderTexture(_size, _size, RenderTextureFormat.ARGBFloat, false);

        // --- Dispatch compute to fill butterfly texture ---
        _compute.SetInt("Size", _size);
        _compute.SetInt("LogSize", _log2Size);
        _compute.SetTexture(_butterflyComputeKernel, "ButterflyTex", _butterflyTex);

        int groupsX = _log2Size;           // numthreads.x = 1
        int groupsY = (_size + 8 - 1) / 8; // numthreads.y = 8
        _compute.Dispatch(_butterflyComputeKernel, groupsX, groupsY, 1);


        SaveTextureToFileUtility.SaveRenderTextureToFile(_butterflyTex, "Images/butterflyTex", SaveTextureToFileUtility.SaveTextureFileFormat.PNG);
        Debug.Log("Saved butterfly texture to file.");

    }


    public void IFFT(RenderTexture input, RenderTexture output = null)
    {
        int pingPong = 0;
        RenderTexture pingPong0 = input;

        if (output != null)
        {
            // Copy to pingPong1
            _compute.SetTexture(_copyToPingPong1Kernel, "PingPong0", input);
            _compute.SetTexture(_copyToPingPong1Kernel, "PingPong1", output);

            _compute.Dispatch(_copyToPingPong1Kernel, _size / 8, _size / 8, 1);
            pingPong0 = output;
        }

        _compute.SetTexture(_horizontalOperationKernel, "ButterflyTex", _butterflyTex);
        _compute.SetTexture(_horizontalOperationKernel, "PingPong0", pingPong0);
        _compute.SetTexture(_horizontalOperationKernel, "PingPong1", _pingPong1);

        for (int stage = 0; stage < _log2Size; stage++)
        {
            _compute.SetInt("PingPong", pingPong);
            _compute.SetInt("Stage", stage);
            _compute.Dispatch(_horizontalOperationKernel, _size / 8, _size / 8, 1);
            pingPong = (pingPong + 1) % 2;
        }

        _compute.SetTexture(_verticalOperationKernel, "ButterflyTex", _butterflyTex);
        _compute.SetTexture(_verticalOperationKernel, "PingPong0", pingPong0);
        _compute.SetTexture(_verticalOperationKernel, "PingPong1", _pingPong1);

        for (int stage = 0; stage < _log2Size; stage++)
        {
            _compute.SetInt("PingPong", pingPong);
            _compute.SetInt("Stage", stage);
            _compute.Dispatch(_verticalOperationKernel, _size / 8, _size / 8, 1);
            pingPong = (pingPong + 1) % 2;
        }

        // Copy to pingPong1
        _compute.SetTexture(_copyToPingPong1Kernel, "PingPong0", pingPong0);
        _compute.SetTexture(_copyToPingPong1Kernel, "PingPong1", _pingPong1);
        _compute.Dispatch(_copyToPingPong1Kernel, _size / 8, _size / 8, 1);

        // Permute and scale
        _compute.SetInt("Size", _size);
        _compute.SetTexture(_permuteAndScaleKernel, "PingPong0", pingPong0);
        _compute.SetTexture(_permuteAndScaleKernel, "PingPong1", _pingPong1);
        _compute.Dispatch(_permuteAndScaleKernel, _size / 8, _size / 8, 1);
    }

    public RenderTexture GetButterflyTexture()
    {
        return _butterflyTex;
    }

    public RenderTexture GetPingPong1()
    {
        return _pingPong1;
    }
}