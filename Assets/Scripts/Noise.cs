using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Noise {

    // Function for 3D perlin values
    public static float Perlin3D(float x, float y, float z)
    {
        float AB = Mathf.PerlinNoise(x, y);
        float BC = Mathf.PerlinNoise(y, z);
        float AC = Mathf.PerlinNoise(x, z);

        float BA = Mathf.PerlinNoise(y, x);
        float CB = Mathf.PerlinNoise(z, y);
        float CA = Mathf.PerlinNoise(z, x);

        float ABC = AB + BC + AC + BA + CB + CA;
        return ABC / 6f;
    }

    // Function to generate 3D noise
    public static float[,,] Generate3DNoise(int xSize, int ySize, int zSize, int seed, float scale, int octaves, float persistance, float lacurnarity, Vector3 offset)
    {
        System.Random prng = new System.Random(seed);
        Vector3[] octaveOffsets = new Vector3[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) - offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            float offsetZ = prng.Next(-100000, 100000) - offset.z;
            octaveOffsets[i] = new Vector3(offsetX, offsetY, offsetZ);
        }

        float[,,] noiseMatrix = new float[xSize, ySize, zSize];

        if (scale <= 0)
            scale = 0.0001f;

        float maxNoiseDensity = float.MinValue;
        float minNoiseDensity = float.MaxValue;



        for (int y = 0; y < ySize; y++)
        {
            for (int z = 0; z < zSize; z++)
            {
                for (int x = 0; x < xSize; x++)
                {
                    float amplitude = 1;
                    float frequency = 1;
                    float noiseDensity = 0;

                    for (int i = 0; i < octaves; i++)
                    {
                        float sampleX = (x + octaveOffsets[i].x) / scale * frequency;
                        float sampleY = (y + octaveOffsets[i].y) / scale * frequency;
                        float sampleZ = (z + octaveOffsets[i].z) / scale * frequency;

                        float perlinValue = Perlin3D(sampleX, sampleY, sampleZ);

                        noiseDensity += perlinValue * amplitude;
                        amplitude *= persistance;
                        frequency *= lacurnarity;

                    }

                    if (noiseDensity > maxNoiseDensity)
                        maxNoiseDensity = noiseDensity;
                    if (noiseDensity < minNoiseDensity)
                        minNoiseDensity = noiseDensity;


                    noiseMatrix[x, y, z] = noiseDensity;
                }
            }
        }


        for (int y = 0; y < ySize; y++)
        {
            for (int z = 0; z < zSize; z++)
            {
                for (int x = 0; x < xSize; x++)
                {
                    noiseMatrix[x, y, z] = Mathf.InverseLerp(minNoiseDensity, maxNoiseDensity, noiseMatrix[x, y, z]) ;
                }
            }
        }

        //Debug.Log(minNoiseDensity);
        //Debug.Log(maxNoiseDensity);
        



        return noiseMatrix;
    }

    // Function to generate 2D noise ( not actually 2d noise, but it will create a terrain like surface rather than random perlin noise blobs )
    public static float[,,] Generate2DNoise(int xSize, int ySize, int zSize, int seed, float scale, int octaves, float persistance, float lacurnarity, Vector2 offset, float openMiddle)
    {
        float[,] noise2d = new float[xSize, zSize];
        float[,,] noise3d = new float[xSize, ySize, zSize];

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) - offset.x;
            float offsetZ = prng.Next(-100000, 100000) - offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetZ);
        }
        
        if (scale <= 0)
            scale = 0.0001f;


        float maxNoiseDensity = float.MinValue;
        float minNoiseDensity = float.MaxValue;

        for (int z = 0; z < zSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseDensity = 0;

                for (int i = 0; i < octaves; i++)
                {
                    //float sampleX = (x - halfX) / scale * frequency + octaveOffsets[i].x + offset.x;
                    //float sampleZ = (z - halfZ) / scale * frequency + octaveOffsets[i].y + offset.y;    // .y only because its the second parameter - this is actually associated to z axis

                    float sampleX = (x + octaveOffsets[i].x) / scale * frequency;
                    float sampleZ = (z + octaveOffsets[i].y) / scale * frequency;    // .y only because its the second parameter - this is actually associated to z axis

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ) * 2 - 1;

                    noiseDensity += perlinValue * amplitude;
                    amplitude *= persistance;
                    frequency *= lacurnarity;

                    

                }

                if (noiseDensity > maxNoiseDensity)
                    maxNoiseDensity = noiseDensity;
                if (noiseDensity < minNoiseDensity)
                    minNoiseDensity = noiseDensity;

                // setting the heightmap to be associated to the density
                noise2d[x, z] = noiseDensity;
            }
        }
        
        for (int z = 0; z < zSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                if (Vector2.Distance(new Vector2(x, z), new Vector2(xSize / 2, zSize / 2)) < openMiddle)
                {
                    noise2d[x, z] = minNoiseDensity;
                }

                noise2d[x, z] = Mathf.InverseLerp(minNoiseDensity, maxNoiseDensity, noise2d[x, z]) * .4f;
            }
        }

        // convert the height map into a 3d matrix
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                float density;
                int height = Mathf.RoundToInt(noise2d[x, z] * ySize);
                for (int y = 0; y < ySize; y++)
                {
                    if (y < height)
                        density = 0;                
                    else if (y == height)
                        density = Random.Range(.25f, 1);
                    else
                        density = 1;

                    noise3d[x, y, z] = density;
                }
            }
        }

        

        for (int y = 0; y < ySize; y++)
        {
            for (int z = 0; z < zSize; z++)
            {
                for (int x = 0; x < xSize; x++)
                {
                    if(y <= ySize / 8) 
                        noise3d[x, y, z] = 0;
                    
                }
            }
        }

        //Debug.Log(minNoiseDensity);
        //Debug.Log(maxNoiseDensity);

        return noise3d;
    }

    public static float[,,] GenerateHallFullField(int xSize, int ySize, int zSize)
    {
        float[,,] noise3d = new float[xSize, ySize, zSize];

        for (int y = ySize / 2; y < ySize; y++)
        {
            for (int z = 0; z < zSize; z++)
            {
                for (int x = 0; x < xSize; x++)
                {
                    noise3d[x, y, z] = 1;
                }
            }
        }

        for (int y = 0; y < ySize/2; y++)
        {
            for (int z = 0; z < zSize; z++)
            {
                for (int x = 0; x < xSize; x++)
                {
                    noise3d[x, y, z] = 0;
                }
            }
        }

        return noise3d;
    }
}
