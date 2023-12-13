using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
/// Creates a flat arrow mesh
/// </summary>
public class DynamicMeshGenerator: MonoBehaviour
{

    [SerializeField] private Material riverMaterial;
    [SerializeField] private Material oceanMaterial;
    public Mesh generateArrowMesh()
    {

        float stemLength=0.6f;
        float stemWidth=0.2f;
        float tipLength=0.4f;
        float tipWidth= 0.5f;
        List<Vector3> verticesList;
        List<int> trianglesList;
    
        Mesh mesh = new Mesh();
         //setup
        verticesList = new List<Vector3>();
        trianglesList = new List<int>();
 
        //stem setup
        Vector3 stemOrigin = new Vector3(0f,0f,-0.5f);
        float stemHalfWidth = stemWidth/2f;
        //Stem points
        verticesList.Add(stemOrigin+(stemHalfWidth*Vector3.right));
        verticesList.Add(stemOrigin+(stemHalfWidth*Vector3.left));
        verticesList.Add(verticesList[0]+(stemLength*Vector3.forward));
        verticesList.Add(verticesList[1]+(stemLength*Vector3.forward));
 
        //Stem triangles
        trianglesList.Add(0);
        trianglesList.Add(1);
        trianglesList.Add(3);
 
        trianglesList.Add(0);
        trianglesList.Add(3);
        trianglesList.Add(2);
        
        //tip setup
        Vector3 tipOrigin = stemLength*Vector3.forward - new Vector3(0f,0f,0.5f);
        float tipHalfWidth = tipWidth/2;
 
        //tip points
        verticesList.Add(tipOrigin+(tipHalfWidth*Vector3.left));
        verticesList.Add(tipOrigin+(tipHalfWidth*Vector3.right));
        verticesList.Add(tipOrigin+(tipLength*Vector3.forward));
 
        //tip triangle
        trianglesList.Add(4);
        trianglesList.Add(6);
        trianglesList.Add(5);
 
        //assign lists to mesh.
        mesh.vertices = verticesList.ToArray();
        mesh.triangles = trianglesList.ToArray();

        return mesh;
    }


    /// <summary>
    /// Creates a 2 triangle quad plane with variying vertice heights with the water material 
    /// </summary>
    /// <param name="locationX">The location on the X axis</param>
    /// <param name="locationY">The location on the Y axis (Height)</param>
    /// <param name="locationZ">The location on the Z axis</param>
    /// <param name="width">Width of the plane(X)</param>
    /// <param name="length">Height of the plane (Z)</param>
    /// <param name="useColider">Flag that determines if the water has a colider</param>
    /// <param name="isRiver">Flag to determine if the water is a river or ocean</param>
    public GameObject createCustomWaterPlane(float locationX, float locationY, float locationZ,float width, float length, bool useColider, bool isRiver, float[] cornerHeightArray)
    {
        // locationX = locationX - width/2f ;
        // locationY = locationY - height/2f;
        // locationZ = 0.1f ; // TODO CHANGE TO BE VARIABLE
        // Debug.Log(" Location X: "+ locationX+" location Z : "+ locationZ+" location Y: "+ locationY+" cornerHeightArray[0]: "+ cornerHeightArray[0]+" cornerHeightArray[1]: "+ cornerHeightArray[1]+" cornerHeightArray[2]: "+ cornerHeightArray[2]+" cornerHeightArray[3]: "+ cornerHeightArray[3]);


        GameObject plain = new GameObject("NAME"); //TODO CHANGE NAME TO DYNAMIC
        plain.transform.position = new Vector3(locationX,locationZ,locationY);
        MeshFilter mf = plain.AddComponent(typeof(MeshFilter)) as MeshFilter;
        MeshRenderer mr = plain.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

        Mesh plainMesh = new Mesh();
        plainMesh.vertices = new Vector3[]
        {
            new Vector3(-(width/2),cornerHeightArray[0],-(length/2)),
            new Vector3((width/2),cornerHeightArray[1],-(length/2)),
            new Vector3((width/2),cornerHeightArray[2],(length/2)),
            new Vector3(-(width/2),cornerHeightArray[3],(length/2)),
            new Vector3(0,0,0),

        };

        plainMesh.uv = new Vector2[]
        {
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1),
            new Vector2(0,0),
            new Vector2(0.5f,0.5f)
        };

        //plainMesh.triangles = new int[]{2,1,0,3,2,0};
        plainMesh.triangles = new int[]{3,2,4,2,1,4,1,0,4,0,3,4};

        mf.mesh = plainMesh;
        if(useColider == true)
        {
            (plain.AddComponent(typeof(MeshCollider)) as MeshCollider).sharedMesh = plainMesh;
        }
        
