using System.Linq;
using UnityEngine;

namespace JC.BattleTesting.Vfx
{
    // 대응: ASB AmplifierOrbHealthVisual.HandleHpChanged, BattleCharactor.OnHpChanged/OnDied,
    // UnitVisualProfile의 외형/소켓. 원본 모델은 JC 런타임 인스턴스에서만 제어합니다.
    // 참고: motion_09(굴렁쇠), motion_10(정지장), 애니메이션 기획 slide 11~12(증폭기 소멸/복구).
    public sealed class JcYuliaUnitVfx : MonoBehaviour
    {
        private BattleCharactor actor;
        private JcYuliaVfxDirector director;
        private Transform orb, wheel;
        private Vector3 orbScale, wheelHome;
        private float lastHp, orbAmount=1, orbTarget=1, shake, spawnElapsed, wheelSpin;
        private int energy;
        private bool initialized, isWheel, isVeil;
        private JcYuliaVfxEffect shell, idle, charged;
        private Mesh wheelMesh;
        public Transform OrbAnchor => orb != null ? orb : transform;
        public Transform WheelVisual => wheel;

        public void Configure(BattleCharactor unit, JcYuliaVfxDirector owner)
        {
            actor=unit;director=owner;lastHp=unit.CurrentHp;energy=unit.EnergyStack;
            isWheel=unit.TemplateIndex=="40005";isVeil=unit.TemplateIndex=="40004";
            foreach(var old in GetComponentsInChildren<AmplifierOrbHealthVisual>(true)) old.enabled=false;
            orb=GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="2010_Orb");
            if(orb!=null){orbScale=orb.localScale;orb.gameObject.SetActive(true);}
            actor.OnHpChanged+=Hp; actor.OnDied+=Died;
            if(isWheel) BuildWheel();
            if(isVeil)
            {
                actor.EnsureAnimationController();var animator=actor.Anim?.Animator;
                if(animator!=null)
                {
                    int hit=Animator.StringToHash("Base Layer.Hit");
                    if(animator.HasState(0,hit)) {animator.Play(hit,0,.35f);animator.Update(0);}
                    animator.enabled=false;
                }
                shell=director.Show(JcYuliaEffectKind.Veil,transform.position,transform,true);
            }
            else if(unit.TemplateIndex=="40001")
            {
                var socket=GetComponent<UnitVisualProfile>()?.AttackEffectSocket;
                if(socket==null)socket=GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="R_HandSocket");
                if(socket!=null) idle=director.Show(JcYuliaEffectKind.Charge,socket.position,socket,true);
            }
            initialized=true;
        }
        private void Hp(float hp,float max)
        {
            if(!initialized)return;
            if(hp>0 && lastHp<=0)
            {
                orbTarget=1;if(orb!=null)orb.gameObject.SetActive(true);
                director.Show(JcYuliaEffectKind.Recover,OrbAnchor.position);
            }
            else if(hp<lastHp)
            {
                shake=.22f;director.Show(JcYuliaEffectKind.Pulse,transform.position+Vector3.up*.65f);
            }
            lastHp=hp;
        }
        private void Died(BattleCharactor dead)
        {
            orbTarget=0;
            director.Show(isWheel?JcYuliaEffectKind.WheelBreak:JcYuliaEffectKind.Smoke,isWheel?wheel.position:OrbAnchor.position+Vector3.up*.35f);
            if(shell!=null)shell.Stop();if(idle!=null)idle.Stop();
            if(charged!=null)charged.Stop();charged=null;
            if(wheel!=null)wheel.gameObject.SetActive(false);
        }
        private void Update()
        {
            if(!initialized)return;
            float speed=JcBattleManager.Instance!=null?JcBattleManager.Instance.CurrentBattleSpeed:1;
            float dt=Time.deltaTime*speed;spawnElapsed+=dt;
            int charge=director.VisualCharge(actor);
            if(charge!=energy)
            {
                energy=charge;if(energy>0&&!actor.IsDead)director.Show(JcYuliaEffectKind.Charge,OrbAnchor.position);
            }
            if(orb!=null&&!actor.IsDead&&energy>0)
            {
                if(charged==null)charged=director.Show(JcYuliaEffectKind.Charge,orb.position,orb,true);
                // 시각 크기만 제한합니다. 실제 전투 스택에는 상한을 추가하지 않습니다.
                charged.transform.localScale=Vector3.one*(1+Mathf.Min(energy,5)*.1f);
                charged.PlaybackSpeed=speed;
            }
            else if(charged!=null){charged.Stop();charged=null;}
            if(orb!=null)
            {
                float duration=orbTarget==0?director.preset.destroySeconds:director.preset.recoverSeconds;
                orbAmount=Mathf.MoveTowards(orbAmount,orbTarget,dt/Mathf.Max(.05f,duration));
                orb.localScale=orbScale*orbAmount;if(orbAmount<=0)orb.gameObject.SetActive(false);
            }
            if(wheel!=null && !actor.IsDead)
            {
                var p=wheelHome;p.y+=Mathf.Sin(spawnElapsed*2.3f)*director.preset.hoverHeight;
                p.y-=Mathf.Max(0,1-spawnElapsed/director.preset.summonSeconds)*.7f;
                wheel.localPosition=p;
                wheelSpin=(wheelSpin+dt*35)%360;
                if(Camera.main!=null)wheel.rotation=Quaternion.LookRotation(Camera.main.transform.position-wheel.position,Vector3.up)*Quaternion.Euler(0,0,wheelSpin);
            }
            if(shake>0)
            {
                shake=Mathf.Max(0,shake-dt);
                if(shell!=null)shell.transform.localPosition=Vector3.right*(Mathf.Sin(shake*110)*shake*.13f);
            }
            else if(shell!=null)shell.transform.localPosition=Vector3.zero;
            if(shell!=null)shell.PlaybackSpeed=speed;if(idle!=null)idle.PlaybackSpeed=speed;
        }
        private void BuildWheel()
        {
            foreach(var r in GetComponentsInChildren<MeshRenderer>(true))r.enabled=false;
            wheel=new GameObject("JC_DespairWheelVisual").transform;wheel.SetParent(transform,false);wheelHome=Vector3.up*.72f;
            wheel.localPosition=wheelHome;wheelMesh=Torus(.48f,.055f);
            var filter=wheel.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=wheelMesh;
            var renderer=wheel.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=director.preset.frameMaterial;
            // motion_09의 금속 외곽을 간단한 8개 프레임으로 표시합니다. 정식 모델 대신 쓰는 임시 외형입니다.
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;var frame=new GameObject("WheelFramePlate");frame.transform.SetParent(wheel,false);
                frame.transform.localPosition=new Vector3(Mathf.Cos(a)*.48f,Mathf.Sin(a)*.48f,0);
                frame.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
                frame.transform.localScale=new Vector3(.16f,.09f,.12f);
                frame.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                frame.AddComponent<MeshRenderer>().sharedMaterial=director.preset.frameMaterial;
            }
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("VioletWheelArc");go.transform.SetParent(wheel,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=director.preset.energyMaterial;line.useWorldSpace=false;
                line.positionCount=65;line.widthMultiplier=director.preset.lineWidth*(i==0?.7f:.35f);
                line.startColor=line.endColor=i==0?director.preset.core:director.preset.energy;
                for(int n=0;n<65;n++){float a=n/64f*Mathf.PI*2;float radius=.38f+i*.07f;line.SetPosition(n,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,.012f));}
            }
            director.Show(JcYuliaEffectKind.Summon,transform.position);
        }
        private static Mesh Torus(float radius,float tube)
        {
            const int rings=48,sides=8;var v=new Vector3[(rings+1)*(sides+1)];var uv=new Vector2[v.Length];var triangles=new int[rings*sides*6];
            for(int r=0;r<=rings;r++)for(int s=0;s<=sides;s++)
            {
                float a=r/(float)rings*Mathf.PI*2,b=s/(float)sides*Mathf.PI*2;int n=r*(sides+1)+s;
                v[n]=new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube);uv[n]=new Vector2(r/(float)rings,s/(float)sides);
            }
            int t=0;for(int r=0;r<rings;r++)for(int s=0;s<sides;s++){int n=r*(sides+1)+s;triangles[t++]=n;triangles[t++]=n+1;triangles[t++]=n+sides+1;triangles[t++]=n+1;triangles[t++]=n+sides+2;triangles[t++]=n+sides+1;}
            var mesh=new Mesh{name="JC_TemporaryDespairWheel"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void OnDisable()
        {
            if(actor!=null){actor.OnHpChanged-=Hp;actor.OnDied-=Died;}
            if(shell!=null)shell.Stop();if(idle!=null)idle.Stop();
            if(charged!=null)charged.Stop();
        }
        private void OnDestroy(){if(wheelMesh!=null)Destroy(wheelMesh);}
    }
}
