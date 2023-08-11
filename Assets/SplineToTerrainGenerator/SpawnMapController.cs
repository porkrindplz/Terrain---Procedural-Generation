using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnMapController : MonoBehaviour
{
    /// <summary>Attach to the map manager. Works in conjunction with SplineToTerrainHeight.cs</summary>
    [SerializeField] GameObject mapPrefab;
    [SerializeField] Vector3 startPoint = new Vector3(250, 0, 0);
    GameObject currentMap = null;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnNextMap();
        }
    }
    void SpawnNextMap(Vector3? previousMapPos = null)
    {
        if (currentMap != null)
        {
            Terrain terrain = currentMap.GetComponent<Terrain>();
            Vector3 spawnPos = currentMap.transform.position + new Vector3(0, 0, terrain.terrainData.size.x);
            currentMap = Instantiate(mapPrefab, new Vector3(currentMap.transform.position.x, currentMap.transform.position.y, currentMap.transform.position.z + terrain.terrainData.size.x), Quaternion.identity);
            currentMap.GetComponent<SplineToTerrainHeight>().SetStartPoint(startPoint);
            currentMap.GetComponent<SplineToTerrainHeight>().StartMapGeneration();
        }
        else
        {
            if (previousMapPos == null)
            {
                previousMapPos = new Vector3(0, 0, 500);
            }
            currentMap = Instantiate(mapPrefab, (Vector3)previousMapPos, Quaternion.identity);
            currentMap.GetComponent<SplineToTerrainHeight>().SetStartPoint(startPoint);
            currentMap.GetComponent<SplineToTerrainHeight>().StartMapGeneration();
        }
    }
}
