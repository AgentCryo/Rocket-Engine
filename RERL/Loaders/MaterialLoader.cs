using System.Text.Json;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RCS;
using static RERL.RenderData;

namespace RERL.Loaders;

public static class MaterialLoader
{
    public class Material(String name) // For PBR
    {
        public string Name = name;
        public bool DoubleSided;
        public Vector3 BaseAlbedo = Vector3.One;
        public float BaseRoughness = 0.5f;
        public float BaseMetallic  = 0.15f;
        public ulong AlbedoHandle = 0;
    }
    
    public static Material DefaultMaterial = new Material("_re_Default")
    {
        AlbedoHandle = 0,
        DoubleSided = true,
    };
    
    public static Material LoadGltfMaterial(JsonElement material, JsonElement root, string gltfFilePath)
    {
        var mat = new Material(material.GetProperty("name").GetString());

        if (material.TryGetProperty("doubleSided", out var ds))
            mat.DoubleSided = ds.GetBoolean();

        if (!material.TryGetProperty("pbrMetallicRoughness", out var pbr)) return mat;
        
        if (pbr.TryGetProperty("baseColorFactor", out var bc))
        {
            mat.BaseAlbedo = new Vector3(
                bc[0].GetSingle(),
                bc[1].GetSingle(),
                bc[2].GetSingle()
            );
        }

        if (pbr.TryGetProperty("roughnessFactor", out var r))
            mat.BaseRoughness = r.GetSingle();

        if (pbr.TryGetProperty("metallicFactor", out var m))
            mat.BaseMetallic = m.GetSingle();

        if (pbr.TryGetProperty("baseColorTexture", out var ctx)) 
            mat.AlbedoHandle =  ImageLoader.GetBindlessHandle(ImageLoader.LoadTexture(Path.Combine(Path.GetDirectoryName(gltfFilePath), root.GetProperty("images")[root.GetProperty("textures")[ctx.GetProperty("index").GetInt32()].GetProperty("source").GetInt32()].GetProperty("uri").GetString())));

        return mat;
    }
}