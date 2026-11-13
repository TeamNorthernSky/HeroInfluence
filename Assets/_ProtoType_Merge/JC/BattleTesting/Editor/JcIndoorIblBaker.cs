using System;
using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-12 | 실내 환경을 편집 시 SH 확산광과 GGX 반사로 변환합니다. 런타임 캡처·매 프레임 베이크는 없습니다.
    public static class JcIndoorIblBaker
    {
        const int Size=64, Samples=128;
        public static Vector3 Direction(int face,float u,float v)
        {
            switch(face)
            {
                case 0:return new Vector3(1,-v,-u).normalized;
                case 1:return new Vector3(-1,-v,u).normalized;
                case 2:return new Vector3(u,1,v).normalized;
                case 3:return new Vector3(u,-1,-v).normalized;
                case 4:return new Vector3(u,-v,1).normalized;
                default:return new Vector3(-u,-v,-1).normalized;
            }
        }
        // JC | 2026-11-12 | 외부 하늘을 흉내 내지 않고 천장 패널·중성 벽·어두운 바닥의 선형 HDR 입사광을 생성합니다.
        public static Color Neutral(Vector3 d)
        {
            Color c=Color.Lerp(new Color(.16f,.17f,.19f),new Color(.62f,.64f,.67f),Mathf.Clamp01((d.y+1)*.5f));
            if(d.y>0)
            {
                float x=d.x/Mathf.Max(.01f,d.y),z=d.z/Mathf.Max(.01f,d.y);
                float panel=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.55f,Mathf.Abs(x)));
                panel*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.9f,Mathf.Abs(z)));
                c+=new Color(1,1,1,0)*panel*2;
            }
            c.a=1; return c;
        }
        static float Radical(uint v)
        {
            v=(v<<16)|(v>>16);v=((v&0x55555555u)<<1)|((v&0xAAAAAAAAu)>>1);
            v=((v&0x33333333u)<<2)|((v&0xCCCCCCCCu)>>2);v=((v&0x0F0F0F0Fu)<<4)|((v&0xF0F0F0F0u)>>4);
            v=((v&0x00FF00FFu)<<8)|((v&0xFF00FF00u)>>8);return v*2.3283064365386963e-10f;
        }
        public static Vector4[] Project(Func<Vector3,Color> radiance)
        {
            var sh=new Vector4[9]; const int count=8192;
            var basis=new float[9];
            for(uint i=0;i<count;i++)
            {
                float z=1-2*(i+.5f)/count,phi=2*Mathf.PI*Radical(i),r=Mathf.Sqrt(1-z*z);
                var n=new Vector3(r*Mathf.Cos(phi),r*Mathf.Sin(phi),z);
                basis[0]=.2820947918f;basis[1]=.4886025119f*n.y;basis[2]=.4886025119f*n.z;basis[3]=.4886025119f*n.x;
                basis[4]=1.092548431f*n.x*n.y;basis[5]=1.092548431f*n.y*n.z;basis[6]=.3153915653f*(3*n.z*n.z-1);
                basis[7]=1.092548431f*n.x*n.z;basis[8]=.5462742153f*(n.x*n.x-n.y*n.y);
                Color c=radiance(n);var rgb=new Vector4(c.r,c.g,c.b,0);
                // JC | 2026-11-12 | Lambert 적분/pi의 밴드별 계수(1,2/3,1/4)로 상수 환경의 밝기를 보존합니다.
                for(int b=0;b<9;b++) sh[b]+=rgb*(basis[b]*(4*Mathf.PI/count)*(b==0?1:b<4?2f/3:.25f));
            }
            return sh;
        }
        public static Color Filter(Func<Vector3,Color> radiance,Vector3 n,float perceptualRoughness)
        {
            if(perceptualRoughness<.001f)return radiance(n);
            Vector3 up=Mathf.Abs(n.z)<.999f?Vector3.forward:Vector3.right;
            var tangent=Vector3.Cross(up,n).normalized;var bitangent=Vector3.Cross(n,tangent);
            float a=perceptualRoughness*perceptualRoughness;Color sum=Color.clear;float weight=0;
            for(uint i=0;i<Samples;i++)
            {
                float phi=2*Mathf.PI*i/Samples,y=Radical(i);
                float cos=Mathf.Sqrt((1-y)/(1+(a*a-1)*y)),sin=Mathf.Sqrt(Mathf.Max(0,1-cos*cos));
                var h=tangent*(Mathf.Cos(phi)*sin)+bitangent*(Mathf.Sin(phi)*sin)+n*cos;
                var l=(2*Vector3.Dot(n,h)*h-n).normalized;float nl=Mathf.Max(0,Vector3.Dot(n,l));
                if(nl>0){sum+=radiance(l)*nl;weight+=nl;}
            }
            sum/=Mathf.Max(.00001f,weight);sum.a=1;return sum;
        }
        sealed class CpuCube
        {
            public Color[][] faces=new Color[6][];
            public Color Sample(Vector3 d)
            {
                float x=Mathf.Abs(d.x),y=Mathf.Abs(d.y),z=Mathf.Abs(d.z),u,v;int face;
                if(x>=y&&x>=z){face=d.x>0?0:1;u=(d.x>0?-d.z:d.z)/x;v=-d.y/x;}
                else if(y>=z){face=d.y>0?2:3;u=d.x/y;v=(d.y>0?d.z:-d.z)/y;}
                else{face=d.z>0?4:5;u=(d.z>0?d.x:-d.x)/z;v=-d.y/z;}
                float px=Mathf.Clamp((u+1)*.5f*Size-.5f,0,Size-1),py=Mathf.Clamp((v+1)*.5f*Size-.5f,0,Size-1);
                int ix=(int)px,iy=(int)py,jx=Mathf.Min(ix+1,Size-1),jy=Mathf.Min(iy+1,Size-1);
                var f=faces[face];return Color.Lerp(Color.Lerp(f[iy*Size+ix],f[iy*Size+jx],px-ix),Color.Lerp(f[jy*Size+ix],f[jy*Size+jx],px-ix),py-iy);
            }
        }
        static CpuCube Read(Cubemap source)
        {
            var shader=Shader.Find("Hidden/JC/Indoor IBL Read");
            if(!shader || !shader.isSupported)throw new InvalidOperationException("IBL 원본 읽기 셰이더를 사용할 수 없습니다.");
            var material=new Material(shader);var rt=RenderTexture.GetTemporary(Size,Size,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
            var read=new Texture2D(Size,Size,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;bool srgb=GL.sRGBWrite;
            try
            {
                var result=new CpuCube();material.SetTexture("_Source",source);GL.sRGBWrite=false;
                for(int f=0;f<6;f++)
                {
                    material.SetInt("_Face",f);Graphics.Blit(null,rt,material);RenderTexture.active=rt;
                    read.ReadPixels(new Rect(0,0,Size,Size),0,0);read.Apply();result.faces[f]=read.GetPixels();
                }
                return result;
            }
            finally{RenderTexture.active=previous;GL.sRGBWrite=srgb;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(read);UnityEngine.Object.DestroyImmediate(material);}
        }
        public static void Bake(JcIndoorIblAsset asset)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("플레이 종료 후 환경을 구워주세요.");
            if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))throw new InvalidOperationException("먼저 환경 에셋을 저장해 주세요.");
            if(asset.input==JcIndoorIblAsset.Input.CustomCubemap&&!asset.source)throw new InvalidOperationException("원본 HDR 큐브맵을 지정해 주세요.");
            Func<Vector3,Color> sample=Neutral;
            if(asset.input==JcIndoorIblAsset.Input.CustomCubemap)sample=Read(asset.source).Sample;
            var sh=Project(sample);var cube=new Cubemap(Size,TextureFormat.RGBAHalf,true){name="실내 HDR GGX 반사",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
            try
            {
                for(int mip=0;mip<cube.mipmapCount;mip++)
                {
                    int size=Mathf.Max(1,Size>>mip);float m=(float)mip/(cube.mipmapCount-1);
                    float roughness=(1.7f-Mathf.Sqrt(2.89f-2.8f*m))/1.4f;
                    for(int face=0;face<6;face++)
                    {
                        var pixels=new Color[size*size];
                        for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=Filter(sample,Direction(face,2*(x+.5f)/size-1,2*(y+.5f)/size-1),roughness);
                        cube.SetPixels(pixels,(CubemapFace)face,mip);
                    }
                }
                cube.Apply(false,false);
                // JC | 2026-11-12 | 계산이 전부 성공한 뒤에만 기존 결과를 갱신하고 하위 큐브맵 참조를 보존합니다.
                if(asset.specular){EditorUtility.CopySerialized(cube,asset.specular);EditorUtility.SetDirty(asset.specular);}
                else{AssetDatabase.AddObjectToAsset(cube,asset);asset.specular=cube;cube=null;}
                asset.diffuse=sh;asset.bakedSource=asset.input==JcIndoorIblAsset.Input.NeutralRoom?"NeutralRoom v1":AssetDatabase.GetAssetPath(asset.source);
                EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
            }
            finally{if(cube)UnityEngine.Object.DestroyImmediate(cube);}
        }
    }
}
