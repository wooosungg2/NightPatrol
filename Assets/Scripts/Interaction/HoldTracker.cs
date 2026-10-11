using UnityEngine;

namespace NightPatrol.Interaction
{
    /// <summary>
    /// E 길게 누르기 판정 (Unity 입력 없이 테스트). 같은 대상을 보며 누른 채로 holdSeconds가 지나면 한 번 발동한다.
    /// 중간에 떼거나 대상이 바뀌면 처음부터. 발동한 뒤 계속 누르고 있어도 다시 발동하지 않는다 (떼고 다시 눌러야 함)
    /// </summary>
    public sealed class HoldTracker
    {
        private object target;
        private float elapsed;
        private bool armed;

        /// <summary>지금 누르고 있는 정도 (0~1). 누르지 않으면 0</summary>
        public float Progress { get; private set; }

        /// <summary>한 프레임 진행. 이번 프레임에 발동하면 true</summary>
        /// <param name="current">지금 조준 중인 대상 (없으면 null)</param>
        /// <param name="pressedThisFrame">이번 프레임에 눌렀는지</param>
        /// <param name="held">지금 누르고 있는지</param>
        /// <param name="holdSeconds">대상이 요구하는 시간. 0 이하면 누르는 순간 발동</param>
        public bool Update(object current, bool pressedThisFrame, bool held, float holdSeconds, float deltaTime)
        {
            if (current == null || holdSeconds <= 0f)
            {
                Reset();
                return current != null && pressedThisFrame;
            }
            if (pressedThisFrame)
            {
                target = current;
                elapsed = 0f;
                armed = true;
            }
            if (!armed || !held || !ReferenceEquals(current, target))
            {
                Reset();
                return false;
            }
            elapsed += Mathf.Max(0f, deltaTime);
            Progress = Mathf.Clamp01(elapsed / holdSeconds);
            if (elapsed < holdSeconds) return false;
            Reset();
            return true;
        }

        /// <summary>누르기 취소 (막혔을 때, 대상을 놓쳤을 때)</summary>
        public void Reset()
        {
            target = null;
            elapsed = 0f;
            armed = false;
            Progress = 0f;
        }
    }
}
