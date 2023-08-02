using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;



public class SplineToTerrainHeight : MonoBehaviour
{
    [SerializeField] Vector3 startPoint = new Vector3(240, 0, 0);
    [SerializeField] Vector3 endPoint;
    [SerializeField] int lowerOffsetRange, upperOffsetRange;
    [SerializeField] int borderBuffer = 10;
    [SerializeField] int flatRadius = 10;
    [SerializeField] int smoothRadius = 30;
    [SerializeField] float height = 0.1f;
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] int points = 10;
    [SerializeField] Terrain terrain;
    [SerializeField] InclineType inclineType = InclineType.Linear;

    enum InclineType { Linear, Lerp }

    // Start is called before the first frame update
    void Start()
    {
        splineContainer = GetComponent<SplineContainer>();
        var res = terrain.terrainData.heightmapResolution;
        SetBaseTerrainHeight();
        GenerateSpline();

        SetTerrainHeightToSpline();
    }

    void SetBaseTerrainHeight()
    {
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
    }

    void GenerateSpline()
    {
        float currentTerrainY = startPoint.x;
        for (int i = 0; i < points; i++)
        {
            float terrainX = terrain.terrainData.size.x * i / (points - 1);

            int terrainYOffset = UnityEngine.Random.Range(lowerOffsetRange, upperOffsetRange);

            float terrainY = currentTerrainY + terrainYOffset;

            if (terrainY < 0 + borderBuffer + flatRadius + smoothRadius || terrainY > terrain.terrainData.alphamapWidth - borderBuffer - flatRadius - smoothRadius)
            {
                terrainYOffset *= -1;
                terrainY = currentTerrainY + terrainYOffset;
            }

            if (i == points)
            {
                terrainY = (int)-endPoint.x;
            }
            currentTerrainY = terrainY;

            BezierKnot knot = new BezierKnot(new Unity.Mathematics.float3(terrainY, height, terrainX));
            splineContainer.Spline.Add(knot);

            splineContainer.Spline.SetAutoSmoothTension(i, .25f);
            splineContainer.Spline.SetTangentMode(TangentMode.AutoSmooth);
        }
    }
    void GenerateSecondarySpline()
    {

    }
    void SetTerrainHeightToSpline()
    {
        var res = terrain.terrainData.heightmapResolution;
        var mesh = new float[res, res];
        for (int x = 0; x < res; x++)
        {
            float3 positionOnSpline = splineContainer.EvaluatePosition(0, (float)x / (float)res);
            Debug.Log("x: " + x + " res: " + res + " x/res: " + (x / res));
            int terrainY = (int)(((positionOnSpline.x - terrain.transform.position.x) / terrain.terrainData.size.x) * terrain.terrainData.heightmapResolution);
            for (int y = 0; y < res; y++)
            {
                if (y < terrainY - flatRadius - smoothRadius || y >= terrainY + flatRadius + smoothRadius)
                {
                    mesh[x, y] = 1;
                }
            }
            for (int yy = -flatRadius - smoothRadius; yy < flatRadius + smoothRadius; yy++)
            {
                if (yy >= flatRadius || yy <= -flatRadius)
                {
                    if (inclineType == InclineType.Linear)
                        Debug.Log("terrainy + yy" + " " + terrainY + " " + yy);
                    mesh[x, terrainY + yy] = LinearIncline(height, (float)yy, flatRadius, smoothRadius);
                    if (inclineType == InclineType.Lerp)
                        mesh[x, terrainY + yy] = LerpIncline(height, (float)yy, flatRadius, smoothRadius);
                }
                else mesh[x, terrainY + yy] = 1 * height;
            }
        }
        terrain.terrainData.SetHeights(0, 0, mesh);
    }

    float LinearIncline(float depthMultiplier, float yOffset, float flatRadius, float smoothRadius)
    {
        float y = yOffset;

        return depthMultiplier + (1 - depthMultiplier) * (((float)Mathf.Abs(y) - flatRadius) / (float)smoothRadius);

    }
    float LerpIncline(float depthMultiplier, float yOffset, float flatRadius, float smoothRadius)
    {
        float y = yOffset;

        return (1 - depthMultiplier) * Mathf.Lerp(0, 1, (((float)Mathf.Abs(y) - flatRadius) / (float)smoothRadius));

    }

    // Update is called once per frame
    void Update()
    {

    }
}
