using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using JC.BattleTesting;
namespace JC.EnvironmentDetail
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcBattleRoadMarkings : JcRoadMarkingControl
    {
        [Tooltip("이 전투씬의 차선 프로필입니다. 인스펙터 버튼으로 플레이 중 저장·적용할 수 있습니다.")]
        public JcBattleRoadProfile profile;
        public override bool IsBattle => true;
        public override JcRoadMarkingProfile CurrentProfile { get=>profile; set=>profile=value as JcBattleRoadProfile; }
        readonly HashSet<JcBattleSurfaceDressing> bound=new HashSet<JcBattleSurfaceDressing>();
        protected override void RefreshTargets(bool changed)
        {
            bound.RemoveWhere(x=>x==null);
            int count=0;
            foreach(var target in gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<JcBattleSurfaceDressing>(true)))
            {
                if(target.LineTargetCount==0)continue;
                bool added=bound.Add(target);
                if(changed||added)target.SetRoadValues(Values);
                count+=target.LineTargetCount;
            }
            Status=count==0?"적용할 도로 차선 없음 (배경 대기/실내)":$"전투 차선 {count}개 구간 적용";
        }
        public static void BindScene(Scene scene)
        {
            foreach(var control in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<JcBattleRoadMarkings>(true)))
                if(control.isActiveAndEnabled)control.RefreshNow();
        }
    }
}
