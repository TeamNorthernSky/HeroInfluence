using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UliaVfx {
 public sealed class YuliaUsageExample:MonoBehaviour,IVfxSampleCase {
  public YuliaVfxRig rig;public Transform actor,hand,center;public Animator animator;public Camera view;
  public Vector3[] summonPositions={new Vector3(-3,0,2),new Vector3(0,0,2),new Vector3(3,0,2)};
  public Vector3[] supplyPositions={new Vector3(-3,1,4),new Vector3(3,1,4)};
  readonly List<DespairRingVfx> rings=new List<DespairRingVfx>();
  void Start(){rig.Bind(actor,animator,hand,center,2.061252f,view);rig.Summoned+=born=>rings.AddRange(born);rig.SetIdle(true);}
  void OnGUI(){GUILayout.BeginArea(new Rect(12,12,300,460),GUI.skin.box);GUILayout.Label("율리아 적용 예제 · 명시적 대상 연결");
   if(GUILayout.Button("손 에너지"))rig.SetIdle(true);if(GUILayout.Button("율리아 피격"))rig.PlayBodyHit(center.position,Vector3.back);
   if(GUILayout.Button("전투 진입 · 방어막 생성"))rig.GenerateShield(supplyPositions);
   if(GUILayout.Button("굴렁쇠 시전 / 세 기 소환"))rig.CastDespairRings(summonPositions);
   if(GUILayout.Button("방어막 유지"))rig.ShowShield();if(GUILayout.Button("방어막 피격"))rig.HitShield(center.position+Vector3.back*1.2f);
   if(GUILayout.Button("방어막 소멸"))rig.BreakShield(center.position+Vector3.back*1.2f);
   for(int i=0;i<rings.Count;i++){var ring=rings[i];if(!ring)continue;GUILayout.BeginHorizontal();GUILayout.Label("굴렁쇠 "+(i+1));if(GUILayout.Button("돌진"))ring.RushTo(ring.VisualPosition+Vector3.back*10);if(GUILayout.Button("파괴"))ring.Explode();GUILayout.EndHorizontal();}
   GUILayout.EndArea();}
  static void Check(bool condition,string text,List<string> results){if(!condition)throw new System.Exception(text);results.Add("PASS "+text);}
  public IEnumerator Verify(List<string> checks){
   yield return new WaitForSecondsRealtime(.2f);rig.ShowShield();rig.HitShield(center.position+Vector3.back*1.2f);yield return new WaitForSecondsRealtime(.8f);Check(rig.IsShieldVisible,"shield hit preserves shield",checks);
   rig.BreakShield(center.position+Vector3.back);yield return new WaitForSecondsRealtime(1.2f);Check(!rig.IsShieldVisible,"shield removal",checks);
   rig.GenerateShield(supplyPositions);yield return new WaitForSecondsRealtime(3.2f);Check(rig.IsShieldVisible,"two supplied cores create shield",checks);
   rig.CastDespairRings(summonPositions);yield return new WaitForSecondsRealtime(3);
   Check(rig.LastSummoned!=null&&rig.LastSummoned.Length==3,"explicit positions summon three independent instances",checks);
   var born=rig.LastSummoned;born[0].RushTo(born[0].VisualPosition+Vector3.back*8);yield return new WaitForSecondsRealtime(.3f);Check(born[1].IsVisible&&born[2].IsVisible&&!born[1].IsBusy,"only selected summon rushes",checks);
   yield return new WaitForSecondsRealtime(1);Check(!born[0].IsVisible&&born[1].IsVisible,"rush completes without hiding other summons",checks);
   born[1].Explode();yield return new WaitForSecondsRealtime(.25f);Check(!born[1].IsVisible&&born[2].IsVisible,"individual destruction",checks);
   rig.Stop();Check(born[2].IsVisible,"summons survive caster Stop",checks);foreach(var ring in born)if(ring){ring.Stop();Destroy(ring.gameObject);}
   rig.ShowShield();rig.Stop();rig.ShowShield();Check(rig.IsShieldVisible,"rig can stop and restart",checks);
  }
  void OnDestroy(){foreach(var ring in rings)if(ring)Destroy(ring.gameObject);}
 }
}
