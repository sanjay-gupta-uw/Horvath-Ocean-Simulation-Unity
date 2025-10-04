using UnityEngine;

public class OceanMeshGenerator
{
    public Mesh GenerateGrid(int N, float spacing)
    {
        if (N < 2) N = 2;

        int vertCount = N * N;
        Vector3[] verts = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        // UVs are mapped from [0,1] range
        Vector2[] uvs = new Vector2[vertCount];

        int idx = 0;
        float half = (N - 1) * spacing * 0.5f;
        for (int z = 0; z < N; z++)
        {
            for (int x = 0; x < N; x++)
            {
                // shift grid to be centered at origin
                float vx = x * spacing - half;
                float vz = z * spacing - half;
                verts[idx] = new Vector3(vx, 0f, vz);
                normals[idx] = Vector3.up;
                uvs[idx] = new Vector2((float)x / (N - 1), (float)z / (N - 1));
                idx++;
            }
        }

        int quadCount = (N - 1) * (N - 1);
        int[] tris = new int[quadCount * 6];
        int t = 0;
        for (int z = 0; z < N - 1; z++)
        {
            for (int x = 0; x < N - 1; x++)
            {
                int v0 = z * N + x;
                int v1 = v0 + 1;
                int v2 = v0 + N;
                int v3 = v2 + 1;

                // Triangle 1
                tris[t++] = v0;
                tris[t++] = v2;
                tris[t++] = v1;

                // Triangle 2
                tris[t++] = v1;
                tris[t++] = v2;
                tris[t++] = v3;
            }
        }

        Mesh m = new Mesh();
        // with 16-bit indices, max 65535 vertices -> use 32-bit if more
        m.indexFormat = (vertCount > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.vertices = verts;
        m.normals = normals;
        m.uv = uvs;
        m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }
}
