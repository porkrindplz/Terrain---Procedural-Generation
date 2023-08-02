using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainEditor : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] float multiplier = .001f;
    [SerializeField] int flatRadius = 10;
    [SerializeField] int smoothRadius = 30;
    [SerializeField] int borderBuffer = 10;
    [SerializeField] InclineType inclineType = InclineType.Linear;

    enum InclineType { Linear, Lerp }

    private void Start()
    {
        terrain = GetComponent<Terrain>();
        var res = terrain.terrainData.heightmapResolution;

        var mesh = new float[res, res];
        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                mesh[x, y] = 1;
            }
        }
        terrain.terrainData.SetHeights(0, 0, mesh);


        int starty = Mathf.RoundToInt(res / 2);
        int currenty = starty;
        for (int x = 0; x < res; x++)
        {
            int yAdjuster = Random.Range(-1, 1);
            if (currenty + yAdjuster + flatRadius + smoothRadius + borderBuffer > res || currenty + yAdjuster < borderBuffer + flatRadius + smoothRadius)
                yAdjuster *= -1;

            currenty += yAdjuster;
            for (int yy = -flatRadius - smoothRadius; yy < flatRadius + smoothRadius; yy++)
            {
                Debug.Log(yy);
                Debug.Log((float)yy / (float)smoothRadius);
                if (yy >= flatRadius || yy <= -flatRadius)
                {
                    if (inclineType == InclineType.Linear)
                        mesh[x, currenty + yy] = LinearIncline(multiplier, (float)yy, flatRadius, smoothRadius);
                    if (inclineType == InclineType.Lerp)
                        mesh[x, currenty + yy] = LerpIncline(multiplier, (float)yy, flatRadius, smoothRadius);
                }
                else
                    mesh[x, currenty + yy] = 1 * multiplier;
            }
        }
        terrain.terrainData.SetHeights(0, 0, mesh);
    }

    float LinearIncline(float depthMultiplier, float yOffset, float flatRadius, float smoothRadius)
    {
        float y = yOffset;

        return (1 - depthMultiplier) * (((float)Mathf.Abs(y) - flatRadius) / (float)smoothRadius);

    }
    float LerpIncline(float depthMultiplier, float yOffset, float flatRadius, float smoothRadius)
    {
        float y = yOffset;

        return (1 - depthMultiplier) * Mathf.Lerp(0, 1, (((float)Mathf.Abs(y) - flatRadius) / (float)smoothRadius));

    }

}


