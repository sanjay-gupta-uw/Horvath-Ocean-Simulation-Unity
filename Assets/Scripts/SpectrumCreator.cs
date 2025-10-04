// using System;
// using Unity.Mathematics;
// using UnityEngine;

// public class SpectrumCreator
// {
//     const float G = 9.81f;
//     [SerializeField] private OceanSettings _oceanSettings;
//     private float gamma = 3.3f;
//     private float alpha; // Phillips spectrum constant  
//     private float w_p;  // peak frequency

//     // K grid: kx, ky, kmag
//     private float[] kx;
//     private float[] ky;

//     private float[,] spectrumValues;
//     private float[,] amplitudeValues;
//     public ComputeShader computeShader;

//     public SpectrumCreator(OceanSettings oceanSettings)
//     {
//         int kernelHandle = computeShader.FindKernel("InitSpectrum");
//         Debug.Log("Kernel Handle: " + kernelHandle);

//         _oceanSettings = oceanSettings;

//         alpha = 0.076f * math.pow(math.pow(_oceanSettings.U, 2) / (G * _oceanSettings.Fetch), 0.22f);
//         w_p = 22 * math.pow(G, 2) / (_oceanSettings.U * _oceanSettings.Fetch);

//         // initialize kx, ky to size N
//         int N = _oceanSettings.N;
//         kx = new float[N];
//         ky = new float[N];

//         // initialize spectrumValues to size NxN
//         spectrumValues = new float[N, N];
//         amplitudeValues = new float[N, N];

//         // BUILD K-GRID
//         // Given N and L, build the kx, ky, and kmag arrays
//         // Then, for each pixel in the N x N grid, compute the dispersion relation w(k) to get the angular frequency, w (omega)
//         // Then, compute the Phillips spectrum for each k vector where the phillips spectrum is related to the wave vector through the dispersion relation
//         BuildKArrays();
//         BuildSpectrum();
//         Debug.Log("Spectrum Created Successfully!");
//         ComputeAmplitudes();
//         Debug.Log("Amplitudes Computed Successfully!");
//     }

//     private void ComputeAmplitudes()
//     {
//         float dk = 2.0f * Mathf.PI / _oceanSettings.L;
//         int N = _oceanSettings.N;
//         for (int i = 0; i < N; i++)
//         {
//             for (int j = 0; j < N; j++)
//             {
//                 amplitudeValues[i, j] = Mathf.Sqrt(2 * spectrumValues[i, j] * dk * dk);
//             }
//         }
//     }


//     private float[] Flatten2DArray(float[,] array2D)
//     {
//         int rows = array2D.GetLength(0);
//         int cols = array2D.GetLength(1);
//         float[] flatArray = new float[rows * cols];

//         for (int i = 0; i < rows; i++)
//         {
//             for (int j = 0; j < cols; j++)
//             {
//                 flatArray[i * cols + j] = array2D[i, j];
//             }
//         }

//         return flatArray;
//     }
//     public Texture2D GetSpectrumTexture()
//     {
//         return ToRFloatNormalized(amplitudeValues);
//     }

//     private Texture2D ToRFloatNormalized(float[,] data2D, Material mat = null)
//     {
//         int N = data2D.GetLength(0);
//         int M = data2D.GetLength(1);

//         // find max
//         float maxv = 0f;
//         for (int y = 0; y < M; y++)
//             for (int x = 0; x < N; x++)
//                 maxv = Mathf.Max(maxv, data2D[x, y]);

//         float inv = maxv > 0 ? 1f / maxv : 1f;

//         // flatten + normalize
//         var flat = new float[N * M];
//         for (int y = 0; y < M; y++)
//             for (int x = 0; x < N; x++)
//                 flat[y * N + x] = data2D[x, y] * inv;

//         var tex = new Texture2D(N, M, TextureFormat.RFloat, false, true);
//         tex.wrapMode = TextureWrapMode.Clamp;
//         tex.filterMode = FilterMode.Point;
//         tex.SetPixelData(flat, 0);
//         tex.Apply();

//         if (mat != null) mat.SetFloat("_KTexMax", maxv); // optional
//         return tex;
//     }


//     private void BuildSpectrum()
//     {
//         int N = _oceanSettings.N;
//         for (int i = 0; i < N; i++)
//         {
//             for (int j = 0; j < N; j++)
//             {
//                 float kx_val = kx[i];
//                 float ky_val = ky[j];
//                 float k_mag = math.sqrt(kx_val * kx_val + ky_val * ky_val);

//                 if (k_mag < 0.0001f)
//                 {
//                     spectrumValues[i, j] = 0.0f;
//                     continue;
//                 }

//                 float w = Dispersion(k_mag);
//                 float S = Spectrum_JONSWAP_(w);
//                 float attenuation = DepthAttenuationFromK(k_mag);

//                 spectrumValues[i, j] = S * attenuation;
//             }
//         }
//     }

//     private void BuildKArrays()
//     {
//         float dk = 2 * Mathf.PI / _oceanSettings.L;
//         float offset = _oceanSettings.N / 2;

//         for (int i = 0; i < _oceanSettings.N; i++)
//         {
//             float k_val = i - offset;
//             kx[i] = k_val * dk;
//             ky[i] = k_val * dk;
//         }
//     }


//     // Relate angular frequency w to wavenumber k 
//     // Take k as input and return w
//     private float Dispersion(float _k_)
//     {
//         // sigma is the surface tension in N/m
//         float sigma = 0.072f; // for water at room temperature
//                               // rho is the density of the fluid in kg/m^3
//         float rho = 1000.0f; // for water

//         float w_2 = (G * _k_ + sigma / rho * math.pow(_k_, 3)) * math.tanh(_k_ * _oceanSettings.Depth);
//         return math.sqrt(w_2);
//     }

//     // JONSWAP spectrum is dependent on parameter w
//     // Gives the expected frequency integrated over all directions theta for a given angular frequency w 
//     // w = 2 * pi / T, where T is the wave period
//     private float Spectrum_JONSWAP_(float w)
//     {
//         float sigma = w <= w_p ? 0.07f : 0.09f; // spectral width parameter
//         float r = math.exp(-math.pow((w - w_p) / (sigma * w_p), 2) / 2);

//         float S = alpha * math.pow(G, 2) / math.pow(w, 5) * math.exp(-1.25f * math.pow(w_p / w, 4)); //* math.pow(gamma, r);
//         return S;
//     }

//     // Attenuation function to reduce the spectrum at low frequencies
//     // private float SpectrumAttenuation(float w)
//     // {
//     //     float h = _oceanSettings.Depth;
//     //     float w_h = (float)(w * math.sqrt(h / G));

//     //     if (w_h <= 1.0f)
//     //     {
//     //         return 0.5f * w_h * w_h;
//     //     }

//     //     return 1.0f - 0.5f * math.pow(2.0f - w_h, 2);
//     // }
//     // Use tanh^2(kh)
//     private float DepthAttenuationFromK(float k)
//     {
//         float h = _oceanSettings.Depth;
//         float th = math.tanh(k * h);
//         return th * th;           // always in [0,1]
//     }

// }