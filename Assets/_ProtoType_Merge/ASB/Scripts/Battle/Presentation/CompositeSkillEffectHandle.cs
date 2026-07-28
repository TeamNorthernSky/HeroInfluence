using System.Collections.Generic;

/// <summary>
/// 여러 유지형 이펙트(예: EachTarget으로 대상마다 스폰된 N개)를 하나의 InstanceKey로 묶어
/// Signal/Stop을 일괄 전달하는 Handle. UnitEffectPresenter가 다중 스폰 시 사용한다.
/// </summary>
public sealed class CompositeSkillEffectHandle : ISkillEffectHandle
{
    private readonly List<ISkillEffectHandle> _children;

    public CompositeSkillEffectHandle(List<ISkillEffectHandle> children)
    {
        _children = children;
    }

    public bool Signal(SkillEffectContext ctx)
    {
        if (_children != null)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i]?.Signal(ctx);
            }
        }
        return true; // 모든 자식에 신호 후 Handle 수명 종료.
    }

    public void Stop()
    {
        if (_children == null)
        {
            return;
        }
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i]?.Stop();
        }
        _children.Clear();
    }
}
