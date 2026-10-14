using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.EnvironmentDetail
{
    // 확정본은 새 세대 에셋으로 저장합니다. 기존 결과를 제자리에서 덮어쓰지 않습니다.
    public sealed class JcExploreDetailBake : ScriptableObject
    {
        public const int FormatVersion = 2;
        [Serializable] public sealed class Input { public string key, hash; public bool replace; }
        [Serializable] public sealed class Dependency { public string path, hash; }
        [HideInInspector] public int formatVersion = FormatVersion;
        [HideInInspector] public string scenePath, createdUtc, settingsSnapshot, report;
        [HideInInspector] public GameObject prefab;
        [HideInInspector] public Input[] inputs = Array.Empty<Input>();
        [HideInInspector] public Dependency[] dependencies = Array.Empty<Dependency>();

        public static MeshFilter[] Sources(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
            .Where(f => f.name.StartsWith("TileChunk_", StringComparison.Ordinal) && f.sharedMesh != null &&
                f.gameObject.activeInHierarchy && f.GetComponent<MeshRenderer>() != null).ToArray();

        // 같은 이름의 형제에 한해서 순번을 넣습니다. InstanceID/전체 siblingIndex는 저장하지 않습니다.
        public static string Key(Transform t)
        {
            int ordinal = 0;
            if (t.parent != null)
                for (int i = 0; i < t.GetSiblingIndex(); i++) if (t.parent.GetChild(i).name == t.name) ordinal++;
            else
                foreach (var root in t.gameObject.scene.GetRootGameObjects())
                { if (root == t.gameObject) break; if (root.name == t.name) ordinal++; }
            string segment = Uri.EscapeDataString(t.name) + "[" + ordinal + "]";
            return t.parent == null ? segment : Key(t.parent) + "/" + segment;
        }

        public static Input[] CaptureInputs(Scene scene)
        {
            var result = new List<Input>();
            var key = CreateKeyResolver(scene);
            var materialHashes = new Dictionary<Material,string>();
            foreach (var f in Sources(scene))
            {
                var renderer = f.GetComponent<MeshRenderer>();
                var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                result.Add(new Input { key = key(f.transform), hash = Digest(w =>
                {
                    Matrix(w, f.transform.localToWorldMatrix); w.Write(renderer.enabled); w.Write(f.gameObject.layer);
                    var mesh = f.sharedMesh;
                    if (!mesh.isReadable) throw new InvalidOperationException("DH 원본 메시를 읽을 수 없습니다: " + f.name);
                    w.Write(mesh.vertexCount);
                    foreach (var v in mesh.vertices) Vector(w, v);
                    foreach (var n in mesh.normals) Vector(w, n);
                    foreach (var uv in mesh.uv) { Number(w, uv.x); Number(w, uv.y); }
                    w.Write(mesh.subMeshCount);
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    { var indices = mesh.GetIndices(sub); w.Write((int)mesh.GetTopology(sub)); w.Write(indices.Length); foreach (int index in indices) w.Write(index); }
                    w.Write(renderer.sharedMaterials.Length);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if(material==null) { w.Write("null"); continue; }
                        if(!block.isEmpty) { w.Write(Digest(writer=>Material(writer,material,block))); continue; }
                        if(!materialHashes.TryGetValue(material,out var hash))
                        { hash=Digest(writer=>Material(writer,material)); materialHashes[material]=hash; }
                        w.Write(hash);
                    }
                }) });
            }
            // 교량 크기는 교량 하부 수면 생성 입력이므로 타일 외에도 검증합니다.
            foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "Bridge" || t.name.StartsWith("Bridge(", StringComparison.Ordinal)))
            {
                var renderers = t.GetComponentsInChildren<Renderer>();
                result.Add(new Input { key = "bridge:" + key(t), hash = Digest(w =>
                {
                    Matrix(w, t.localToWorldMatrix); w.Write(renderers.Length);
                    foreach (var r in renderers.OrderBy(r => key(r.transform), StringComparer.Ordinal))
                    { w.Write(key(r.transform)); Vector(w, r.bounds.center); Vector(w, r.bounds.size); }
                }) });
            }
            return result.OrderBy(x => x.key, StringComparer.Ordinal).ToArray();
        }

        public static Func<Transform,string> CreateKeyResolver(Scene scene)
        {
            var cache=new Dictionary<Transform,string>();
            Func<Transform,string> resolve=null;
            resolve=t=>
            {
                if(cache.TryGetValue(t,out var found)) return found;
                string prefix=t.parent!=null?resolve(t.parent)+"/":"";
                var siblings=t.parent!=null?t.parent.Cast<Transform>().ToArray():scene.GetRootGameObjects().Select(r=>r.transform).ToArray();
                var counts=new Dictionary<string,int>(StringComparer.Ordinal);
                foreach(var sibling in siblings)
                {
                    string name=sibling.name; counts.TryGetValue(name,out int ordinal); counts[name]=ordinal+1;
                    cache[sibling]=prefix+Uri.EscapeDataString(name)+"["+ordinal+"]";
                }
                return cache[t];
            };
            return resolve;
        }

        public string Compare(Input[] current)
        {
            if (formatVersion != FormatVersion) return "베이크 형식/생성기 버전이 다릅니다.";
            var now = current.ToDictionary(x => x.key, StringComparer.Ordinal);
            int removed = 0, changed = 0;
            foreach (var old in inputs)
            { if (!now.TryGetValue(old.key, out var value)) removed++; else if (old.hash != value.hash) changed++; }
            var oldKeys=new HashSet<string>(inputs.Select(x=>x.key),StringComparer.Ordinal);
            int added = current.Count(x => !oldKeys.Contains(x.key));
            return removed + changed + added == 0 ? null : $"DH 입력 불일치: 추가 {added}, 제거 {removed}, 변경 {changed}. 재생성·베이크 확정이 필요합니다.";
        }

        static string Digest(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            { using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer);
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", ""); }
        }
        static void Number(BinaryWriter w, float x) => w.Write((long)Math.Round(x * 100000));
        static void Vector(BinaryWriter w, Vector3 v) { Number(w, v.x); Number(w, v.y); Number(w, v.z); }
        static void Matrix(BinaryWriter w, Matrix4x4 m) { for (int i = 0; i < 16; i++) Number(w, m[i]); }
        static void Material(BinaryWriter w, Material m, MaterialPropertyBlock block=null)
        {
            w.Write(m != null); if (m == null) return;
            w.Write(m.name); w.Write(m.shader != null ? m.shader.name : ""); w.Write(m.renderQueue);
            foreach (string keyword in m.shaderKeywords.OrderBy(x => x, StringComparer.Ordinal)) w.Write(keyword);
            if (m.shader == null) return;
            for (int i = 0; i < m.shader.GetPropertyCount(); i++)
            {
                string p = m.shader.GetPropertyName(i); w.Write(p);
                switch (m.shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        var color=block!=null&&block.HasColor(p)?block.GetColor(p):m.GetColor(p);
                        Number(w,color.r);Number(w,color.g);Number(w,color.b);Number(w,color.a);break;
                    case ShaderPropertyType.Vector:
                        var v = block!=null&&block.HasVector(p)?block.GetVector(p):m.GetVector(p); for (int k = 0; k < 4; k++) Number(w, v[k]); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: Number(w, block!=null&&block.HasFloat(p)?block.GetFloat(p):m.GetFloat(p)); break;
                    case ShaderPropertyType.Int: w.Write(m.GetInteger(p)); break;
                    case ShaderPropertyType.Texture:
                        var t = block!=null&&block.HasTexture(p)?block.GetTexture(p):m.GetTexture(p); w.Write(t != null ? t.name : "");
                        if (t != null) { w.Write(t.width); w.Write(t.height); }
                        var scale = m.GetTextureScale(p); var offset = m.GetTextureOffset(p);
                        Number(w, scale.x); Number(w, scale.y); Number(w, offset.x); Number(w, offset.y); break;
                }
            }
        }
    }
}
