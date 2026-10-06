using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace JC.BattleTesting.Editor
{
    // 사용자 승인한 실제 Play 검증용 도구입니다. HP·계수·페이즈를 강제 변경하지 않습니다.
    // 아군의 정상 턴 권한을 확보하고 유효한 스킬/대상을 선택한 뒤 같은 전투 실행기를 사용합니다.
    public static class JcFullBattleProbe
    {
        private static JcBattleTestSession session;
        private static string path;
        private static bool running;
        private static int lastPhase, turns, skills, deaths, revives;
        private static int phaseTwoRound, phaseThreeRound;
        private static string ended="None";
        private static readonly HashSet<BattleCharactor> watched=new HashSet<BattleCharactor>();
        private static float started;
        public static string Start(string outputPath,bool tactical=true,bool observeStates=false)
        {
            if(!Application.isPlaying||running)throw new InvalidOperationException("새 Play 검증이 필요합니다.");
            session=UnityEngine.Object.FindFirstObjectByType<JcBattleTestSession>();path=outputPath;
            File.WriteAllText(path,"JC full battle actual Play probe\n");started=Time.realtimeSinceStartup;running=true;ended="None";turns=skills=deaths=revives=0;lastPhase=0;phaseTwoRound=phaseThreeRound=0;watched.Clear();
            session.flow.OnTurnResolved+=Turn;session.flow.OnBattleEnded+=End;session.execution.OnSkillResolved+=Skill;
            UnityEngine.Object.FindFirstObjectByType<JcAutoBattleController>().IsAutoBattle=!tactical;
            session.execution.ChangeBattleSpeed(2);
            var input=UnityEngine.Object.FindFirstObjectByType<JcInputHandler>();input.IsAutoBattleActive=tactical;
            session.StartCoroutine(Run(input,tactical,observeStates));
            return "Actual Play probe started: "+path;
        }
        private static void Log(string value)=>File.AppendAllText(path,$"{Time.realtimeSinceStartup-started:F2}s {value}\n");
        private static void Turn(TurnResolutionContext c){turns++;Log("TURN "+c.RoundIndex+" "+c.Actor?.TemplateIndex+" skipped="+c.WasSkipped+" phase="+session.Rules.Phase+" charge="+session.Rules.GetCharge(40002)+"/"+session.Rules.GetCharge(40003)+" overload="+session.Rules.Overload);}
        private static void Skill(SkillResolutionContext c){skills++;Log("SKILL "+c.Actor?.TemplateIndex+" id="+c.Skill?.skillIndex+" success="+c.Success+" damage="+c.TotalAppliedDamage);}
        private static void End(BattleResult result){ended=result.ToString();Log("RESULT "+ended);running=false;}
        private static IEnumerator Run(JcInputHandler input,bool tactical,bool observeStates)
        {
            try
            {
                while(running && session!=null && Time.realtimeSinceStartup-started<600)
                {
                    int phase=session.Rules?.Phase??0;
                    if(phase!=lastPhase){lastPhase=phase;if(phase==2)phaseTwoRound=session.flow.RoundIndex;if(phase==3)phaseThreeRound=session.flow.RoundIndex;Log("PHASE "+phase);ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path,null)+"_phase"+phase+".png");}
                    foreach(var u in session.flow.Participants)
                    {
                        if(u==null||!watched.Add(u))continue;
                        u.OnDied+=OnDeath;float previous=u.CurrentHp;
                        u.OnHpChanged+=(hp,max)=>{if(previous<=0&&hp>0){revives++;Log("REVIVE "+u.TemplateIndex+" hp="+hp+"/"+max);}previous=hp;};
                        Log("SPAWN "+u.TemplateIndex+" hp="+u.CurrentHp+" rank="+u.Level);
                    }
                    var actor=session.flow.CurrentUnit;
                    if(tactical && actor!=null && actor.IsPlayer && !actor.IsDead && !session.flow.IsActionInProgress && !session.flow.IsTurnPresentationPending && !session.flow.IsFlowBlocked)
                    {
                        var options=new List<(SkillData skill,BattleCharactor target,float score)>();
                        foreach(var skill in actor.availableSkills.Concat(actor.EquippedWeaponData!=null?new[]{actor.EquippedWeaponData.ToSkillData()}:Array.Empty<SkillData>()))
                        {
                            if(skill==null||actor.CurrentInfluence<skill.IPCost)continue;
                            foreach(var target in TargetingHelper.GetValidTargetsForSkillData(actor,skill))
                            {
                                if(target==null)continue;
                                if(observeStates&&phase==1&&session.flow.RoundIndex<6&&!target.IsPlayer&&target.TemplateIndex!="40005")continue;
                                // 2페이즈에서 복구 기믹을 실제로 관찰한 뒤 본체를 공격합니다. HP/페이즈 강제 변경은 하지 않습니다.
                                if(phase==2&&target.TemplateIndex=="40001"&&session.flow.RoundIndex<phaseTwoRound+3)continue;
                                if(observeStates&&phase==3&&target.TemplateIndex=="40004"&&session.flow.RoundIndex<phaseThreeRound+5)continue;
                                if(observeStates&&phase==3&&(target.TemplateIndex=="40002"||target.TemplateIndex=="40003")&&session.flow.RoundIndex<phaseThreeRound+2)continue;
                                float score;
                                if(!target.IsPlayer&&!target.IsDead)
                                    score=(phase==1&&(target.TemplateIndex=="40002"||target.TemplateIndex=="40003")?200:phase==3&&target.TemplateIndex=="40004"?180:target.TemplateIndex=="40005"?100:80)+Mathf.Min(skill.skillValue,3);
                                else if(target.IsPlayer&&target.IsDead&&skill.classSkillEffect==2)score=250;
                                else if(target.IsPlayer&&!target.IsDead&&skill.classSkillEffect==1&&target.CurrentHp<target.MaxHp*.65f)score=220+(1-target.CurrentHp/target.MaxHp)*10;
                                else continue;
                                options.Add((skill,target,score));
                            }
                        }
                        if(session.flow.TryClaimPlayerAction(actor))
                        {
                            if(options.Count==0){Log("PASS "+actor.TemplateIndex);input.ResolveAutoBattleAction(actor,null);}
                            else
                            {
                                var action=options.OrderByDescending(x=>x.score).First();bool success=false;
                                actor.SetClassSkillIndex(action.skill.skillIndex);actor.ResolveSelectedSkill(false);
                                Log("SELECT "+actor.TemplateIndex+" "+action.skill.skillIndex+" target="+action.target.TemplateIndex);
                                yield return session.execution.ExecuteGridSkill(actor,action.target,action.skill,ok=>success=ok);
                                input.ResolveAutoBattleAction(actor,success?action.target:null);
                            }
                        }
                    }
                    yield return null;
                }
                if(running){running=false;Log("TIMEOUT "+Snapshot());}
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path,null)+"_result.png");
            }
            finally
            {
                if(session!=null){session.flow.OnTurnResolved-=Turn;session.flow.OnBattleEnded-=End;session.execution.OnSkillResolved-=Skill;}
                foreach(var unit in watched)if(unit!=null)unit.OnDied-=OnDeath;
                Log("SUMMARY turns="+turns+" skills="+skills+" deaths="+deaths+" revives="+revives+" result="+ended);
                running=false;
            }
        }
        private static void OnDeath(BattleCharactor u){deaths++;Log("DEATH "+u.TemplateIndex);}
        public static string Snapshot()
        {
            if(session==null)return "not started";
            return "running="+running+" result="+ended+" phase="+session.Rules?.Phase+" round="+session.flow.RoundIndex+" current="+session.flow.CurrentUnit?.TemplateIndex+" action="+session.flow.IsActionInProgress+" turns="+turns+" skills="+skills+"\n"+
                string.Join("\n",session.flow.Participants.Where(u=>u!=null).Select(u=>u.TemplateIndex+" HP="+u.CurrentHp+"/"+u.MaxHp));
        }
    }
}
