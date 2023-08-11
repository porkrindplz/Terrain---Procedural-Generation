using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;

public class SplineToTerrainHeight : MonoBehaviour
{
    [Header("Spline Data")]
    [SerializeField] Vector3 startPoint = new Vector3(240, 0, 0);
    [SerializeField] Vector3 endPoint;
    [SerializeField] int lowerOffsetRange, upperOffsetRange;
    [SerializeField] int points = 10;

    [Header("Secondary Spawn Location")]
    [SerializeField] float minRange = 0;
    [SerializeField] float maxRange = 10;
    [SerializeField][HideInInspector] float maxRangeCheck;
    [SerializeField][HideInInspector] float minRangeCheck;

    [Header("Terrain Data")]
    [SerializeField] int borderBuffer = 10;
    [SerializeField] int floorRadius = 10;
    [SerializeField] int smoothRadius = 30;
    [SerializeField] float floorHeight = 0.1f;
    [SerializeField] float maxHeight = 100;
    [SerializeField] InclineType inclineType = InclineType.Linear;

    [Header("References")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] Terrain terrain;
    [SerializeField] TerrainData terrainDataPrefab;
    [SerializeField] GameObject terrainPrefab;
    float[,] mesh;
    float secondaryStartT;
    enum InclineType { Linear, RaisedPath, CurveIn, CurveOut, }

