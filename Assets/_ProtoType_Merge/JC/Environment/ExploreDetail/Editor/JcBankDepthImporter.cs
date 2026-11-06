using UnityEditor;
using UnityEngine;
namespace JC.EnvironmentDetail.Editor
{
    // 원본 FBX와 배치 Transform은 보존합니다. 은행의 앞면을 기준으로 뒤쪽 깊이만 압축합니다.
    public sealed class JcBankDepthImporter : AssetPostprocessor
    {
        public const string BankPath="Assets/_ProtoType_Merge/DH/Resources/Building/SPBuilding002.Bank.fbx";
        public const float DepthScale=.9f;
        // JC | 2026-11-06 | 은행 시각 메시만 뒤로 옮겨 입구 계단과 인카운터 테두리 사이를 띄웁니다.
        // 튜토리얼 배율36에서 0.216m 후퇴하며 셀·루트·점령 판정은 변경하지 않습니다.
        public const float RearClearance=.006f;
        public override uint GetVersion()=>2;
        void OnPostprocessModel(GameObject root)
        {
            if(assetPath!=BankPath)return;
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=f.sharedMesh;if(!mesh)continue;
                // FBX의 로컬 +Y가 -90도 X 회전 후 은행 정면(-Z)이 됩니다.
                var vertices=mesh.vertices;float front=mesh.bounds.max.y;
                for(int i=0;i<vertices.Length;i++)vertices[i].y=front+(vertices[i].y-front)*DepthScale-RearClearance;
                mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();
            }
        }
    }
}
