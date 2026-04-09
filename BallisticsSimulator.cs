using UnityEngine;
using EFT.Ballistics; // 引入 G1 阻力计算

namespace EFTBallisticCalculator
{
    /// <summary>
    /// 核心弹道解算器类，使用近似塔科夫 G1 空气动力学模型的算法模拟弹道轨迹
    /// </summary>
    public static class BallisticsSimulator
    {
        private static readonly Vector3 Gravity = Physics.gravity;

        public static Vector3 SimulateToHorizontalDistance(
         Vector3 startPos, Vector3 vel, float mass, float diam, float bc, float targetH, out float timeOfFlight)
        {
            float mKg = mass / 1000f;
            float dM = diam / 1000f;
            float dragMult = (mKg * 0.0014223f) / (dM * dM * bc);
            float airFactor = 1.2f * (dM * dM * Mathf.PI / 4f);

            Vector3 pos = startPos;
            float dt = 0.01f;
            timeOfFlight = 0f;

            for (int tick = 0; tick < 1000; tick++)
            {
                float vMag = vel.magnitude;
                float dragAcc = EftBulletClass.CalculateG1DragCoefficient(vMag) * dragMult;
                Vector3 acc = Gravity + (airFactor * -dragAcc * vMag * vMag / (mKg * 2f)) * vel.normalized;

                Vector3 nextPos = pos + vel * dt + 0.5f * acc * dt * dt;
                Vector3 nextVel = vel + acc * dt;

                float currH = new Vector2(pos.x - startPos.x, pos.z - startPos.z).magnitude;
                float nextH = new Vector2(nextPos.x - startPos.x, nextPos.z - startPos.z).magnitude;

                if (nextH >= targetH)
                {
                    float t = (targetH - currH) / (nextH - currH);
                    timeOfFlight += dt * t;
                    return Vector3.Lerp(pos, nextPos, t);
                }

                pos = nextPos;
                vel = nextVel;
                timeOfFlight += dt;
            }
            return pos;
        }
    }
}