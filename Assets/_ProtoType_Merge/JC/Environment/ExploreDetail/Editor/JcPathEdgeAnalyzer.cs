using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JC.EnvironmentDetail.Editor
{
    public static class JcPathEdgeAnalyzer
    {
        // 타일 레지스트리의 실제 이미지에서 굽습니다. 런타임 텍스처 읽기와 원본 Import 변경은 없습니다.
        public static void Analyze(JcExploreDetailProfile profile)
        {
            var registry=AssetDatabase.LoadAssetAtPath<LevelTileRegistry>("Assets/_ProtoType_Merge/DH/LevelTileRegistry.asset");
            if(registry==null)throw new InvalidOperationException("DH 타일 레지스트리를 찾을 수 없습니다.");
            profile.pathEdgeRules=registry.TileEntries.Where(e=>e.Sprite!=null&&e.Sprite.texture.name.StartsWith("ParkRoad",StringComparison.Ordinal))
                .Select(e=>e.Sprite.texture).Distinct().OrderBy(t=>t.name).Select(Read).ToArray();
            profile.roadEdgeShader=Shader.Find("JC/Environment/Road Edge");
            EditorUtility.SetDirty(profile);
        }
        static JcPathEdgeRule Read(Texture2D source)
        {
            var rt=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;Texture2D copy=null;
            try
            {
                Graphics.Blit(source,rt);RenderTexture.active=rt;copy=new Texture2D(128,128,TextureFormat.RGB24,false);
                copy.ReadPixels(new Rect(0,0,128,128),0,0);copy.Apply();
                Func<int,Vector2> edge=d=>
                {
                    int first=-1,last=-1,runs=0;bool inside=false;
                    for(int i=0;i<128;i++)
                    {
                        Color c=copy.GetPixel(d<2?(d==0?1:126):i,d<2?i:(d==2?1:126));
                        bool dirt=c.r>c.g*1.07f&&c.g>c.b*1.12f&&c.r>.3f;
                        if(dirt){if(!inside)runs++;if(first<0)first=i;last=i;}inside=dirt;
                    }
                    // 간격을 합쳐 입구를 과하게 넓히지 않고 지원 범위를 명확히 제한합니다.
                    if(runs>1)throw new InvalidOperationException(source.name+"의 한쪽 경계에 흙길 구간이 여러 개입니다. 별도 규칙 확인이 필요합니다.");
                    return first<0?new Vector2(-1,-1):new Vector2(first/128f,(last+1)/128f);
                };
                return new JcPathEdgeRule{texture=source,left=edge(0),right=edge(1),bottom=edge(2),top=edge(3)};
            }
            finally{RenderTexture.active=previous;if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
