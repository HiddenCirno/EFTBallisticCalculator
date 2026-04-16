using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using EFT.HealthSystem;
using Fika.Core.Main.Components;
using Fika.Core.Main.ObservedClasses;

namespace EFTBallisticCalculator.HUD
{
    /// <summary>
    /// Fika 专属的隔离工具类。
    /// 必须使用 NoInlining 标签，防止单机环境下 JIT 编译器提前加载 Fika 类型导致游戏报错。
    /// </summary>
    public static class FikaIntegration
    {
        //规避NetworksoftJson的自动扫描构建
        //你丫是不是手贱....
        private static object _cachedCoopHandlerObj = null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdateFikaTeammateStatus(Dictionary<string, TeamPanel.TeammateRecord> roster)
        {
            // 1. 获取 Fika 的 CoopHandler 实例
            if (_cachedCoopHandlerObj == null)
            {
                // FindObjectOfType 返回的是 UnityEngine.Object，可以直接存进 object 里
                _cachedCoopHandlerObj = UnityEngine.Object.FindObjectOfType<CoopHandler>();
                if (_cachedCoopHandlerObj == null) return;
            }
            var handler = (CoopHandler)_cachedCoopHandlerObj;

            var fikaPlayers = handler.Players;
            var extractedNetIds = handler.ExtractedPlayers;

            if (fikaPlayers == null || extractedNetIds == null) return;

            // 2. 映射 ProfileID -> NetID 并检查撤离
            foreach (var kvp in roster)
            {
                var record = kvp.Value;

                if (record.Status == ETeammateStatus.Alive)
                {
                    int matchedNetId = -1;

                    // 遍历寻找当前队友对应的 NetId
                    foreach (var fikaKvp in fikaPlayers)
                    {
                        if (fikaKvp.Value.ProfileId == record.AccountId)
                        {
                            matchedNetId = fikaKvp.Key;
                            break;
                        }
                    }

                    // 如果该 NetId 存在于 Fika 的官方撤离列表里，实锤撤离
                    if (matchedNetId != -1 && extractedNetIds.Contains(matchedNetId))
                    {
                        record.Status = ETeammateStatus.Extracted;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TryGetFikaStats(IHealthController healthCtrl, out float hydr, out float hydrmax, out float energy, out float energymax)
        {
            hydr = 0f; hydrmax = 100f; energy = 0f; energymax = 100f;

            // 确认是远端玩家的组件
            if (healthCtrl is ObservedHealthController fikaHealth)
            {
                try
                {
                    hydr = fikaHealth.HealthValue_1.Current;
                    hydrmax = fikaHealth.HealthValue_1.Maximum;

                    energy = fikaHealth.HealthValue_0.Current;
                    energymax = fikaHealth.HealthValue_0.Maximum;
                }
                catch (Exception)
                {
                    // 仅保留一个兜底，防止万一数据结构有变动导致报错
                }
            }
        }
    }
}