    void OnValidate()
    {
        maxRangeCheck = 1;
        minRangeCheck = 0;
        if (maxRangeCheck < maxRange) maxRange = maxRangeCheck;
        if (minRangeCheck > minRange) minRange = minRangeCheck;

        if (minRange > maxRange) minRange = maxRange;
        if (maxRange < minRange) maxRange = minRange;
        if (terrain != null)
        {
            if (startPoint.x + TotalBuffer() > terrain.terrainData.size.x) startPoint.x = terrain.terrainData.size.x - terrain.transform.position.x - TotalBuffer();
            if (startPoint.x - TotalBuffer() < 0) startPoint.x = TotalBuffer() + terrain.transform.position.x;
            if (endPoint.x + TotalBuffer() > terrain.terrainData.size.x) startPoint.x = terrain.terrainData.size.x - terrain.transform.position.x - TotalBuffer();
            if (endPoint.x - TotalBuffer() < 0) startPoint.x = TotalBuffer() + terrain.transform.position.x;
            if (upperOffsetRange - lowerOffsetRange > terrain.terrainData.size.x - 2 * TotalBuffer())
            {
                lowerOffsetRange = TotalBuffer();
                upperOffsetRange = (int)terrain.terrainData.size.x - TotalBuffer();
            }
        }
        if (lowerOffsetRange > upperOffsetRange) lowerOffsetRange = upperOffsetRange;
        if (upperOffsetRange < lowerOffsetRange) upperOffsetRange = lowerOffsetRange;


    }
    int TotalBuffer()
    {
        return borderBuffer + floorRadius + smoothRadius;
    }
    int PathRadius()
    {
        return floorRadius + smoothRadius;
    }
    private void Awake()
    {
        terrain.terrainData = Instantiate(terrainDataPrefab, transform.position, Quaternion.identity);
        GetComponent<TerrainCollider>().terrainData = terrain.terrainData;
        if (GetComponent<SplineContainer>() != null) splineContainer = GetComponent<SplineContainer>();
        else splineContainer = new SplineContainer();
    }
    void Start()
    {
        secondaryStartT = RandomSecondaryStartPoint();
        var res = terrain.terrainData.heightmapResolution;
    }
    public void SetStartPoint(Vector3 newStartPoint)
    {
        startPoint = newStartPoint;
    }
    public void StartMapGeneration()
    {
        StartCoroutine(GenerateMap());
    }
    IEnumerator GenerateMap()
    {
        yield return StartCoroutine(SetBaseTerrainHeight());
        yield return StartCoroutine(GenerateSpline()); ;
        yield return StartCoroutine(SetTerrainHeightToSpline());
    }
    IEnumerator SetBaseTerrainHeight()
    {
        var res = terrain.terrainData.heightmapResolution;
        mesh = new float[res, res];
        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                mesh[x, y] = 1;
            }
        }
        terrain.terrainData.SetHeights(0, 0, mesh);
        yield return null;
    }
    IEnumerator GenerateSpline()
    {
        float currentTerrainY = startPoint.x;
        for (int i = 0; i < points; i++)
        {
            float terrainX = terrain.terrainData.size.x * i / (points - 1);

            int terrainYOffset = UnityEngine.Random.Range(lowerOffsetRange, upperOffsetRange);

            float terrainY = currentTerrainY + terrainYOffset;

            if (terrainY < 0 + borderBuffer + floorRadius + smoothRadius || terrainY > terrain.terrainData.size.x - borderBuffer - floorRadius - smoothRadius)
            {
                terrainYOffset *= -1;
                terrainY = currentTerrainY + terrainYOffset;
            }
            if (i > points / 2)
            {
                Mathf.Clamp(terrainY, startPoint.x + lowerOffsetRange * (points - i), startPoint.x + upperOffsetRange * (points - i));
            }
            Debug.Log(i);
            if (i == points - 1)
            {
                Debug.Log("END");
                terrainY = (int)startPoint.x;
            }
            currentTerrainY = terrainY;

            BezierKnot knot = new BezierKnot(new Unity.Mathematics.float3(terrainY, floorHeight, terrainX));
            splineContainer.Spline.Add(knot);

            splineContainer.Spline.SetAutoSmoothTension(i, .1f);
            splineContainer.Spline.SetTangentMode(TangentMode.AutoSmooth);
        }
        yield return null;
    }
    IEnumerator SetTerrainHeightToSpline()
    {
        var res = terrain.terrainData.heightmapResolution;
        var mesh = new float[res, res];
        for (int x = 0; x < res; x++)
        {
            float3 positionOnSpline = splineContainer.EvaluatePosition(0, (float)x / (float)res);

            int terrainY = (int)(((positionOnSpline.x - terrain.transform.position.x) / terrain.terrainData.size.x) * terrain.terrainData.heightmapResolution);
            for (int y = 0; y < res; y++)
            {
                if (y < terrainY - floorRadius - smoothRadius || y >= terrainY + floorRadius + smoothRadius)
                {
                    mesh[x, y] = 1;
                }
            }
            for (int yy = -floorRadius - smoothRadius; yy < floorRadius + smoothRadius; yy++)
            {
                if (yy >= floorRadius || yy <= -floorRadius)
                {
                    if (inclineType == InclineType.Linear)
                        mesh[x, terrainY + yy] = LinearIncline(floorHeight, (float)yy, floorRadius, smoothRadius);
                    if (inclineType == InclineType.RaisedPath)
                        mesh[x, terrainY + yy] = RaisedPath(floorHeight, (float)yy, floorRadius, smoothRadius);
                    if (inclineType == InclineType.CurveIn)
                        mesh[x, terrainY + yy] = CurveIn(floorHeight, (float)yy, floorRadius, smoothRadius);
                    if (inclineType == InclineType.CurveOut)
                        mesh[x, terrainY + yy] = CurveOut(floorHeight, (float)yy, floorRadius, smoothRadius);
                }
                else mesh[x, terrainY + yy] = 1 * floorHeight;
            }
        }
        terrain.terrainData.SetHeights(0, 0, mesh);
        yield return null;
    }
    IEnumerator SetTerrainHeightToMultipleSplines()
    {
        var res = terrain.terrainData.heightmapResolution;
        var mesh = new float[res, res];
        float3 posOnSpline = 0;
        float3 posOnSpline2 = 0;
        int terrainY = 0, terrainY2 = 0;
        for (int x = 0; x < res; x++)
        {
            List<float> baseSplineYValues = new List<float>();
            List<float> secondarySplineYValues = new List<float>();
            posOnSpline = splineContainer.EvaluatePosition(0, (float)x / (float)res);
            if (x >= splineContainer.EvaluatePosition(0, secondaryStartT).z)
            {
                posOnSpline2 = splineContainer.EvaluatePosition(1, ((float)x - secondaryStartT * res) / secondaryStartT * res);
            }
            terrainY = (int)(((posOnSpline.x - terrain.transform.position.x) / terrain.terrainData.size.x) * terrain.terrainData.heightmapResolution);
            if (math.all(posOnSpline2 != float3.zero))
                terrainY2 = (int)(((posOnSpline2.x - terrain.transform.position.x) / terrain.terrainData.size.x) * terrain.terrainData.heightmapResolution);
            for (int y = 0; y < res; y++)
            {
                if (y >= terrainY - PathRadius() && y <= terrainY + PathRadius())
                {
                    baseSplineYValues.Add(y);
                }
                if (terrainY2 != 0 && y >= terrainY2 - PathRadius() && y <= terrainY2 + PathRadius())
                {
                    secondarySplineYValues.Add(y);
                }
                else mesh[x, y] = 1;
            }
            for (int yy = -PathRadius(); yy < PathRadius(); yy++)
            {
                if (yy >= floorRadius || yy <= -floorRadius)
                {
                    if (inclineType == InclineType.Linear)
                        mesh[x, terrainY + yy] = LinearIncline(floorHeight, (float)yy, floorRadius, smoothRadius);
                    if (inclineType == InclineType.RaisedPath)
                        mesh[x, terrainY + yy] = RaisedPath(floorHeight, (float)yy, floorRadius, smoothRadius);
                    if (inclineType == InclineType.CurveIn)
                        mesh[x, terrainY + yy] = CurveIn(floorHeight, (float)yy, floorRadius, smoothRadius);
                    if (inclineType == InclineType.CurveOut)
                        mesh[x, terrainY + yy] = CurveOut(floorHeight, (float)yy, floorRadius, smoothRadius);
                }
                else mesh[x, terrainY + yy] = 1 * floorHeight;
            }
        }
        terrain.terrainData.SetHeights(0, 0, mesh);
        yield return null;
    }
    IEnumerator GenerateSecondarySpline()
    {
        float t = secondaryStartT;
        Spline baseSpline = splineContainer.Spline;
        splineContainer.AddSpline();
        float3 startPosition = baseSpline.EvaluatePosition(t);
        Debug.Log("StartPos: " + startPosition);
        float avgBasePath = (startPoint.x + baseSpline.EvaluatePosition(1).x) / 2;
        bool spawnRight;
        int secondaryPoints = 5;
        if (avgBasePath <= terrain.terrainData.size.x / 2) spawnRight = true;
        else spawnRight = false;
        int lowerOffsetAdjustment = 0;
        int upperOffsetAdjustment = 0;
        float terrainX = startPosition.z;

        float terrainY = startPosition.x;
        float currentTerrainY = 0;
        for (int i = 0; i < secondaryPoints; i++)
        {

            if (i != 0)
            {
                terrainX = startPosition.z + (terrain.terrainData.size.x - startPosition.z) * i / (secondaryPoints - 1);

                if (spawnRight && currentTerrainY + lowerOffsetRange < baseSpline.EvaluatePosition(t + i / points).x)
                    lowerOffsetAdjustment = (int)baseSpline.EvaluatePosition(t + i / points).x + (2 * TotalBuffer());
                if (!spawnRight && currentTerrainY + upperOffsetRange > baseSpline.EvaluatePosition(t + i / points).x)
                    upperOffsetAdjustment = (int)baseSpline.EvaluatePosition(t + i / points).x - (2 * TotalBuffer());
                int terrainYOffset = UnityEngine.Random.Range(lowerOffsetRange + lowerOffsetAdjustment, upperOffsetRange);
                terrainY = currentTerrainY + terrainYOffset;

                if (terrainY < 0 + TotalBuffer() || terrainY > terrain.terrainData.size.x - TotalBuffer())
                {
                    terrainYOffset *= -1;
                    terrainY = currentTerrainY + terrainYOffset;
                }
            }
            Debug.Log("terrainX: " + terrainX);

            currentTerrainY = terrainY;
            Debug.Log("CurrentTerrainY at " + i + ": " + terrainY);

            BezierKnot knot = new BezierKnot(new Unity.Mathematics.float3(terrainY, floorHeight, terrainX));
            Debug.Log("Knot: " + knot.Position);
            splineContainer.Splines[1].Add(knot);
            Debug.Log("Knot added");
            splineContainer.Splines[1].SetAutoSmoothTension(i, .25f);
            splineContainer.Splines[1].SetTangentMode(TangentMode.AutoSmooth);
        }
        yield return null;
    }
    float RandomSecondaryStartPoint()
    {
        return UnityEngine.Random.Range(minRange, maxRange);
    }
    void SetAdditionalTerrainHeightToSpline(int startPos)
    {
        // Debug.Log("StartPos: " + startPos);
        var res = terrain.terrainData.heightmapResolution;
        var newMesh = new float[res, res];
        // for (int x = 0; x < startPos; x++)
        // {
        //     for (int y = 0; y < terrain.terrainData.size.x; y++)
        //     {
        //         mesh[x, y] = terrain.terrainData.GetHeight(y, x);
        //     }
        // }
        // for (int x = startPos; x < res; x++)
        // {
        //     Debug.Log("x should not = 0: " + x);
        //     float3 positionOnSpline = splineContainer.EvaluatePosition(1, (float)x / (float)res);
        //     Debug.Log("x: " + x + " res: " + res + " x/res: " + (x / res));
        //     int terrainY = (int)(((positionOnSpline.x - terrain.transform.position.x) / terrain.terrainData.size.x) * terrain.terrainData.heightmapResolution);

        //     for (int yy = -floorRadius - smoothRadius; yy < floorRadius + smoothRadius; yy++)
        //     {
        //         if (yy >= floorRadius || yy <= -floorRadius)
        //         {
        //             if (inclineType == InclineType.Linear)
        //                 Debug.Log("terrainy + yy" + " " + terrainY + " " + yy);
        //             mesh[x, terrainY + yy] = LinearIncline(floorHeight, (float)yy, floorRadius, smoothRadius);
        //             if (inclineType == InclineType.RaisedPath)
        //                 mesh[x, terrainY + yy] = RaisedPath(floorHeight, (float)yy, floorRadius, smoothRadius);
        //             if (inclineType == InclineType.CurveIn)
        //                 mesh[x, terrainY + yy] = CurveIn(floorHeight, (float)yy, floorRadius, smoothRadius);
        //             if (inclineType == InclineType.CurveOut)
        //                 mesh[x, terrainY + yy] = CurveOut(floorHeight, (float)yy, floorRadius, smoothRadius);
        //         }
        //         else mesh[x, terrainY + yy] = 1 * floorHeight;
        //     }
        //     for (int y = 0; y < terrainY - floorRadius - smoothRadius; y++)
        //     {
        //         mesh[x, y] = terrain.terrainData.GetHeight(x, y);
        //     }
        //     for (int yyy = terrainY + floorRadius + smoothRadius; yyy < terrain.terrainData.size.x - 1; yyy++)
        //     {
        //         mesh[x, yyy] = terrain.terrainData.GetHeight(x, yyy);
        //     }
        // }

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
                mesh[x, y] = terrain.terrainData.GetHeight(y, x);
        }
        terrain.terrainData.SetHeights(0, 0, newMesh);
    }
    float LinearIncline(float depthMultiplier, float yOffset, float floorRadius, float smoothRadius)
    {
        float y = yOffset;

        return depthMultiplier + (1 - depthMultiplier) * (((float)Mathf.Abs(y) - floorRadius) / (float)smoothRadius);

    }
    float RaisedPath(float depthMultiplier, float yOffset, float floorRadius, float smoothRadius)
    {
        float y = yOffset;

        return (1 - depthMultiplier) * Mathf.Lerp(0, 1, (((float)Mathf.Abs(y) - floorRadius) / (float)smoothRadius));

    }
    float CurveOut(float depthMultiplier, float yOffset, float floorRadius, float smoothRadius)
    {
        float y = yOffset;

        return depthMultiplier + (1 - depthMultiplier) * Mathf.Sqrt((Mathf.Abs(y) - floorRadius) / smoothRadius);
    }
    float CurveIn(float depthMultiplier, float yOffset, float floorRadius, float smoothRadius)
    {
        float y = yOffset;

        return depthMultiplier + (1 - depthMultiplier) * Mathf.Pow(((Mathf.Abs(y) - floorRadius) / smoothRadius), 2);
    }
}
