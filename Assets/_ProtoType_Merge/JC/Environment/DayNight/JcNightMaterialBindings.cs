using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.Environment
{
 // Runtime copies preserve imported materials, per-renderer palettes and occlusion replacements.
 public sealed class JcNightMaterialBindings : IDisposable
 {
  sealed class Entry {public Renderer renderer;public int slot;public Material original,shown;}
  readonly List<Entry> entries=new List<Entry>();
  static readonly Dictionary<Material,Material> originals=new Dictionary<Material,Material>();
  static Shader shader;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void Reset(){originals.Clear();shader=null;}
  public static Material Original(Material material)=>material!=null&&originals.TryGetValue(material,out var source)?source:material;
  public void Bind(Renderer renderer,int slot,Texture windowMask,bool humanoid=false){
   if(renderer==null)return;
   var materials=renderer.sharedMaterials;if(slot>=materials.Length)return;
   foreach(var entry in entries)if(entry.renderer==renderer&&entry.slot==slot){
    if(materials[slot]==entry.shown)return;
    if(materials[slot]==entry.original){materials[slot]=entry.shown;renderer.sharedMaterials=materials;return;}
   }
   var original=materials[slot];
   if(original==null||original.shader==null||original.shader.name!="Universal Render Pipeline/Lit")return;
   if(shader==null)shader=Resources.Load<Shader>("JcNightLit");if(shader==null||!shader.isSupported)return;
   var copy=new Material(original){name=original.name+" · time lighting",hideFlags=HideFlags.HideAndDontSave};
   copy.shader=shader;copy.SetTexture("_JcWindowMask",windowMask!=null?windowMask:Texture2D.blackTexture);copy.SetFloat("_JcHumanoid",humanoid?1:0);
   originals[copy]=original;materials[slot]=copy;renderer.sharedMaterials=materials;
   entries.Add(new Entry{renderer=renderer,slot=slot,original=original,shown=copy});
  }
  public void ScanCharacters(Scene scene){
   // Character rigs only: never brighten props, terrain or mechanical enemies.
   foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    var party=renderer.GetComponentInParent<PartyGridMover>();var enemy=renderer.GetComponentInParent<EnemyGridMover>();
    if(party==null&&enemy==null)continue;
    if(enemy!=null){
     bool head=false,leg=false;
     var animator=renderer.GetComponentInParent<Animator>();
     var bones=animator!=null?animator.GetComponentsInChildren<Transform>(true):renderer.bones;
     foreach(var bone in bones){if(bone==null)continue;string name=bone.name.ToLowerInvariant();head|=name.Contains("head");leg|=name.Contains("thigh")||name.Contains("leg");}
     if(!head||!leg)continue;
    }
    for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)Bind(renderer,slot,null,true);
   }
  }
  public void Dispose(){
   // The occlusion system caches original material arrays. Release its cache before destroying copies.
   var placements=new HashSet<DecorativeObjectPlacement>();
   foreach(var entry in entries)if(entry.renderer!=null){var placement=entry.renderer.GetComponentInParent<DecorativeObjectPlacement>();if(placement!=null)placements.Add(placement);}
   foreach(var placement in placements)placement.RestoreOcclusionFade();
   foreach(var entry in entries){
    if(entry.renderer!=null){var materials=entry.renderer.sharedMaterials;if(entry.slot<materials.Length&&materials[entry.slot]==entry.shown){materials[entry.slot]=entry.original;entry.renderer.sharedMaterials=materials;}}
    if(entry.shown!=null){originals.Remove(entry.shown);if(Application.isPlaying)UnityEngine.Object.Destroy(entry.shown);else UnityEngine.Object.DestroyImmediate(entry.shown);}
   }entries.Clear();foreach(var placement in placements)if(placement!=null)placement.RefreshRenderers();
  }
 }
}
