using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
namespace UliaVfx {
 public sealed class AmplifierUsageExample:MonoBehaviour,IVfxSampleCase {
  public AmplifierVfxRig rig,other;public Transform core,otherCore;public GameObject coreVisual,otherVisual;public Light coreLight,otherLight;public Camera view;
  void Start(){rig.Bind(core,coreVisual,new[]{coreLight},view);other.Bind(otherCore,otherVisual,new[]{otherLight},view);rig.SetIdle(true);other.SetIdle(true);}
  void OnGUI(){GUILayout.BeginArea(new Rect(12,12,300,300),GUI.skin.box);GUILayout.Label("증폭기 적용 예제 · 한 대씩 독립 제어");
   if(GUILayout.Button("대기 에너지"))rig.SetIdle(true);if(GUILayout.Button("시전"))rig.PlayCast();if(GUILayout.Button("나락의 폭풍"))rig.PlayStorm(new Vector3(0,0,0));
   if(GUILayout.Button("공멸의 궤적"))rig.PlayTrajectory(new Vector3(0,.8f,2),new Vector3(0,.8f,-12),view);
   if(GUILayout.Button("에너지 공급"))rig.PlayEnergySupply();if(GUILayout.Button("코어 파괴 / 흰 연기"))rig.BreakCore();if(GUILayout.Button("코어 복구"))rig.RecoverCore();if(GUILayout.Button("상태 초기화"))rig.ResetVisualState();GUILayout.EndArea();}
  static void Check(bool condition,string text,List<string> results){if(!condition)throw new System.Exception(text);results.Add("PASS "+text);}
  public IEnumerator Verify(List<string> checks){
   yield return new WaitForSecondsRealtime(.2f);int release=0;rig.SkillReleased+=()=>release++;
   rig.PlayCast();yield return new WaitForSecondsRealtime(1.2f);Check(release==1,"cast release event fires once",checks);
   rig.PlayStorm(Vector3.zero);yield return new WaitForSecondsRealtime(1.2f);Check(release==2,"storm release event",checks);
   rig.PlayTrajectory(new Vector3(0,.8f,2),new Vector3(0,.8f,-12),view);yield return new WaitForSecondsRealtime(1.7f);
   Check(rig.LastTrajectory&&rig.LastTrajectory.ReleaseFrame==rig.LastTrajectory.BoltStartFrame&&rig.LastTrajectory.ReleaseFrame>=0,"trajectory lightning starts on amplifier release frame",checks);
   rig.BreakCore();yield return new WaitForSecondsRealtime(1);while(rig.IsBusy)yield return null;Check(rig.IsBroken&&!coreVisual.activeSelf&&otherVisual.activeSelf,"one amplifier breaks independently",checks);
   Check(rig.BrokenSmoke,"white smoke hold exists",checks);foreach(var v in rig.BrokenSmoke.GetComponentsInChildren<VisualEffect>()){
    if(v.HasFloat("SmokeRate"))Check(Mathf.Approximately(v.GetFloat("SmokeRate"),2),"final smoke rate = 2",checks);
    if(v.HasGradient("SmokeGradient")){var g=v.GetGradient("SmokeGradient");foreach(var k in g.colorKeys)Check(Mathf.Abs(k.color.r-k.color.g)<.001f&&Mathf.Abs(k.color.g-k.color.b)<.001f,"smoke gradient is neutral white",checks);}
   }
   bool recovered=false;rig.RecoverCore(()=>recovered=true);yield return new WaitForSecondsRealtime(3);
   Check(!rig.IsBroken&&coreVisual.activeSelf&&recovered,"core recovers and invokes shield-link callback",checks);
   rig.PlayCast();rig.BreakCore();yield return new WaitForSecondsRealtime(1);while(rig.IsBusy)yield return null;Check(rig.IsBroken,"break cancels pending cast",checks);
   rig.ResetVisualState();Check(!rig.IsBroken&&coreVisual.activeSelf,"explicit reset restores core",checks);
   bool supply=false;rig.PlayEnergySupply(()=>supply=true);yield return new WaitForSecondsRealtime(3);Check(supply,"energy supply burst callback",checks);
  }
 }
}

