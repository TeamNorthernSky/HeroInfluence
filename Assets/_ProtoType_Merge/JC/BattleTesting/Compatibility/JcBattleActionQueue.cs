// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/BattleActionQueue.cs
// 원본 객체: BattleActionQueue -> JcBattleActionQueue
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections;
using System.Collections.Generic;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// IBattleActionCommand를 순서대로 실행합니다.
    /// </summary>
    public class JcBattleActionQueue
    {
        private readonly Queue<JcIBattleActionCommand> _queue = new Queue<JcIBattleActionCommand>();

        public int Count => _queue.Count;

        // 원본 함수 대응: BattleActionQueue.Enqueue (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/BattleActionQueue.cs)

        public void Enqueue(JcIBattleActionCommand command)
        {
            if (command != null)
            {
                _queue.Enqueue(command);
            }
        }

        public void Clear() => _queue.Clear();

        /// <summary>선입선출로 하나 꺼낸다. 비어 있으면 null.</summary>
        public JcIBattleActionCommand Dequeue() => _queue.Count > 0 ? _queue.Dequeue() : null;

        /// <summary>
        /// 큐가 빌 때까지 순서대로 실행한다.
        /// 매 바퀴 Count를 다시 보므로, 실행 중에 추가된 액션도 같은 루프가 이어서 처리한다(연쇄).
        /// </summary>
        // 원본 함수 대응: BattleActionQueue.RunAll (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/BattleActionQueue.cs)
        public IEnumerator RunAll(JcBattleManager battleManager)
        {
            while (_queue.Count > 0)
            {
                JcIBattleActionCommand command = _queue.Dequeue();
                yield return battleManager.StartCoroutine(command.Execute(battleManager));
            }
        }
    }
}
