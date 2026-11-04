using System.Collections.Generic;
using UnityEngine;
namespace JC.BattleTesting
{
    [ExecuteAlways]
    public sealed class JcDynamicBattleOutput:MonoBehaviour
    {
        public string Report {get;internal set;}
        internal readonly HashSet<Renderer> TerrainRenderers=new HashSet<Renderer>();
        readonly List<Object> owned=new List<Object>();
        readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
        readonly Dictionary<Texture,Texture> textures=new Dictionary<Texture,Texture>();
        public MeshRenderer Add(string label,Mesh mesh,Material[] source,bool terrain,MeshRenderer sourceRenderer=null)
        {
            mesh.hideFlags=HideFlags.DontSave;owned.Add(mesh);
            var go=new GameObject(label){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            MaterialPropertyBlock beforeFade = null;
            if (sourceRenderer)
            {
                var decoration = sourceRenderer.GetComponentInParent<DecorativeObjectPlacement>();
                var target = sourceRenderer.GetComponentInParent<PartyOcclusionFadeTarget>();
                Material[] originalSlots;
                if ((decoration && decoration.TryGetAppearanceBeforeFade(sourceRenderer, out originalSlots, out beforeFade)) ||
                    (target && target.TryGetAppearanceBeforeFade(sourceRenderer, out originalSlots, out beforeFade)))
                    source = originalSlots;
            }
            var result=new Material[source.Length];
            for(int i=0;i<source.Length;i++)
            {
                var original=source[i];if(original==null)continue;
                var block=new MaterialPropertyBlock();if(sourceRenderer!=null){sourceRenderer.GetPropertyBlock(block,i);if(block.isEmpty){if(beforeFade!=null)block=beforeFade;else sourceRenderer.GetPropertyBlock(block);}}
                if(!block.isEmpty||!materials.TryGetValue(original,out var material))
                {
                    material=new Material(original){hideFlags=HideFlags.DontSave};if(block.isEmpty)materials[original]=material;owned.Add(material);
                    for(int prop=0;prop<material.shader.GetPropertyCount();prop++){string name=material.shader.GetPropertyName(prop);int id=Shader.PropertyToID(name);if(!block.HasProperty(id))continue;switch(material.shader.GetPropertyType(prop)){case UnityEngine.Rendering.ShaderPropertyType.Color:material.SetColor(id,block.GetColor(id));break;case UnityEngine.Rendering.ShaderPropertyType.Vector:material.SetVector(id,block.GetVector(id));break;case UnityEngine.Rendering.ShaderPropertyType.Float:case UnityEngine.Rendering.ShaderPropertyType.Range:material.SetFloat(id,block.GetFloat(id));break;case UnityEngine.Rendering.ShaderPropertyType.Texture:material.SetTexture(id,block.GetTexture(id));break;}}
                    // 임포트된 텍스처는 변경하지 않으므로 공유합니다. 읽기 옵션을 바꾸거나 복제하지 않습니다.
                    // 탐사 드레싱이 소유하는 임시 텍스처만 별도 복사하여 원본 씬 종료에 대비합니다.
                    foreach(var name in material.GetTexturePropertyNames())
                    {
                        var texture=material.GetTexture(name);
                        if(texture==null||(texture.hideFlags&HideFlags.DontSave)==0)continue;
#if UNITY_EDITOR
                        if(UnityEditor.EditorUtility.IsPersistent(texture))continue;
#endif
                        if(!textures.TryGetValue(texture,out var copy))
                        {
                            if(texture is Texture2D image)copy=Instantiate(image);
                            else if(texture is RenderTexture rendered)
                            {
                                var renderCopy=new RenderTexture(rendered.descriptor){name=rendered.name+" · battle",filterMode=rendered.filterMode,wrapMode=rendered.wrapMode};
                                renderCopy.Create();
                                var previous=RenderTexture.active;
                                try{Graphics.Blit(rendered,renderCopy);}catch{renderCopy.Release();Destroy(renderCopy);throw;}
                                finally{RenderTexture.active=previous;}
                                copy=renderCopy;
                            }
                            else throw new System.InvalidOperationException("임시 텍스처를 독립 보관할 수 없습니다: "+texture.name);
                            copy.hideFlags=HideFlags.DontSave;textures.Add(texture,copy);owned.Add(copy);
                        }
                        material.SetTexture(name,copy);
                    }
                    // 변형 전 월드 좌표의 방향 지도를 재사용하지 않고 공통 방향으로 복귀합니다.
                    if(material.HasProperty("_FollowCanal"))material.SetFloat("_FollowCanal",0);
                }
                result[i]=material;
            }
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=result;
            if(terrain)TerrainRenderers.Add(renderer);
            return renderer;
        }
        internal void AddDistantGround(JcDynamicBattleSource source, Matrix4x4 fieldToBattle)
        {
            Material grass = null;
            float bottom = 0;
            foreach (var part in source.parts)
            {
                if (!part.terrain) continue;
                bottom = Mathf.Min(bottom, fieldToBattle.MultiplyPoint3x4(part.bounds.min).y);
                if (grass || part.materials == null) continue;
                foreach (var material in part.materials)
                {
                    if (!material || !material.HasProperty("_BaseMap")) continue;
                    var texture = material.GetTexture("_BaseMap");
                    if (texture && (texture.name == "Grass_Color" || texture.name == "tilemap_04"))
                    {
                        grass = material;
                        break;
                    }
                }
            }
            var surface = grass ? grass : source.roadMaterial;
            if (!surface) return;
            // Below the lowest terrain, to keep water and authored roads fully visible.
            const float half = 2048f;
            float y = bottom - .25f;
            var mesh = new Mesh { name = "Distant field ground" };
            mesh.vertices = new[] { new Vector3(-half,y,-half), new Vector3(-half,y,half),
                new Vector3(half,y,half), new Vector3(half,y,-half) };
            mesh.uv = new[] { Vector2.zero, Vector2.up*512, Vector2.one*512, Vector2.right*512 };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Add("JC_DistantFieldGround", mesh, new[] { surface }, false);
        }

        void OnDestroy(){foreach(var value in owned)if(value!=null){if(value is RenderTexture rendered)rendered.Release();if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}owned.Clear();}
    }
}
