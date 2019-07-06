using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class VoxelGenerator : MonoBehaviour {

    #region MAP GENERATION
    // Define the size of a given chunk in unity distances
    int xChunkLength;
    int yChunkLength;
    int zChunkLength;

    // Define the size of the map in chunks in the x,y, and z directions
    public int xChunks = 1;
    public int yChunks = 1;
    public int zChunks = 1;


    // Decide how many cubes fit inside the matrix
    public enum CUBE_RESOLUTION { INSANE = 8, HIGH = 4, MED = 2, LOW = 1 };
    public CUBE_RESOLUTION cubeResolution = CUBE_RESOLUTION.LOW;

    // Define the number of cubes in a chunk based on the chunk length and cube resolution
    int xChunkCubeLength;
    int yChunkCubeLength;
    int zChunkCubeLength;

    // Store the size of the grid of cubes - this is affected by the size of the chunk and the cube resolution - DEPRECIATED?
    int xMatrix;
    int yMatrix;
    int zMatrix;

    // Store the chunks in a 3d array
    ChunkMatrix[,,] chunks;

    // Determine the surface level of the objects
    [Range(0, 1)]
    public float surfaceLevel = .6f;

    // Game object to hold all the chunks (so the heirarchy doesnt get crowded)
    public GameObject mapHolder;

    // Game object to hold the mesh inside the chunk (so the heirarchy doesnt get crowded)
    public GameObject chunk_parent;

    // Cube game object to represent each voxel if draw cubes is on
    public GameObject voxel;

    // Matrix of Nodes that contain the noise data
    public Node[,,] nodeMatrix;


    #endregion

    #region NOISE    
    // Noise generation parameters
    int seed = 0;
    public float scale = .15f;
    [Range(0, 6)]
    public int octaves = 4;
    [Range(0, 1)]
    public float persistance = .5f;
    [Range(0, 10)]
    public float lacurnarity = 2;
    public Vector3 offset3D;
    public Vector2 offset2D;

    // This chooses how the noise will be generated
    // 2D is a height map based terrain, 3D is a random perlin noise terrain, half full is just half air half terrain
    public enum DIMENSIONS { TWO_DIMENSIONAL_NOISE, THREE_DIMENSIONAL_NOISE, HALF_FULL };
    public DIMENSIONS dim = DIMENSIONS.TWO_DIMENSIONAL_NOISE;

    public float openMiddle = 10f;

    // noise matrices holding:
    float[,,] mapNoise;     // all the noise for the entire play field
    float[,,] chunkNoise;   // the noise for the current chunk 
    #endregion
    
    #region MESH GENERATION 
    //list of indices associated to the vertices in the mesh
    //List<int> indices;

    // List of vertices stored for the mesh to update
    //List<Vector3> vertices;
    //int vertexCount;

    // Winding order to demonstrate direction of the triangles
    int[] windingOrder;

    // this is to define whether or not the interpolation between voxels will be used
    public bool softEdge = true;

    // information for the meshes
    public Material mesh_material;
    List<GameObject> meshes;
    #endregion

    #region Camera settings
    // information for the camera movement
    public float cameraSpeed = .3f;
    public float mouseSens = 10f;
    #endregion
    
    #region Optimizers

    Vector3[] emptyVertices;
    int[] emptyIndices;
    int[] emptyCurrentVoxel;
    int[] emptyHitIndices;
    int[] emptyChunkIndices;
    Vector3 distanceP1;
    Vector3 distanceP2;
    int[] emptyNoiseIndices;
    Vector3 emptyWallPos;
    #endregion

    #region TerrainEditing

    // Determine the value at which to create and destroy voxels on touch
    [Range(0, 1)]
    public float editValue = .25f;

    // Determine the value for the edit brush size
    [Range(0, 4)]
    public int radius = 1;
    #endregion

    // Initializers
    private void Initialize()
    {
        xChunkLength = 8 / (int)cubeResolution;
        yChunkLength = 8 / (int)cubeResolution;
        zChunkLength = 8 / (int)cubeResolution;

        scale *= (float)cubeResolution;         // scale the noise with the cube resolution
        openMiddle *= (float)xChunks * .1f;

        // need to add 2 because the noise for a hidden set of nodes needs to be generated for seemless transitions between chunks
        xChunkCubeLength = xChunkLength * (int)cubeResolution + 2;      // get the amount of nodes within the chunkMatrix
        yChunkCubeLength = yChunkLength * (int)cubeResolution + 2;
        zChunkCubeLength = zChunkLength * (int)cubeResolution + 2;

        // this is the size of the map to allot for the hidden nodes on each side of the edge chunks
        xMatrix = (xChunkCubeLength - 2) * xChunks + 2;
        yMatrix = (yChunkCubeLength - 2) * yChunks + 2;
        zMatrix = (zChunkCubeLength - 2) * zChunks + 2;
        
        mapNoise = new float[xMatrix, yMatrix, zMatrix];

        chunks = new ChunkMatrix[xChunks, yChunks, zChunks];
        windingOrder = new int[] { 2, 1, 0 };   // initialize the winding order

        // change the rate at which voxels are edited based on the resolution
        editValue *= (float)cubeResolution;
    }

    // Function for precaluclations and pre data storage
    void Optimizers()
    {
        emptyVertices = new Vector3[12];
        emptyCurrentVoxel = new int[3];
        emptyIndices = new int[3];
        emptyHitIndices = new int[3];
        emptyChunkIndices = new int[3];
        distanceP1 = new Vector3();
        distanceP2 = new Vector3();
        emptyNoiseIndices = new int[3];
        emptyWallPos = new Vector3();
    }

    // Fill the mapNoise matrix with all the necessary noise values for the playing field
    void CalculateAllNoise()
    {
        // If the noise is a 2d heightmap
        if (dim == DIMENSIONS.TWO_DIMENSIONAL_NOISE)
        {
            // generate all the noise for the chunks to reference and grab
            mapNoise = Noise.Generate2DNoise(xMatrix, yMatrix, zMatrix, seed, scale, octaves, persistance, lacurnarity, offset2D, openMiddle);
        }
        else if (dim == DIMENSIONS.THREE_DIMENSIONAL_NOISE)             // if the noise is a 3d map
        {
            mapNoise = Noise.Generate3DNoise(xMatrix, yMatrix, zMatrix, seed, scale, octaves, persistance, lacurnarity, offset3D);
        }
        else                                                            // if the noise is a half full map
        {
            mapNoise = Noise.GenerateHallFullField(xMatrix, yMatrix, zMatrix);
        }
    }

    // Function to fill the chunk matrix with the noise values
    //      x,y,z are chunk's indices
    void AddNoiseToChunk(ChunkMatrix currChunk)
    {
        int x = currChunk.chunksIndex[0];
        int y = currChunk.chunksIndex[1];
        int z = currChunk.chunksIndex[2];

        float currNoise = 0;

        // iterate through the noiseMap and add the noise to the current node in the chunk matrix
        for (int _y = 0, k = y * (yChunkCubeLength - 2); k < y * (yChunkCubeLength - 2) + yChunkCubeLength; k++, _y++)
        {
            for (int _z = 0, j = z * (zChunkCubeLength - 2); j < z * (zChunkCubeLength - 2) + zChunkCubeLength; j++, _z++)             
            {
                for (int _x = 0, i = x * (xChunkCubeLength - 2); i < x * (xChunkCubeLength - 2) + xChunkCubeLength; i++, _x++)
                {
                    // get the noise from the large noise map
                    currNoise = mapNoise[i, k, j];

                    // define the node's position in unity space'
                    //Debug.Log(currChunk.nodes[_x, _y, _z].position.x);
                    currChunk.nodes[_x, _y, _z].position.x = _x / (float)cubeResolution + x * xChunkLength;
                    currChunk.nodes[_x, _y, _z].position.y = _y / (float)cubeResolution + y * yChunkLength;
                    currChunk.nodes[_x, _y, _z].position.z = _z / (float)cubeResolution + z * zChunkLength;

                    // define the node's isovalue
                    currChunk.nodes[_x, _y, _z].isovalue = currNoise;

                    // define the node's location on the noise map
                    currChunk.nodes[_x, _y, _z].noiseIndices[0] = i;
                    currChunk.nodes[_x, _y, _z].noiseIndices[1] = k;
                    currChunk.nodes[_x, _y, _z].noiseIndices[2] = j;

                    // add the working matrix to the array of chunks
                    chunks[x, y, z] = currChunk;
                }
            }
        }
    }

    // Add all of the necessary nodes to the chunk matrix
    void AddNodesToChunk(ChunkMatrix currChunk)
    {
        for (int y = 0; y<yChunkCubeLength; y++)
        {
            for (int z = 0; z<zChunkCubeLength; z++)
            {
                for (int x = 0; x<xChunkCubeLength; x++)
                {
                    currChunk.nodes[x, y, z] = new Node(Vector3.zero, 0f);
}
            }
        }

        // place the current chunk into the chunk array
        chunks[currChunk.chunksIndex[0], currChunk.chunksIndex[1], currChunk.chunksIndex[2]] = currChunk;
    }

    // Create a single chunk's matrix of nodes
            // arguements are the chunks index, not their true positions
    void CreateChunkMatrix(int x, int y, int z)
    {
        // For heirarchy cleaning purposes I create multiple game objects to be nested within eachother
        ChunkMatrix currChunkMatrix = new ChunkMatrix(xChunkCubeLength, yChunkCubeLength, zChunkCubeLength);
        GameObject chunkObj = Instantiate(chunk_parent, Vector3.zero, Quaternion.identity);
        chunkObj.transform.parent = mapHolder.transform;
        chunkObj.tag = "Chunk";
        currChunkMatrix.chunkObj = chunkObj;

        Mesh mesh = new Mesh();
        mesh.SetVertices(currChunkMatrix.vertices);
        mesh.SetTriangles(currChunkMatrix.indices, 0);
        currChunkMatrix.chunkMesh = new GameObject("Mesh");
        currChunkMatrix.chunkMesh.transform.parent = currChunkMatrix.chunkObj.transform;
        currChunkMatrix.chunkMesh.AddComponent<MeshCollider>();
        currChunkMatrix.chunkMesh.AddComponent<MeshFilter>();
        currChunkMatrix.chunkMesh.AddComponent<MeshRenderer>();
        currChunkMatrix.chunkMesh.GetComponent<Renderer>().material = mesh_material;
        currChunkMatrix.chunkMesh.GetComponent<MeshFilter>().mesh = mesh;
        currChunkMatrix.chunkMesh.GetComponent<MeshCollider>().sharedMesh = mesh;
        currChunkMatrix.chunkMesh.tag = "Mesh";
        currChunkMatrix.chunkMesh.transform.localPosition = Vector3.zero;

        // this is like the local positions of the chunk with respect to other chunks
        currChunkMatrix.chunksIndex = new int [] { x, y, z };

        // this is the true position of the start of the chunk in unity units... or whatever
        currChunkMatrix.position = new Vector3(x * xChunkLength / (float)cubeResolution, y * yChunkLength / (float)cubeResolution, z * zChunkLength / (float)cubeResolution);

        AddNodesToChunk(currChunkMatrix);

        AddNoiseToChunk(currChunkMatrix);


    }
    
    // Draw a cube object
    void DrawCube(Vector3 pos, GameObject p)
    {
        GameObject clone = Instantiate(voxel, pos, Quaternion.identity);
        clone.transform.parent = p.transform;
        clone.tag = "Cube";
    }

    // Iterate through the specified map size and generate chunks 
    void GenerateChunks()
    {
        for (int y = 0; y < yChunks; y++)
        {
            for (int z = 0; z < zChunks; z++)
            {
                for (int x = 0; x < xChunks; x++)
                {
                    CreateChunkMatrix(x, y, z);
                }
            }
        }
    }
    

    // Function to check each corner of a given cube - this is the "March" of Marching Cubes
    //      The x,y,z arguments are the positions of nodes in a chunk's node matrix
    //      The currChunk argument is the nodes within the current chunk's matrix data
    ChunkMatrix PolygonizeCube(ChunkMatrix currChunk, int x, int y, int z)
    {
        Node[,,] nodes = currChunk.nodes;

        // get the type of cube dependant on the active vertices within it
        int cubeIndex = MarchingCubes.CheckVertices(currChunk, chunks, surfaceLevel, x, y, z);

        // if there were no active nodes in this cube, break the function
        if (cubeIndex == 0)
            return currChunk;

        // use the edge table to obtain which edges are active for this cube configuration
        int edges = MarchingCubes.edgeTable[cubeIndex];

        // array holding the positions of each edge vertex
        Vector3[] edgeVertices = emptyVertices;

        // value of which vertex is being worked on at the moment for the mesh creation
        int vert;

        // Find the vertices for the triangles in the cube - TODO: do the interpolation here   *****************************************
        for (int i = 0; i < edgeVertices.Length; i++)      // edge vertices length is the numver of edges in the cube
        {
            if ((edges & (1 << i)) != 0)                   // checks the boolean values from the edges table to see which edges are active
            {
                // start by assuming a hard edge - turn softEdge to false to create a more flat terrain
                float edgeOffset = 1 / (float)cubeResolution / 2f;

                // turning on softEdge will use the linear interpolation to smooth out the edges
                if (softEdge)
                {
                    float[] cube = MarchingCubes.GetIsovaluesOfCube(nodes, x, y, z);
                    edgeOffset = MarchingCubes.CalculateEdgeOffset(cube[MarchingCubes.edgeConnection[i, 0]], cube[MarchingCubes.edgeConnection[i, 1]], surfaceLevel) / (float)cubeResolution;
                }

                // The edgeVertices array holds the position for where the vertex will be drawn along the edge
                //      To figure this out, we need the position of the current node added to the 
                //          VertexOffset defined by the LUT that shows where the next edge is in comparison to this current one
                //          The vertex offset needs to be divided by the cube resolution because the distance between edges will change if there are more cubes in the same amount of space
                //          The edgeOffset is the linear interpolated value we found and it is basically a percentage of how far between the current and next node that the vertex should be placed
                //          The edgeOffset needs to be multiplied by the edgeDirection value in order to know which direction that the offset is in

                edgeVertices[i].x = nodes[x, y, z].position.x + MarchingCubes.vertexOffset[MarchingCubes.edgeConnection[i, 0], 0] / (float)cubeResolution + edgeOffset * MarchingCubes.edgeDirection[i, 0];
                edgeVertices[i].y = nodes[x, y, z].position.y + MarchingCubes.vertexOffset[MarchingCubes.edgeConnection[i, 0], 1] / (float)cubeResolution + edgeOffset * MarchingCubes.edgeDirection[i, 1];
                edgeVertices[i].z = nodes[x, y, z].position.z + MarchingCubes.vertexOffset[MarchingCubes.edgeConnection[i, 0], 2] / (float)cubeResolution + edgeOffset * MarchingCubes.edgeDirection[i, 2];
            }
        }

        // Create the triangles from these verts - there will be at most 5 triangles to be made per cube
        for (int i = 0; i < 5; i++)
        {
            if (MarchingCubes.triTable[cubeIndex, 3 * i] < 0)       // no triangles to be made
                break;

            // get the current number of verts in the mesh
            int vertexCount = currChunk.vertices.Count;


            for (int j = 0; j < 3; j++)                             // iterate through the triangle verts
            {
                vert = MarchingCubes.triTable[cubeIndex, 3 * i + j];
                currChunk.indices.Add(vertexCount + windingOrder[j]);
                currChunk.vertices.Add(edgeVertices[vert]);
            }
        }


        return currChunk;
    }

    // This function sets up the cube-by-cube marching within the polygonize function for a specific chunk
    void MarchThroughChunk(ChunkMatrix currChunk)
    {
        for (int y = 1; y < currChunk.nodes.GetLength(1) - 1; y++)
        {
            for (int x = 1; x < currChunk.nodes.GetLength(0) - 1; x++)
            {
                for (int z = 1; z < currChunk.nodes.GetLength(2) - 1; z++)
                {
                    currChunk = PolygonizeCube(currChunk, x, y, z);
                }
            }
        }
    }

    // This function is basically an initializing function that iterates through all the chunks and polygonizes everything to start it off
    void IterateThroughAllChunks()
    {
        for (int y = 0; y < chunks.GetLength(1); y++)
        {
            for (int z = 0; z < chunks.GetLength(2); z++)
            {
                for (int x = 0; x < chunks.GetLength(0); x++)
                {
                    MarchThroughChunk(chunks[x, y, z]);
                }
            }
        }
    }

    // (not used)
    // This function will generate Chunks within a specific size
    //      x y z are the indices in the chunks matrix for which chunk youre referencing not the chunks position
    //      the width component is how far from the current chunk you want to iterate through
    void IterateThroughSomeChunks(int x, int y, int z, int width)
    {
 
        if (width == 0)     // if the specific width is set to be 0
        {
            MarchThroughChunk(chunks[x, y, z]);
        }
        else if (Mathf.Pow((width*2 + 1), 2) < xChunks * yChunks * zChunks)     // if the width is small enough to
        {
            for (int k = y - width; k < y + width; k++)
            {
                for (int j = z - width; j < z + width; j++)
                {
                    for (int i = x - width; i < x + width; i++)
                    {
                        MarchThroughChunk(chunks[x, y, z]);
                    }
                }
            }
        }
        else                // getting here means that this function was called but the width was larger or the same size as the entire chunk matrix anyway, so just do them all
        {
            IterateThroughAllChunks();
        }
    }

    // Function to draw the mesh of a given chunk
    //      x y z are the indicies in the chunk matrix for which chunk youre referencing not the chunks position
    void DrawSingleMesh(ChunkMatrix currChunk)
    {
        MeshFilter filter = currChunk.chunkMesh.GetComponent<MeshFilter>();
        Mesh mesh = filter.mesh;
        filter.mesh = null;
        mesh.Clear();
        mesh.SetVertices(currChunk.vertices);
        mesh.SetTriangles(currChunk.indices, 0);
        mesh.RecalculateNormals();

        filter.mesh = mesh;
        MeshCollider collider = currChunk.chunkMesh.GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
    }

    // Function to iterate through chunks and draw their meshes
    void DrawAllMeshes()
    {
        for (int y = 0; y < chunks.GetLength(1); y++)
        {
            for (int z = 0; z < chunks.GetLength(2); z++)
            {
                for (int x = 0; x < chunks.GetLength(0); x++)
                {
                    DrawSingleMesh(chunks[x, y, z]);
                }
            }
        }
    }

    // Use this for initialization
    void Start () {
        Initialize();

        Optimizers();

        CalculateAllNoise();

        GenerateChunks();


        IterateThroughAllChunks();
        DrawAllMeshes();

    }

    // Function to turn a position vector3 to chunk indices to figure out which chunk the point resides
    int[] GetCurrentChunkFromPos(Vector3 pos)
    {
        int[] indices = emptyIndices;
        indices[0] = Mathf.FloorToInt(pos.x / xChunkLength);
        indices[1] = Mathf.FloorToInt(pos.y / yChunkLength);
        indices[2] = Mathf.FloorToInt(pos.z / zChunkLength);

        return indices;
    }

    // Function to turn a position vector3 into cube indices to figure out which voxel the point is closest to
    int[] GetCurrentCubeInChunkFromPos(Vector3 pos)
    {
        int[] currentVoxel = emptyCurrentVoxel;
        currentVoxel[0] = Mathf.FloorToInt(pos.x * (float)cubeResolution) % (xChunkCubeLength - 2);
        currentVoxel[1] = Mathf.FloorToInt(pos.y * (float)cubeResolution) % (yChunkCubeLength - 2);
        currentVoxel[2] = Mathf.FloorToInt(pos.z * (float)cubeResolution) % (zChunkCubeLength - 2);

        return currentVoxel;
    }

    // Function to set a specific chunk's specific node's isovalue
    //      also set the chunks around it 
    ChunkMatrix SetIsovalues(ChunkMatrix currChunk, int x, int y, int z, float isovalue, int width)
    {
        float fWidth = width / (float)cubeResolution;



        for (int k = 0; k < yChunkCubeLength; k++)
        {
            for (int j = 0; j < zChunkCubeLength; j++)
            {
                for (int i = 0; i < xChunkCubeLength; i++)
                {
                    float checkDst = Vector3.Distance(currChunk.nodes[i, k, j].position, currChunk.nodes[x, y, z].position);
                    // any nodes within the width of the "brush"
                    if (checkDst <= fWidth)
                    {
                        currChunk.nodes[i, k, j].isovalue += isovalue;
                    }
                }
            }
        }
        
        return currChunk;
    }

    // Function to change values in the noise map
    //      x,y,z are the nodes position inside the chunk
    void SetNoiseValues(ChunkMatrix currChunk, int x, int y, int z, float isovalue, int width)
    {
        float fWidth = width; // / (float)cubeResolution;

        // get the indices in the noise map for the supplied position
        int[] hitIndices = emptyHitIndices;
        hitIndices[0] = currChunk.nodes[x, y, z].noiseIndices[0];
        hitIndices[1] = currChunk.nodes[x, y, z].noiseIndices[1];
        hitIndices[2] = currChunk.nodes[x, y, z].noiseIndices[2];

        int[] chunkIndices = emptyChunkIndices;
        chunkIndices[0] = currChunk.chunksIndex[0];
        chunkIndices[1] = currChunk.chunksIndex[1];
        chunkIndices[2] = currChunk.chunksIndex[2];


        // if its in the middle of the chunks
        if (chunkIndices[0] > 0 && chunkIndices[0] < xChunks - 1 &&
            chunkIndices[1] > 0 && chunkIndices[1] < yChunks - 1 &&
            chunkIndices[2] > 0 && chunkIndices[2] < zChunks - 1)
        {
            
            for (int y_ = hitIndices[1] - width; y_ < hitIndices[1] + width; y_++)
            {
                for (int z_ = hitIndices[2] - width; z_ < hitIndices[2] + width; z_++)
                {
                    
                    for (int x_ = hitIndices[0] - width; x_ < hitIndices[0] + width; x_++)
                    {
                        // Create vectors to check the distances between the nodes within the range of the hit point
                        distanceP1.x = x_;
                        distanceP1.y = y_;
                        distanceP1.z = z_;
                        distanceP2.x = hitIndices[0];
                        distanceP2.y = hitIndices[1];
                        distanceP2.z = hitIndices[2];

                        float checkDst = Vector3.Distance(distanceP1, distanceP2);

                        if (checkDst <= fWidth)
                        {
                            mapNoise[x_, y_, z_] += isovalue;
                            if (mapNoise[x_, y_, z_] > 1)
                                mapNoise[x_, y_, z_] = 1;
                            else if (mapNoise[x_, y_, z_] < 0)
                                mapNoise[x_, y_, z_] = 0;
                        }
                    }
                }
            }
        }
        else
            Debug.Log(chunkIndices[0] + ", " + chunkIndices[1] + ", " + chunkIndices[2]);       // tells the console that it clicked on an edge rather than somewhere in the middle


    }

    // Function to reset the chunk's mesh data
    ChunkMatrix ResetChunkMeshData(ChunkMatrix hitChunk)
    {
        hitChunk.vertices.Clear();
        hitChunk.indices.Clear();
        hitChunk.vertexCount = 0;

        // reset the noise in the chunk
        AddNoiseToChunk(hitChunk);
            
        return hitChunk;
    }

    void DestroyChunk(ChunkMatrix currChunk)
    {
        // destroy the mesh object from the current chunk
        Destroy(currChunk.chunkMesh);
    }

    // Update the information for the chunks affected by the raycast
    void UpdateSurroundingChunks(int _x, int _y, int _z)
    {
        // as long as the current chunk is within the middle of the map and not a chunk on the edge
        if (_x > 0 && _y < xChunks &&
            _y > 0 && _y < yChunks &&
            _z > 0 && _z < zChunks)
        {
            for (int y = _y - 1; y < _y + 1; y++)
            {
                for (int z = _z - 1; z < _z + 1; z++)
                {
                    for (int x = _x - 1; x < _x + 1; x++)
                    {

                        // reset the chunk data
                        chunks[x,y,z] = ResetChunkMeshData(chunks[x, y, z]);

                        // polygonize the chunk
                        MarchThroughChunk(chunks[x, y, z]);

                        // draw the new mesh
                        DrawSingleMesh(chunks[x, y, z]);
                    }
                }
            }
        }
        else
        {           // this shouldnt happen
            // reset the chunk data
            chunks[_x, _y, _z] = ResetChunkMeshData(chunks[_x, _y, _z]);

            // polygonize the chunk
            MarchThroughChunk(chunks[_x, _y, _z]);

            // draw the new mesh
            DrawSingleMesh(chunks[_x, _y, _z]);
        }
        
    }


    // Function to move the camera around
    void CameraMovement()
    {
        // lock the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (Input.GetKey("escape"))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // get movement states
        float forwardMovement = Input.GetAxis("Vertical") * cameraSpeed;
        float sideMovement = Input.GetAxis("Horizontal") * cameraSpeed;
        if (Input.GetKey("space"))
        {
            Camera.main.transform.Translate(Vector3.up * cameraSpeed);
        }
        else if (Input.GetKey("left shift"))
        {
            Camera.main.transform.Translate(-Vector3.up * cameraSpeed);
        }

        // set movement
        Camera.main.transform.Translate(sideMovement, 0, forwardMovement);

        // get mouse look states
        float mouseX = Input.GetAxis("Mouse X") * mouseSens;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSens;

        Camera.main.transform.Rotate(Vector3.left * mouseY);
        Camera.main.transform.Rotate(Vector3.up * mouseX);

    }

    // I want to change this so that it just works on the current voxel rather than have the material fly at the user

    // Update is called once per frame
    void Update() {

        CameraMovement();
        
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            // dir variable is used to dictate whether the cubes are being deleted or added to
            int dir = 0;
            if (Input.GetMouseButton(0))
            {
                dir = 1;
            }
            else if (Input.GetMouseButton(1))
            {
                dir = -1;
            }
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 20f) && hit.transform.tag == "Mesh")
            {
                Vector3 hitPos = hit.point;
                int[] currentChunkIndices = GetCurrentChunkFromPos(hitPos);
                int[] currentVoxelIndices = GetCurrentCubeInChunkFromPos(hitPos);
                    

                // get the chunk that the ray hit
                ChunkMatrix hitChunk = chunks[currentChunkIndices[0], currentChunkIndices[1], currentChunkIndices[2]];

                // change the noise values around the affected point
                SetNoiseValues(hitChunk, currentVoxelIndices[0], currentVoxelIndices[1], currentVoxelIndices[2], dir * editValue, radius);



                // update the chunks around the affected mesh
                UpdateSurroundingChunks(currentChunkIndices[0], currentChunkIndices[1], currentChunkIndices[2]);
            }
        }



	}

    // This just clamps the values in the inspector so things dont get weird if a value of 0 chunks is supplied for some reason
    private void OnValidate()
    {
        if (xChunkLength <= 0)
            xChunkLength = 1;
        if (yChunkLength <= 0)
            yChunkLength = 1;
        if (zChunkLength <= 0)
            zChunkLength = 1;
        if (xChunks <= 0)
            xChunks = 1;
        if (yChunks <= 0)
            yChunks = 1;
        if (zChunks <= 0)
            zChunks = 1;
    }
}
