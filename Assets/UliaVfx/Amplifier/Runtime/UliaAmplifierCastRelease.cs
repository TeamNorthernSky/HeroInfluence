using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
namespace UliaVfx
{
 // Finishes the existing orbital particles instead of replacing them with a second burst.
 public sealed class UliaAmplifierCastRelease : MonoBehaviour
 {
  class Cloud {
   public ParticleSystem system;
   public ParticleSystem.Particle[] particles;
   public Vector3[] origins, directions, sizes;
   public Color[] colors;
   public LineRenderer[] ribbons;
   public bool world; public Transform basis;
  }
  readonly List<Cloud> clouds = new List<Cloud>();
  GameObject glow;
  VisualEffect[] glowGraphs;
  Gradient[] glowColors;
  Vector3 glowScale;
  Material ribbonMaterial;
  float elapsed, started;
  Vector3 orbitCenter;
  const float Seconds=.4f;
  public void Begin(GameObject coreGlow, Vector3 center) {
   started=Time.unscaledTime; orbitCenter=center;
   glow=coreGlow;
   if(glow) {
    glowScale=glow.transform.localScale;
    glowGraphs=glow.GetComponentsInChildren<VisualEffect>(true);
    glowColors=new Gradient[glowGraphs.Length];
    for(int i=0;i<glowGraphs.Length;i++) if(glowGraphs[i].HasGradient("Color")) glowColors[i]=glowGraphs[i].GetGradient("Color");
   }
   foreach(var ps in GetComponentsInChildren<ParticleSystem>(true)) {
    if(!ps.gameObject.activeInHierarchy) continue;
    var main=ps.main;
    var particles=new ParticleSystem.Particle[ps.particleCount];
    int n=ps.GetParticles(particles);
    var cloud=new Cloud {system=ps,particles=particles,origins=new Vector3[n],directions=new Vector3[n],sizes=new Vector3[n],colors=new Color[n],ribbons=new LineRenderer[n]};
    Transform basis=main.simulationSpace==ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : ps.transform;
    bool world=main.simulationSpace==ParticleSystemSimulationSpace.World; cloud.world=world;cloud.basis=basis;
    for(int i=0;i<n;i++) {
     Vector3 position=world ? particles[i].position : basis.TransformPoint(particles[i].position);
     Vector3 radial=position-center;
     if(radial.sqrMagnitude<.001f) radial=Quaternion.Euler(0,i*137.5f,0)*Vector3.forward;
     cloud.origins[i]=position;
     cloud.directions[i]=(radial.normalized+Vector3.up*.15f).normalized;
     cloud.colors[i]=particles[i].GetCurrentColor(ps);
     cloud.sizes[i]=particles[i].GetCurrentSize3D(ps);
     particles[i].velocity=Vector3.zero;
     particles[i].startLifetime=particles[i].remainingLifetime=Seconds+.2f;
    }
    // Native per-particle trails are paused with their simulation: moving the particle
    // by SetParticles otherwise stretches one straight segment from its last position.
    var nativeTrails=ps.trails;nativeTrails.enabled=false;
    if(ps.name=="orbs") {
     if(!ribbonMaterial) {
      ribbonMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
      ribbonMaterial.SetFloat("_Surface",1);ribbonMaterial.SetFloat("_Blend",1);
      ribbonMaterial.SetFloat("_SrcBlend",5);ribbonMaterial.SetFloat("_DstBlend",1);
      ribbonMaterial.SetFloat("_ZWrite",0);ribbonMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
      ribbonMaterial.SetColor("_BaseColor",Color.white);ribbonMaterial.renderQueue=3000;
     }
     var source=ps.GetComponent<ParticleSystemRenderer>();
     var material=source.trailMaterial ? source.trailMaterial : source.sharedMaterial;
     for(int i=0;i<n;i++) {
      var ribbon=new GameObject("Curved_Burst_Trail_"+i).AddComponent<LineRenderer>();
      ribbon.transform.SetParent(transform,false);ribbon.transform.position=cloud.origins[i];
      ribbon.useWorldSpace=true;ribbon.numCornerVertices=4;ribbon.numCapVertices=4;
      ribbon.sharedMaterial=ribbonMaterial;ribbon.widthMultiplier=.08f;
      ribbon.widthCurve=AnimationCurve.Linear(0,1,1,0);
      ribbon.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
      ribbon.receiveShadows=false;cloud.ribbons[i]=ribbon;
     }
    }
    ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
    var emission=ps.emission;emission.enabled=false;
    var velocity=ps.velocityOverLifetime;velocity.enabled=false;
    var force=ps.forceOverLifetime;force.enabled=false;
    var noise=ps.noise;noise.enabled=false;
    var size=ps.sizeOverLifetime;size.enabled=false;
    var color=ps.colorOverLifetime;color.enabled=false;
    main.gravityModifier=0;
    main.useUnscaledTime=true;main.stopAction=ParticleSystemStopAction.None;
    ps.SetParticles(particles,n);
    ps.Pause(false);
    clouds.Add(cloud);
   }
   gameObject.name="Amplifier_Cast_Finish_Spiral_Burst";
   Debug.Log("[Yulia amplifier cast finish] Burst: orbit flares and rapidly opens into curved outward paths, then fades.");
  }
  void OnDestroy(){if(ribbonMaterial)Destroy(ribbonMaterial);}
  Vector3 PositionAt(Cloud cloud,int index,float time) {
   var offset=cloud.origins[index]-orbitCenter;
   if(time<0)return orbitCenter+Quaternion.AngleAxis(time*900f,Vector3.up)*offset;
   float p=Mathf.Clamp01(time/Seconds);
   float spread=1.35f*(1-Mathf.Exp(-time/.045f))+.45f*p;
   float angle=108f*(1-Mathf.Exp(-time/.12f))+180f*time;
   return orbitCenter+Quaternion.AngleAxis(angle,Vector3.up)*(offset+cloud.directions[index]*spread);
  }
  void Update() {
   elapsed=Time.unscaledTime-started;
   float p=Mathf.Clamp01(elapsed/Seconds);
   float opacity=1-Mathf.SmoothStep(.18f,1,p);
   float flash=1+1.25f*Mathf.Exp(-elapsed/.035f);
   foreach(var cloud in clouds) {
    if(!cloud.system) continue;
    for(int i=0;i<cloud.particles.Length;i++) {
     var position=PositionAt(cloud,i,elapsed);
     if(cloud.ribbons[i]) {
      // Rebuild a short section of the curved trajectory, never a center-to-tip spoke.
      var points=new Vector3[16];
      for(int k=0;k<points.Length;k++)points[k]=PositionAt(cloud,i,elapsed-k*.008f);
      cloud.ribbons[i].positionCount=points.Length;cloud.ribbons[i].SetPositions(points);
      cloud.ribbons[i].startColor=new Color(1,.65f,1,opacity*.85f);
      cloud.ribbons[i].endColor=new Color(.65f,.15f,1,0);
     }
     cloud.particles[i].position=cloud.world ? position : cloud.basis.InverseTransformPoint(position);
     cloud.particles[i].startColor=new Color(cloud.colors[i].r,cloud.colors[i].g,cloud.colors[i].b,cloud.colors[i].a*opacity);
     cloud.particles[i].startSize3D=cloud.sizes[i]*Mathf.Lerp(1.4f,.45f,p);
     cloud.particles[i].remainingLifetime=Seconds+.2f;
    }
    cloud.system.SetParticles(cloud.particles,cloud.particles.Length);
   }
   if(glow) {
    glow.transform.localScale=glowScale*Mathf.Lerp(1.35f,.65f,p);
    for(int i=0;i<glowGraphs.Length;i++) if(glowGraphs[i] && glowColors[i]!=null) {
     var keys=glowColors[i].colorKeys;
     foreach(var index in System.Linq.Enumerable.Range(0,keys.Length)) keys[index].color*=opacity*flash;
     var alphas=glowColors[i].alphaKeys;
     for(int a=0;a<alphas.Length;a++) alphas[a].alpha*=1-p;
     var gradient=new Gradient();gradient.SetKeys(keys,alphas);glowGraphs[i].SetGradient("Color",gradient);
    }
   }
   if(p>=1) {if(glow && glow!=gameObject) Destroy(glow);Destroy(gameObject);}
  }
 }
}







