using OpenTK.Mathematics;

namespace RERL.Loaders;

public static class MeshLoader
{
    public const string Cube = @"./Models/Cube.obj";
    public const string Icosahedron = @"./Models/Icosahedron.obj";
    public const string UVSphere = @"./Models/UVSphere.obj";

    public static RERL_Core.Mesh CubeMesh => ParseMesh(Cube);
    public static RERL_Core.Mesh IcosahedronMesh => ParseMesh(Icosahedron);
    public static RERL_Core.Mesh UVSphereMesh => ParseMesh(UVSphere);
    
    public static RERL_Core.Mesh ParseMesh(string filename)
    {
        if (filename.EndsWith(".obj")) {
            return ParseObj(filename);
        }

        throw new Exception($"ERR: Unsupported file format '{filename}'.");
    }
    
    public static RERL_Core.Mesh ParseObj(string objFilePath)
    {
        List<Vector3> tempVertexPositions = new();
        List<Vector3> tempVertexNormals = new();
        List<Vector2> tempVertexUVs = new();
        List<uint> tempIndices = new();
        List<RERL_Core.Vertex> tempVertices = new();

        foreach (var line in File.ReadLines(objFilePath)) {
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            switch (tokens[0]) {
                case "v":  //Vertex Position
                    tempVertexPositions.Add(new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])));
                    break;
                case "vn": //Vertex Normal
                    tempVertexNormals.Add(new Vector3(float.Parse(tokens[1]), float.Parse(tokens[2]), float.Parse(tokens[3])));
                    break;
                case "vt": //UV (TextureCoord)
                    tempVertexUVs.Add(new Vector2(float.Parse(tokens[1]), float.Parse(tokens[2])));
                    break;
                case "f":  //Face
                    for (int i = 2; i < tokens.Length - 0; i++)
                    {
                        uint i0 = AddVertex(tokens[1]);    
                        uint i1 = AddVertex(tokens[i]);    
                        uint i2 = AddVertex(tokens[i + 1]);

                        tempIndices.Add(i0);
                        tempIndices.Add(i1);
                        tempIndices.Add(i2);

                        if (i + 1 == tokens.Length - 1)
                            break;
                    }
                    break;
            }
        }
        
        return new RERL_Core.Mesh(tempVertices.ToArray(), tempIndices.ToArray());

        uint AddVertex(string faceToken)
        {
            string[] vertex = faceToken.Split('/');
            tempVertices.Add(new RERL_Core.Vertex(
                position: tempVertexPositions[ int.Parse(vertex[0]) - 1 ], 
                uv:       tempVertexUVs.Count > 0 ? tempVertexUVs[ int.Parse(vertex[1]) - 1 ] : Vector2.Zero, 
                normal:   tempVertexNormals.Count > 0 ? tempVertexNormals[ int.Parse(vertex[2]) - 1 ] : Vector3.Zero));
            return (uint)(tempVertices.Count - 1);
        }
    }
}