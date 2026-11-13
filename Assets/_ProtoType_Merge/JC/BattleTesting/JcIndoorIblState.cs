using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-12 | 전역 RenderSettings는 건드리지 않고 전용 셰이더 값만 소유합니다. 씬 이탈 시 정확히 복원합니다.
    public sealed class JcIndoorIblState
    {
        static readonly string[] Names = { "_JcIblControl", "_JcIblTint", "_JcIblRotation" };
        readonly Vector4[] before = new Vector4[3];
        readonly Vector4[] coefficients = new Vector4[9];
        Texture previousTexture;
        Vector4[] previousSh;
        bool acquired;
        public void Apply(JcIndoorIblSettings settings, bool bank)
        {
            if(!acquired)
            {
                for(int i=0;i<Names.Length;i++) before[i]=Shader.GetGlobalVector(Names[i]);
                previousTexture=Shader.GetGlobalTexture("_JcIblSpecular");
                previousSh=Shader.GetGlobalVectorArray("_JcIblSH"); acquired=true;
            }
            bool ready=settings != null && settings.enabled && settings.environment && settings.environment.Ready;
            Shader.SetGlobalVector(Names[0],ready ? new Vector4(1,Mathf.Max(0,settings.diffuseIntensity),Mathf.Max(0,settings.specularIntensity),bank?Mathf.Clamp01(settings.bankProbeBlend):0) : Vector4.zero);
            if(!ready) return;
            Shader.SetGlobalTexture("_JcIblSpecular",settings.environment.specular);
            Shader.SetGlobalVectorArray("_JcIblSH",settings.environment.diffuse);
            Shader.SetGlobalVector(Names[1],settings.tint.linear);
            float angle=settings.rotation*Mathf.Deg2Rad;
            Shader.SetGlobalVector(Names[2],new Vector4(Mathf.Cos(angle),Mathf.Sin(angle),settings.environment.specular.mipmapCount-1,0));
        }
        public void Restore()
        {
            if(!acquired) return;
            for(int i=0;i<Names.Length;i++) Shader.SetGlobalVector(Names[i],before[i]);
            Shader.SetGlobalTexture("_JcIblSpecular",previousTexture);
            Shader.SetGlobalVectorArray("_JcIblSH",previousSh != null && previousSh.Length==9 ? previousSh : coefficients);
            acquired=false;
        }
    }
}
