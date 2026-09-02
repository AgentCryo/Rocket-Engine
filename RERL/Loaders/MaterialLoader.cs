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
        public ulong NormalHandle = 0;
    }
    
    public static Material DefaultMaterial = new Material("_re_Default")
    {
        AlbedoHandle = 0,
        NormalHandle = 0,
        DoubleSided = true,
    };
    
    internal static Material LoadGltfMaterial(JsonElement material, JsonElement root, string gltfFilePath)
    {
        var mat = new Material(material.GetProperty("name").GetString());

        if (material.TryGetProperty("doubleSided", out var ds))
            mat.DoubleSided = ds.GetBoolean();

        // normalTexture lives on the material root, not inside pbrMetallicRoughness -
        // glTF spec keeps it separate from the metallic-roughness block.
        if (material.TryGetProperty("normalTexture", out var nrmTex))
            mat.NormalHandle = LoadMaterialTexture(nrmTex, root, gltfFilePath);

        if (!material.TryGetProperty("pbrMetallicRoughness", out var pbr)) return mat;
        
        if (pbr.TryGetProperty("baseColorFactor", out var bc))
            mat.BaseAlbedo = new Vector3(
                bc[0].GetSingle(),
                bc[1].GetSingle(),
                bc[2].GetSingle()
            );

        if (pbr.TryGetProperty("roughnessFactor", out var r)) mat.BaseRoughness = r.GetSingle();

        if (pbr.TryGetProperty("metallicFactor", out var m)) mat.BaseMetallic = m.GetSingle();

        if (pbr.TryGetProperty("baseColorTexture", out var ctx))
            mat.AlbedoHandle = LoadMaterialTexture(ctx, root, gltfFilePath);

        return mat;
    }

    static ulong LoadMaterialTexture(JsonElement textureRef, JsonElement root, string gltfFilePath)
    {
        int textureIndex = textureRef.GetProperty("index").GetInt32();
        int sourceIndex = root.GetProperty("textures")[textureIndex].GetProperty("source").GetInt32();
        string uri = root.GetProperty("images")[sourceIndex].GetProperty("uri").GetString();
        string path = Path.Combine(Path.GetDirectoryName(gltfFilePath), uri);

        return ImageLoader.GetBindlessHandle(ImageLoader.LoadTexture(path));
    }
}