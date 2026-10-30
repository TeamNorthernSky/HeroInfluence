using UnityEditor;
using UnityEngine;
namespace JC.EnvironmentDetail.Editor
{
    // 원본 FBX와 배치 Transform은 보존합니다. 은행의 앞면을 기준으로 뒤쪽 깊이만 압축합니다.
    public sealed class JcBankDepthImporter : AssetPostprocessor
    {
        public const string BankPath="Assets/_ProtoType_Merge/DH/Resources/Building/SPBuilding002.Bank.fbx";
        public const float DepthScale=.9f;
        public override uint GetVersion()=>1;
        void OnPostprocessModel(GameObject root)
        {
            if(assetPath!=BankPath)return;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=f.sharedMesh;if(!mesh)continue;
                // FBX의 로컬 +Y가 -90도 X 회전 후 은행 정면(-Z)이 됩니다.
                var vertices=mesh.vertices;float front=mesh.bounds.max.y;
                for(int i=0;i<vertices.Length;i++)vertices[i].y=front+(vertices[i].y-front)*DepthScale;
                mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();
            }
        }
    }
}
