using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;

namespace ASB.Work.EditorTools.Jig
{
    [MenuEntry("Skill Presentation Jig에서 열기", 5000)]
    public sealed class OpenSkillPresentationJigTimelineAction : TimelineAction
    {
        public override ActionValidity Validate(ActionContext context)
        {
            return TimelineEditor.inspectedAsset != null
                ? ActionValidity.Valid
                : ActionValidity.NotApplicable;
        }

        public override bool Execute(ActionContext context)
        {
            if (TimelineEditor.inspectedAsset == null) return false;
            SkillPresentationJigWindow.Open(TimelineEditor.inspectedAsset);
            return true;
        }
    }
}
