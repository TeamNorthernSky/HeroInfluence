using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace JC.BattleTesting
{
    // 씬 전용 디버그 패널. 전투 턴/저장 데이터/실내 라이팅에 쓰지 않습니다.
    public sealed class JcBossSceneryTest : MonoBehaviour
    {
        [Tooltip("이 씬의 전투 세션입니다.")] public JcBattleTestSession session;
        [Tooltip("향후 베이크 결과를 연결할 시간대별 프리셋입니다.")] public JcBossSceneryBake bake;
        [Tooltip("도시 좌표 기준 편집 영역 최소값입니다.")] public Vector2 editMin;
        [Tooltip("도시 좌표 기준 편집 영역 최대값입니다.")] public Vector2 editMax;
        [Tooltip("시작 시간대입니다. 0 아침, 1 오후, 2 저녁, 3 새벽.")][Range(0,3)] public int period;
        [Tooltip("창밖에 편집 영역 경계를 표시합니다.")] public bool showBoundary;
        readonly float[] hours={8.5f,14.5f,20.5f,2.5f};
        readonly string[] labels={"08:30","14:30","20:30","02:30"};
        readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
        readonly Dictionary<MeshFilter,Mesh> originalMeshes=new Dictionary<MeshFilter,Mesh>();
        GameObject background; Transform city; LineRenderer boundary; Material lineMaterial;
        int applied=-1; bool baked;
        public string Status {get;private set;}="배경 생성 대기";
        void LateUpdate()
        {
            var next=session?session.ActiveEncounterBackground:null;
            if(next!=background){Restore();background=next;city=background?background.transform.Find("도시 원경 (베이크)"):null;applied=-1;boundary=null;}
            if(!city)return;
            if(applied!=period){ApplyPeriod();applied=period;}
            if(showBoundary&&!boundary)CreateBoundary();
            if(boundary)boundary.enabled=showBoundary;
        }
        void Restore(){foreach(var p in originalMeshes)if(p.Key)p.Key.sharedMesh=p.Value;originalMeshes.Clear();foreach(var p in originals)if(p.Key){p.Key.sharedMaterials=p.Value;p.Key.SetPropertyBlock(null);for(int i=0;i<p.Value.Length;i++)p.Key.SetPropertyBlock(null,i);}originals.Clear();}
        public void SelectPeriod(int index){period=Mathf.Clamp(index,0,3);applied=-1;}
        void ApplyPeriod()
        {
            Restore();period=Mathf.Clamp(period,0,3);baked=false;
            var entry=bake&&bake.periods!=null?bake.periods.FirstOrDefault(p=>p!=null&&Mathf.Abs(p.hour-hours[period])<.01f):null;
            var bindings=entry?.bindings;
            if(bindings!=null&&bindings.Length>0){
                var targets=new List<Renderer>();bool valid=true;
                foreach(var binding in bindings){var t=binding!=null?city.Find(binding.rendererPath):null;var r=t?t.GetComponent<Renderer>():null;var f=t?t.GetComponent<MeshFilter>():null;
                    if(!r||!f||f.sharedMesh!=binding.mesh||binding.materials==null||binding.materials.Length!=r.sharedMaterials.Length||binding.materials.Any(m=>!m)){valid=false;break;}targets.Add(r);}
                // 부분 베이크를 섞지 않습니다. 도시 전체가 준비되었을 때만 전환합니다.
                var expected=city.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is LineRenderer)).ToArray();
                valid&=targets.Distinct().Count()==expected.Length&&expected.All(targets.Contains);
                if(valid){for(int i=0;i<targets.Count;i++){var r=targets[i];originals[r]=r.sharedMaterials;r.sharedMaterials=bindings[i].materials;if(bindings[i].bakedMesh){var f=r.GetComponent<MeshFilter>();originalMeshes[f]=f.sharedMesh;f.sharedMesh=bindings[i].bakedMesh;}for(int j=0;j<bindings[i].materials.Length;j++)r.SetPropertyBlock(null,j);}baked=true;}
            }
            if(!baked){var time=city.GetComponent<JcBossCityTime>();if(time)time.SetHour(hours[period]);}
            Status=baked?"베이크 재질 적용":"임시 도시 라이팅 · 베이크 미적용";
        }
        void CreateBoundary()
        {
            var go=new GameObject("편집 범위 (테스트)");go.transform.SetParent(city,false);boundary=go.AddComponent<LineRenderer>();boundary.useWorldSpace=false;boundary.loop=true;boundary.widthMultiplier=.12f;boundary.positionCount=4;
            boundary.SetPositions(new[]{new Vector3(editMin.x-.5f,.4f,editMin.y-.5f),new Vector3(editMax.x+.5f,.4f,editMin.y-.5f),new Vector3(editMax.x+.5f,.4f,editMax.y+.5f),new Vector3(editMin.x-.5f,.4f,editMax.y+.5f)});
            if(!lineMaterial){lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterial.SetColor("_BaseColor",Color.cyan);}boundary.sharedMaterial=lineMaterial;boundary.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;boundary.receiveShadows=false;
        }
        void OnGUI()
        {
            if(!Application.isPlaying)return;
            var old=GUI.matrix;float scale=Mathf.Max(.65f,Screen.height/1080f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUILayout.BeginArea(new Rect(Screen.width/scale*.5f-225,8,450,100),GUI.skin.box);
            GUILayout.Label("창밖 풍경 테스트 / "+Status);GUILayout.BeginHorizontal();
            for(int i=0;i<4;i++){GUI.enabled=city; if(GUILayout.Toggle(period==i,labels[i],GUI.skin.button)&&period!=i)SelectPeriod(i);}
            GUI.enabled=true;GUILayout.EndHorizontal();showBoundary=GUILayout.Toggle(showBoundary,"편집 영역 경계 표시");GUILayout.EndArea();GUI.matrix=old;
        }
        void OnDestroy(){Restore();if(lineMaterial)Destroy(lineMaterial);}
    }
}
