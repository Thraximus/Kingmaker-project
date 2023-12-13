using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterGenerator : MonoBehaviour
{
    [SerializeField] private GameObject waterWaypointObject;

    public struct WaterGrid
    {
        public WaterSquare[][] squareMatrix;
        public WaterWaypoint startingWaypoint;
        public WaterSquare startWaypointSquare;
        public Vector3 originPosition;
    }
    public struct WaterSquare
    {
        public Vector3 position;
        public Vertice edgeDownLeft;
        public Vertice edgeDownRight;
        public Vertice edgeUpLeft;
        public Vertice edgeUpRight;
        public bool renderMesh;
        public Vector2 gridId;
    }
    public struct Vertice
    {
        public Vector3 position;
        public bool isCalculated;
    }
    public class WaterWaypoint
    {
        public WaterWaypoint previousWaypoint;
        public GameObject currentWaypoint;
        public WaterWaypoint nextWaypoint;
        public Vector3 lookVector;
        public Vector3 yLockedLookVector;

        public WaterWaypoint(WaterWaypoint inputPreviousWaypoint, GameObject inputCurrentWaypoint, WaterWaypoint inputNextWaypoint)
        {
            currentWaypoint = inputCurrentWaypoint;
            nextWaypoint = inputNextWaypoint;
            previousWaypoint = inputPreviousWaypoint;
        }
    }

    private Ray ray;

    private RaycastHit hit;

    DynamicMeshGenerator meshGenerator = null;
    TerrainEditor terrainEditor = null;

    private List<WaterWaypoint> waypointsForGeneration = new List<WaterWaypoint>();

    private WaterWaypoint pastWaterWaypoint = null;
    private WaterWaypoint currentWaterWaypoint = null;
    private float WaterWaypointHeight=-2f;

    // Start is called before the first frame update
    void Start()
    {
        meshGenerator =  GetComponent<DynamicMeshGenerator>();
        terrainEditor = GetComponent<TerrainEditor>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void generateWaterFromWaypoints()
    {
        if(waypointsForGeneration.Count > 0)
            {
                foreach(WaterGenerator.WaterWaypoint startWaypoint in waypointsForGeneration)
                {
                    if(startWaypoint.previousWaypoint == null)
                    {
                        WaterGenerator.WaterGrid waterGrid = generateVerticeArray(startWaypoint);
                        populateWaterGrid(ref waterGrid,0.1f);
                        CalculateWaterGridHeights(ref waterGrid);
                        GenerateWaterFromWaterGrid(waterGrid);
                    }
                }
                 ClearWaypoints();
            }
    }
    

    private void GenerateWaterFromWaterGrid(WaterGrid gridForGeneration)
    {
        List<GameObject> meshes = new List<GameObject>();
        for(int i=0; i<gridForGeneration.squareMatrix.Length;i++)
        {
            for(int j=0; j<gridForGeneration.squareMatrix[0].Length;j++)
            {
                if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                {
                    float[] heightArray = {gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y-gridForGeneration.squareMatrix[i][j].position.y,
                    gridForGeneration.squareMatrix[i][j].edgeDownRight.position.y-gridForGeneration.squareMatrix[i][j].position.y,
                    gridForGeneration.squareMatrix[i][j].edgeUpRight.position.y-gridForGeneration.squareMatrix[i][j].position.y,
                    gridForGeneration.squareMatrix[i][j].edgeUpLeft.position.y-gridForGeneration.squareMatrix[i][j].position.y};
                    
                    float[] heightArrayDefault = {0,0,0,0};

                    meshes.Add(meshGenerator.createCustomWaterPlane(gridForGeneration.squareMatrix[i][j].position.x,gridForGeneration.squareMatrix[i][j].position.z,gridForGeneration.squareMatrix[i][j].position.y,1,1,true,true,heightArray));
                }
            }
        }
        GameObject combinedWater = meshGenerator.combineMeshes(meshes);
    }

    public void enableWaypointPlacement()
    {
        pastWaterWaypoint= null;
        currentWaterWaypoint = null;
    }

    public void populateWaterGrid(ref WaterGrid gridForGeneration, float precision)
    {
        WaterWaypoint waypointForEvaluation = gridForGeneration.startingWaypoint;
        while(waypointForEvaluation.nextWaypoint != null)
        {
            float distanceBetweenWaypoints = Mathf.Ceil(Vector3.Distance(waypointForEvaluation.currentWaypoint.transform.position,waypointForEvaluation.nextWaypoint.currentWaypoint.transform.position));

            // Debug.Log("Angle between water line 1 and 2: ");
            // Debug.Log(Vector3.Angle(waypointForEvaluation.lookVector,waypointForEvaluation.nextWaypoint.lookVector));
            
            
            for(float j = 0; j< distanceBetweenWaypoints;j+=precision)
            {
                float posX = waypointForEvaluation.currentWaypoint.transform.position.x+j*(waypointForEvaluation.lookVector.normalized.x);
                float posY = waypointForEvaluation.currentWaypoint.transform.position.y+j*(waypointForEvaluation.lookVector.normalized.y);
                float posZ = waypointForEvaluation.currentWaypoint.transform.position.z+j*(waypointForEvaluation.lookVector.normalized.z);

                // Debug.Log("posY: " + posY);
                // Debug.Log("waypointForEvaluation.lookVector: " + waypointForEvaluation.lookVector);
                // Debug.Log("waypointForEvaluation.lookVector.normalized.y: " + waypointForEvaluation.lookVector.normalized.y);
                // Debug.Log("waypointForEvaluation.currentWaypoint.transform.position.y: " + waypointForEvaluation.currentWaypoint.transform.position.y);
                // Debug.Log("Y position at step: "+j+ " is : " + posY);
                // Debug.Log("posZ: " + posZ);

                // Vector3 startingWaypointSquarePos = new Vector3(Mathf.Round(posX-gridForGeneration.originPosition.x),0,Mathf.Round(posZ-gridForGeneration.originPosition.z));
                int squareIdX = Mathf.RoundToInt(posX-gridForGeneration.originPosition.x);
                int squareIdZ = Mathf.RoundToInt(posZ-gridForGeneration.originPosition.z);

                // if(gridForGeneration.squareMatrix[squareIdX][squareIdZ].renderMesh == false)
                // {
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].renderMesh = true;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeDownLeft.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeDownRight.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeUpLeft.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeUpRight.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].position.y = posY;
                // }

                RaycastHit distanceRay;
                int distanceToLeft = 0;
                int distanceToRight = 0;
                Vector3 traceOrigin = new Vector3(posX,posY,posZ);
                Vector3 rightLookVector;
                Vector3 leftLookVector;
                

                if(Physics.Raycast(traceOrigin, Quaternion.AngleAxis(-90, Vector3.up) * waypointForEvaluation.yLockedLookVector , out distanceRay))
                {
                    // Debug.Log("Left distance between points: "+ Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point)));
                    distanceToLeft = Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point));     
                    leftLookVector = distanceRay.point - traceOrigin;
                };

                if(Physics.Raycast(traceOrigin, Quaternion.AngleAxis(90, Vector3.up) * waypointForEvaluation.yLockedLookVector, out distanceRay))
                {
                    // Debug.Log("Right distance between points: "+ Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point)));
                    distanceToRight = Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point));
                    rightLookVector = distanceRay.point - traceOrigin;

                    // Debug.Log("right point: "+distanceRay.point);
                };
                // Debug.Log("Processing square X: "+(gridForGeneration.squareMatrix[squareIdX][squareIdZ].position.x)+" Y: "+ (gridForGeneration.squareMatrix[squareIdX][squareIdZ].position.z)+" "+ distanceToLeft+" "+distanceToRight);

                for (float i = precision; i< distanceToLeft;i+=precision)
                {
                    var lookVectorRotatedLeft = Quaternion.AngleAxis(-90, Vector3.up) * waypointForEvaluation.yLockedLookVector;
                    
                    float subPosX = posX+i*(lookVectorRotatedLeft.normalized.x);
                    float subPosY = posY+i*(lookVectorRotatedLeft.normalized.y);
                    float subPosZ = posZ+i*(lookVectorRotatedLeft.normalized.z);

                    int subSquareIdX = Mathf.RoundToInt(subPosX-gridForGeneration.originPosition.x);
                    int subSquareIdZ = Mathf.RoundToInt(subPosZ-gridForGeneration.originPosition.z);

                    if(gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh == false)
                    {
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh = true;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].position.y = posY;
                    }
                }
            
                for (float i = precision; i< distanceToRight;i+=precision)
                {

                    var lookVectorRotatedRight = Quaternion.AngleAxis(90, Vector3.up)*waypointForEvaluation.yLockedLookVector;



                    float subPosX = posX+i*(lookVectorRotatedRight.normalized.x);
                    float subPosY = posY+i*(lookVectorRotatedRight.normalized.y);
                    float subPosZ = posZ+i*(lookVectorRotatedRight.normalized.z);

                    int subSquareIdX = Mathf.RoundToInt(subPosX-gridForGeneration.originPosition.x);
                    int subSquareIdZ = Mathf.RoundToInt(subPosZ-gridForGeneration.originPosition.z);
                    

                    if(gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh == false)
                    {
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh = true;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].position.y = posY;

                    }
                }
                
            }

            if(waypointForEvaluation.nextWaypoint != null && waypointForEvaluation.previousWaypoint != null)
            {
                 // Calculate the two vectors representing the lines from pointA to pointB and from pointC to pointB.
                Vector3 vectorAB = (waypointForEvaluation.currentWaypoint.transform.position - waypointForEvaluation.previousWaypoint.currentWaypoint.transform.position).normalized;
                Vector3 vectorCB = (waypointForEvaluation.currentWaypoint.transform.position - waypointForEvaluation.nextWaypoint.currentWaypoint.transform.position).normalized;

                // Calculate the cross product to determine the rotation direction (clockwise or counter-clockwise).
                Vector3 cross = Vector3.Cross(vectorAB, vectorCB);
                float angle = Vector3.Angle(vectorAB, vectorCB) * (cross.y < 0 ? -1 : 1);

                // Find the central point where the lines connect.
                Vector3 centralPoint = waypointForEvaluation.currentWaypoint.transform.position;

                // Cast rays from the central point for every degree of the angle.
                for (float degree = 0f; Mathf.Abs(degree) <= Mathf.Abs(angle); degree += Mathf.Sign(angle))
                {
                    // Calculate the direction of the ray.
                    Quaternion rotation = Quaternion.AngleAxis(degree, Vector3.up);
                    Vector3 rayDirection = rotation * vectorAB;

                    // Set the y-component of the rayDirection to 0 to cast rays in the XZ plane.
                    rayDirection.y = 0f;

                    // Cast the ray and measure the distance to the terrain.
                    Ray ray = new Ray(new Vector3(centralPoint.x, waypointForEvaluation.currentWaypoint.transform.position.y, centralPoint.z), rayDirection);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit))
                    {
                        // Debug.DrawRay(ray.origin, rayDirection * hit.distance, Color.green, 60f);
                        // Debug.Log("Hit distance: " + hit.distance);
                        for (float i = precision; i< hit.distance;i+=precision)
                        {
                            var lookVectorRotatedLeft = rayDirection;
                            
                            float subPosX = waypointForEvaluation.currentWaypoint.transform.position.x + i*(lookVectorRotatedLeft.normalized.x);
                            float subPosY = waypointForEvaluation.currentWaypoint.transform.position.y + i*(lookVectorRotatedLeft.normalized.y);
                            float subPosZ = waypointForEvaluation.currentWaypoint.transform.position.z + i*(lookVectorRotatedLeft.normalized.z);

                            int subSquareIdX = Mathf.RoundToInt(subPosX-gridForGeneration.originPosition.x);
                            int subSquareIdZ = Mathf.RoundToInt(subPosZ-gridForGeneration.originPosition.z);

                            if(gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh == false)
                            {
                                gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh = true;
                                gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownLeft.position.y = waypointForEvaluation.currentWaypoint.transform.position.y;
                                gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownRight.position.y = waypointForEvaluation.currentWaypoint.transform.position.y;
                                gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpLeft.position.y = waypointForEvaluation.currentWaypoint.transform.position.y;
                                gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpRight.position.y = waypointForEvaluation.currentWaypoint.transform.position.y;
                                gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].position.y = waypointForEvaluation.currentWaypoint.transform.position.y;
                            }
                        }
                    }
                }
            }

            
           


            waypointForEvaluation = waypointForEvaluation.nextWaypoint;
        }

    }

     private void CalculateWaterGridHeights(ref WaterGrid gridForGeneration)
    {
        int vertexCount;
        WaterSquare[] squaresForProcessing;
        float vertexHeightValue;
        bool[] squareActiveStatus;
        for(int i=0; i<gridForGeneration.squareMatrix.Length;i++)
        {
            for(int j=0; j<gridForGeneration.squareMatrix[0].Length;j++)
            {
                if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                {
                    if(gridForGeneration.squareMatrix[i][j].edgeUpRight.isCalculated == false)
                    {
                        if(gridForGeneration.squareMatrix[i+1][j+1].renderMesh == false)
                        {
                            if(!((i+1)>gridForGeneration.squareMatrix.Length) && !((j+1)> gridForGeneration.squareMatrix[0].Length))
                            {
                                

                                vertexCount = 3;
                                squaresForProcessing = new WaterSquare[vertexCount];
                                squareActiveStatus = new bool[vertexCount];
                                squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                                if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                                {
                                    squareActiveStatus[0] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[0] = false; 
                                }
                                squaresForProcessing[1] = gridForGeneration.squareMatrix[i+1][j];
                                if(gridForGeneration.squareMatrix[i+1][j].renderMesh == true)
                                {
                                    squareActiveStatus[1] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[1] = false; 
                                }

                                squaresForProcessing[2] = gridForGeneration.squareMatrix[i][j+1];
                                if(gridForGeneration.squareMatrix[i][j+1].renderMesh == true)
                                {
                                    squareActiveStatus[2] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[2] = false; 
                                }

                                vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,true,true);

                                gridForGeneration.squareMatrix[i][j].edgeUpRight.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i+1][j].edgeUpLeft.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i][j+1].edgeDownRight.position.y = vertexHeightValue;


                                gridForGeneration.squareMatrix[i][j].edgeUpRight.isCalculated = true;
                                gridForGeneration.squareMatrix[i+1][j].edgeUpLeft.isCalculated = true;
                                gridForGeneration.squareMatrix[i][j+1].edgeDownRight.isCalculated = true;
                            }
                        }
                    }

                    if(gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated == false)
                    {                          
                        if(i-1 < 0)
                        {   
                            if(j-1>0)
                            {
                                vertexCount = 2;
                                squareActiveStatus = new bool[vertexCount];
                                squaresForProcessing = new WaterSquare[vertexCount];
                                squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                                squaresForProcessing[1] = gridForGeneration.squareMatrix[i][j-1];

                                if(gridForGeneration.squareMatrix[i][j].renderMesh)
                                {
                                    squareActiveStatus[0] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[0] = false; 
                                }
                                if(gridForGeneration.squareMatrix[i][j-1].renderMesh)
                                {
                                    squareActiveStatus[1] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[1] = false; 
                                }

                                vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,true,false);

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.position.y = vertexHeightValue;

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated = true;
                                gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.isCalculated = true;
                            }
                        }
                        else if(j-1 <0)
                        {
                            if(i-1>0)
                            {
                                vertexCount = 2;
                                squaresForProcessing = new WaterSquare[vertexCount];
                                squareActiveStatus = new bool[vertexCount];
                                squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                                squaresForProcessing[1] = gridForGeneration.squareMatrix[i-1][j];

                                if(gridForGeneration.squareMatrix[i][j].renderMesh)
                                {
                                    squareActiveStatus[0] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[0] = false; 
                                }
                                if(gridForGeneration.squareMatrix[i-1][j].renderMesh)
                                {
                                    squareActiveStatus[1] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[1] = false; 
                                }

                                vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,false,false);

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i-1][j].edgeDownRight.position.y = vertexHeightValue;

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated = true;
                                gridForGeneration.squareMatrix[i-1][j].edgeDownRight.isCalculated = true;
                            }
                        }
                        else
                        {
                            vertexCount = 4;
                            squaresForProcessing = new WaterSquare[vertexCount];
                            squareActiveStatus = new bool[vertexCount];
                            squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                            if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                            {
                                squareActiveStatus[0] = true; 
                            }
                            else
                            {
                                squareActiveStatus[0] = false; 
                            }
                            squaresForProcessing[1] = gridForGeneration.squareMatrix[i][j-1];
                            if(gridForGeneration.squareMatrix[i][j-1].renderMesh == true)
                            {
                                squareActiveStatus[1] = true; 
                            }
                            else
                            {
                                squareActiveStatus[1] = false; 
                            }
                            squaresForProcessing[2] = gridForGeneration.squareMatrix[i-1][j-1];
                            if(gridForGeneration.squareMatrix[i-1][j-1].renderMesh == true)
                            {
                                squareActiveStatus[2] = true; 
                            }
                            else
                            {
                                squareActiveStatus[2] = false; 
                            }
                            squaresForProcessing[3] = gridForGeneration.squareMatrix[i-1][j];
                            if(gridForGeneration.squareMatrix[i-1][j].renderMesh == true)
                            {
                                squareActiveStatus[3] = true; 
                            }
                            else
                            {
                                squareActiveStatus[3] = false; 
                            }

                            vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,true,false);

                            gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y = vertexHeightValue;
                            gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.position.y = vertexHeightValue;
                            gridForGeneration.squareMatrix[i-1][j-1].edgeUpRight.position.y = vertexHeightValue;
                            gridForGeneration.squareMatrix[i-1][j].edgeDownRight.position.y = vertexHeightValue;


                            gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated = true;
                            gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.isCalculated = true;
                            gridForGeneration.squareMatrix[i-1][j-1].edgeUpRight.isCalculated = true;
                            gridForGeneration.squareMatrix[i-1][j].edgeDownRight.isCalculated = true;
                        }
                        
                    }
                }
            }
        }
    }

    private float averageEdgeHeightPerVertex( WaterSquare[] squaresForProcessing, bool[] squareActiveStatus, bool vertical, bool topRightStitch)
    {
        float finalHeight=0f;
        int numOfSquares=0;
        if(topRightStitch == true)
        {
            if(squaresForProcessing.Length == 3)
            {
                if (squareActiveStatus[0] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[0].edgeUpRight.position.y;
                }
                if (squareActiveStatus[1] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                }
                if (squareActiveStatus[2] == true )
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[2].edgeDownRight.position.y;
                }
                finalHeight = finalHeight/numOfSquares;
            }
            else
            {
                if(vertical)
                {
                    if (squareActiveStatus[0] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeUpRight.position.y;
                    }
                    if (squareActiveStatus[1] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeDownRight.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
                else
                {
                    if (squareActiveStatus[0] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeUpRight.position.y;
                    }
                    if (squareActiveStatus[1] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
            }
        }
        else
        {
            if(squaresForProcessing.Length == 4)
            {
                if (squareActiveStatus[0] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[0].edgeDownLeft.position.y;
                }
                if (squareActiveStatus[1] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                }
                if (squareActiveStatus[2] == true )
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[2].edgeUpRight.position.y;
                }
                if (squareActiveStatus[3] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[3].edgeDownRight.position.y;
                }
                finalHeight = finalHeight/numOfSquares;
            }
            else
            {
                if(vertical)
                {
                    if (squareActiveStatus[0])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeDownLeft.position.y;
                    }
                    if (squareActiveStatus[1])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
                else
                {
                    if (squareActiveStatus[0])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeDownLeft.position.y;
                    }
                    if (squareActiveStatus[1])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeDownRight.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
            }
        }
        

        return finalHeight;
    }


    private float[][] averageEdgeHeight( WaterSquare squareForAverageCentral, WaterSquare squareForAverageOutside, int xOffset, int zOffset)
    {
        float cornerOne = 0f;
        float cornerTwo = 0f;

        float[][] returnHeightTotal= new float[2][];

        switch((xOffset, zOffset,squareForAverageOutside.renderMesh))
        {
            case(-1,-1,true):
                // bottom left / top right 
                cornerOne = (squareForAverageOutside.edgeUpRight.position.y+squareForAverageCentral.edgeDownLeft.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(cornerOne,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = MakeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,cornerOne,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;


            case(0,-1,true):
                // bottom right, bottom left / top right, top left

                cornerOne = (squareForAverageOutside.edgeUpRight.position.y+squareForAverageCentral.edgeDownRight.position.y) / 2f;
                cornerTwo = (squareForAverageOutside.edgeUpLeft.position.y+squareForAverageCentral.edgeDownLeft.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(cornerTwo,cornerOne,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = MakeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,cornerOne,cornerTwo);
                
                return returnHeightTotal;


            case(1,-1,true):
                // bottom right /top left 
                cornerOne = (squareForAverageOutside.edgeUpLeft.position.y+squareForAverageCentral.edgeDownRight.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,cornerOne,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = MakeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,cornerOne);

                return returnHeightTotal;


            case(-1,0,true):
                // top left bottom left / top right bottom right

                cornerOne = (squareForAverageOutside.edgeUpRight.position.y+squareForAverageCentral.edgeUpLeft.position.y) /2f;
                cornerTwo = (squareForAverageOutside.edgeDownRight.position.y+squareForAverageCentral.edgeDownLeft.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(cornerTwo,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,cornerOne);
                returnHeightTotal[1] = MakeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,cornerTwo,cornerOne,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;

            case(0,0,true):
                // ignore
            break;

            case(1,0,true):
                // top right bottom right / top left bottom left

                cornerOne = (squareForAverageOutside.edgeUpLeft.position.y+squareForAverageCentral.edgeUpRight.position.y) /2f;
                cornerTwo = (squareForAverageOutside.edgeDownLeft.position.y+squareForAverageCentral.edgeDownRight.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,cornerTwo,cornerOne,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = MakeHieghtArray(cornerTwo,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,cornerOne);

                return returnHeightTotal;

            case(-1,1,true):
                //  top left / bottom right 

                cornerOne = (squareForAverageOutside.edgeDownRight.position.y+squareForAverageCentral.edgeUpLeft.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,cornerOne);
                returnHeightTotal[1] = MakeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,cornerOne,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;

            case(0,1,true):
                // top left top right / bottom left  bottom right 

                cornerOne = (squareForAverageOutside.edgeDownLeft.position.y+squareForAverageCentral.edgeUpLeft.position.y) /2f;
                cornerTwo = (squareForAverageOutside.edgeDownRight.position.y+squareForAverageCentral.edgeUpRight.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,cornerTwo,cornerOne);
                returnHeightTotal[1] = MakeHieghtArray(cornerOne,cornerTwo,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;

            case(1,1,true):
                //  top right / bottom left

                cornerOne = (squareForAverageOutside.edgeDownLeft.position.y+squareForAverageCentral.edgeUpRight.position.y) /2f;

                returnHeightTotal[0] = MakeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,cornerOne,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = MakeHieghtArray(cornerOne,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;
            default:
                Debug.Log("Borked!!!");

                returnHeightTotal[0] = MakeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = MakeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;
        }
        return null;
    }


    private float[] MakeHieghtArray(float cornerOne, float cornerTwo, float cornerThree, float cornerFour)
    {
        float[] returnArray = new float[4];
        returnArray[0] = cornerTwo;
        returnArray[1] = cornerOne;
        returnArray[2] = cornerThree;
        returnArray[3] = cornerFour;

        return returnArray;
    }

    private WaterGrid generateVerticeArray(WaterWaypoint waypoint)
    {
        float maxWidth = waypoint.currentWaypoint.transform.position.x;
        float minWidth = waypoint.currentWaypoint.transform.position.x;
        float maxLength = waypoint.currentWaypoint.transform.position.z;
        float minLength = waypoint.currentWaypoint.transform.position.z;

        WaterGrid returnGrid = new WaterGrid();
        returnGrid.startingWaypoint = waypoint;

        WaterWaypoint minXWaypoint = waypoint;
        WaterWaypoint minYWaypoint = waypoint;

        while (waypoint != null)
        {
            // Debug.Log("Processing waypoint: ");
            if(maxWidth < waypoint.currentWaypoint.transform.position.x)
            {
                maxWidth = waypoint.currentWaypoint.transform.position.x;
            }
            else if (minWidth > waypoint.currentWaypoint.transform.position.x)
            {
                minWidth = waypoint.currentWaypoint.transform.position.x;
                minXWaypoint = waypoint;
            }

            if(maxLength < waypoint.currentWaypoint.transform.position.z)
            {
                maxLength = waypoint.currentWaypoint.transform.position.z;
            }
            else if (minLength > waypoint.currentWaypoint.transform.position.z)
            {
                minYWaypoint = waypoint;
                minLength = waypoint.currentWaypoint.transform.position.z;
            }
            
            waypoint = waypoint.nextWaypoint;
        }
       
        int arrayX = Mathf.CeilToInt(maxWidth)-Mathf.CeilToInt(minWidth)+40;
        int arrayY = Mathf.CeilToInt(maxLength)-Mathf.CeilToInt(minLength)+40;


        Vector3 gridStartingLocation = new Vector3(minXWaypoint.currentWaypoint.transform.position.x-20, 0, minYWaypoint.currentWaypoint.transform.position.z-20);
        Vector3 startingWaypointSquarePos = new Vector3(Mathf.Round(returnGrid.startingWaypoint.currentWaypoint.transform.position.x-gridStartingLocation.x),0,Mathf.Round(returnGrid.startingWaypoint.currentWaypoint.transform.position.z-gridStartingLocation.z));


        WaterSquare[][] squareMatrix = new WaterSquare[arrayX][];
        for (int i = 0; i < arrayX;i++)
        {
            squareMatrix[i] = new WaterSquare[arrayY];
            for (int j = 0; j < arrayY;j++)
            {
                squareMatrix[i][j] = new WaterSquare();
                squareMatrix[i][j].gridId = new Vector2(i,j);
                squareMatrix[i][j].position = new Vector3(gridStartingLocation.x+i,0,gridStartingLocation.z+j);
                squareMatrix[i][j].edgeDownLeft.position = new Vector3(squareMatrix[i][j].position.x-0.5f,0,squareMatrix[i][j].position.z-0.5f);
                squareMatrix[i][j].edgeDownRight.position = new Vector3(squareMatrix[i][j].position.x+0.5f,0,squareMatrix[i][j].position.z-0.5f);
                squareMatrix[i][j].edgeUpLeft.position = new Vector3(squareMatrix[i][j].position.x-0.5f,0,squareMatrix[i][j].position.z+0.5f);
                squareMatrix[i][j].edgeUpRight.position = new Vector3(squareMatrix[i][j].position.x+0.5f,0,squareMatrix[i][j].position.z+0.5f);
                squareMatrix[i][j].renderMesh = false;
            }
        }

        returnGrid.startWaypointSquare = squareMatrix[Mathf.RoundToInt(startingWaypointSquarePos.x)][Mathf.RoundToInt(startingWaypointSquarePos.z)];
        returnGrid.originPosition = gridStartingLocation;


        returnGrid.squareMatrix = squareMatrix;
        return returnGrid;
    }

    public void createWaterWaypoint()
    {
        currentWaterWaypoint = CreateWaypointSingle(WaterWaypointHeight);
            //AddToWaterWaypointUndoStack(currentWaterWaypoint); TODO REDO UNRO REDO
            // WaterWaypointHeight += 0.9f; TODO : just for debug purposes
            if(pastWaterWaypoint != null)
            {
                currentWaterWaypoint.previousWaypoint = pastWaterWaypoint;
                pastWaterWaypoint.nextWaypoint = currentWaterWaypoint;

                Vector3 lookPos = currentWaterWaypoint.currentWaypoint.transform.position - pastWaterWaypoint.currentWaypoint.transform.position;
                Quaternion newRotation = Quaternion.LookRotation(lookPos);
                pastWaterWaypoint.currentWaypoint.transform.rotation = newRotation;
                currentWaterWaypoint.currentWaypoint.transform.rotation = newRotation;

                currentWaterWaypoint.yLockedLookVector = new Vector3(lookPos.x,0,lookPos.z);
                pastWaterWaypoint.yLockedLookVector = new Vector3(lookPos.x,0,lookPos.z);
                currentWaterWaypoint.lookVector = lookPos;
                pastWaterWaypoint.lookVector = lookPos;

                LineRenderer lr = pastWaterWaypoint.currentWaypoint.AddComponent<LineRenderer>();
                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;

                lr.SetPosition(0, new Vector3(pastWaterWaypoint.currentWaypoint.transform.position.x,pastWaterWaypoint.currentWaypoint.transform.position.y,pastWaterWaypoint.currentWaypoint.transform.position.z));
                lr.SetPosition(1, new Vector3(currentWaterWaypoint.currentWaypoint.transform.position.x,currentWaterWaypoint.currentWaypoint.transform.position.y,currentWaterWaypoint.currentWaypoint.transform.position.z));
            }

            pastWaterWaypoint = currentWaterWaypoint;
    }

    public WaterWaypoint CreateWaypointSingle(float height) //TODO in dev
    {
        ray = Camera.main.ScreenPointToRay (Input.mousePosition);
        if (Physics.Raycast (ray, out hit)) 
        {
            float mouseX = hit.point.x;
            float mouseY = hit.point.y;
            float mouseZ = hit.point.z;

            // Debug.Log("x"+mouseX);
            // Debug.Log("y"+mouseY);
            // Debug.Log("z"+mouseZ);

            GameObject waterStartWaypoint = Instantiate(waterWaypointObject, new Vector3(0,0,0),Quaternion.identity);
            waterStartWaypoint.GetComponent<MeshFilter>().mesh = meshGenerator.generateArrowMesh();
            waterStartWaypoint.transform.position = new Vector3(mouseX,height,mouseZ);

            WaterWaypoint waypoint = new WaterWaypoint(null,waterStartWaypoint,null);
            // Debug.Log(waypoint.currentWaypoint);
            // Debug.Log(waypoint.nextWaypoint);
            waypointsForGeneration.Add(waypoint);
            return waypoint;
            
        }
        return null;
    }

    private void ClearWaypoints()
    {
        foreach(WaterWaypoint waypoint in waypointsForGeneration)
        {
            Destroy(waypoint.currentWaypoint);
        }
        waypointsForGeneration.Clear();
    }

}