        if(isRiver)
        {
            mr.material = riverMaterial;
            mr.material.SetFloat("_WaveSpeed",0); // TODO REMOVE , Temporary while rivers dont have a custom material
        }
        else
        {
            mr.material = oceanMaterial;
        }
        plainMesh.RecalculateNormals();
        plainMesh.RecalculateBounds();

        return plain;
    }

    /// <summary>
    /// Creates a 2 triangle quad plane with the water material 
    /// </summary>
    /// <param name="locationX">The location on the X axis</param>
    /// <param name="locationY">The location on the Y axis (Height)</param>
    /// <param name="locationZ">The location on the Z axis</param>
    /// <param name="width">Width of the plane(X)</param>
    /// <param name="height">Height of the plane (Z)</param>
    /// <param name="useColider">Flag that determines if the water has a colider</param>
    /// <param name="isRiver">Flag to determine if the water is a river or ocean</param>
    public GameObject createWaterPlane(float locationX, float locationY, float locationZ,float width, float height, bool useColider, bool isRiver)
    {
        // locationX = locationX - width/2f ;
        // locationY = locationY - height/2f;
        // locationZ = 0.1f ; // TODO CHANGE TO BE VARIABLE

        GameObject plain = new GameObject("NAME"); //TODO CHANGE NAME TO DYNAMIC
        plain.transform.position = new Vector3(locationX,locationZ,locationY);
        MeshFilter mf = plain.AddComponent(typeof(MeshFilter)) as MeshFilter;
        MeshRenderer mr = plain.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

        Mesh plainMesh = new Mesh();
        plainMesh.vertices = new Vector3[]
        {
            new Vector3(-(width/2),0,-(height/2)),
            new Vector3((width/2),0,-(height/2)),
            new Vector3((width/2),0,(height/2)),
            new Vector3(-(width/2),0,(height/2))
        };

        plainMesh.uv = new Vector2[]
        {
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1),
            new Vector2(0,0)
        };

        plainMesh.triangles = new int[]{2,1,0,3,2,0};

        mf.mesh = plainMesh;
        if(useColider == true)
        {
            (plain.AddComponent(typeof(MeshCollider)) as MeshCollider).sharedMesh = plainMesh;
        }
        
        if(isRiver)
        {
            mr.material = riverMaterial;
            mr.material.SetFloat("_WaveSpeed",0); // TODO REMOVE , Temporary while rivers dont have a custom material
        }
        else
        {
            mr.material = oceanMaterial;
        }
        plainMesh.RecalculateNormals();
        plainMesh.RecalculateBounds();

        return plain;
    }

    /// <summary>
    /// Creates a 2 triangle quad plane with variying vertice heights with the water material 
    /// </summary>
    /// <param name="locationX">The location on the X axis</param>
    /// <param name="locationY">The location on the Y axis (Height)</param>
    /// <param name="locationZ">The location on the Z axis</param>
    /// <param name="width">Width of the plane(X)</param>
    /// <param name="length">Height of the plane (Z)</param>
    /// <param name="useColider">Flag that determines if the water has a colider</param>
    /// <param name="isRiver">Flag to determine if the water is a river or ocean</param>
    


    /// <summary>
    /// Combines multiple game objects into one (efectively creates a new meged mesh into a new object and deletes the existing objects)
    /// </summary>
    /// <param name="mergeObjects">List of all the objects whose meshes need to be merged</param>
    public GameObject combineMeshes(List<GameObject> mergeObjects )
    {
         CombineInstance[] combine = new CombineInstance[mergeObjects.Count];

        int i = 0;
        while (i < mergeObjects.Count)
        {
            combine[i].mesh = mergeObjects[i].GetComponent<MeshFilter>().sharedMesh;
            combine[i].transform = mergeObjects[i].transform.localToWorldMatrix;
            mergeObjects[i].gameObject.SetActive(false);

            i++;
        }
       
        Mesh mesh = new Mesh();
        mesh.CombineMeshes(combine);
        GameObject combinedMesh = new GameObject("CombinedWater"); // TODO make the name dynamic
        MeshFilter mf = combinedMesh.AddComponent(typeof(MeshFilter)) as MeshFilter;
        MeshRenderer mr = combinedMesh.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
        mr.material = mergeObjects[0].GetComponent<MeshRenderer>().material;

        combinedMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
        combinedMesh.gameObject.SetActive(true);

        for(int j =0;j<mergeObjects.Count;j++)
        {
            Destroy(mergeObjects[j]);
        }
        return combinedMesh;
    }
}
