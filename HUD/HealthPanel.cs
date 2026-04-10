using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using System;
using System.Linq; // 塔科夫健康系统核心命名空间
using System.Collections.Generic;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class HealthPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        public static void InitCfg(ConfigFile config)
        {
            OffsetX = config.Bind("Health Panel / 健康数据", "X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_x_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Health Panel / 健康数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_y_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Health Panel / 健康数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_scale_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Health Panel / 健康数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_active_name") }));

            Color = config.Bind("Health Panel / 健康数据", "颜色设置", new Color(1f, 0.8f, 0.9f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_color_name") }));
        }

        public static float Draw(float startX, float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return startY;

            // 获取塔科夫健康和物理组件
            var healthCtrl = PluginsCore.CorrectPlayer.ActiveHealthController;
            var physCtrl = PluginsCore.CorrectPlayer.Physical;
            if (healthCtrl == null || physCtrl == null) return startY;

            float finalScale = globalScale * Scale.Value;
            float finalX = startX + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = 350f * finalScale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : Color.Value;

            float currentY = finalY;

            // --- [ 标题 ] ---
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), "<b>[ BIOMETRIC SENSORS ACTIVE ]</b>", mainColor, titleStyle);
            currentY += lh;

            // ==========================================
            // 区块 1：基本生存指标 (Vitals)
            // ==========================================
            float totalHealth = 0f, totalMaxHealth = 0f;
            foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
            {
                if (part == EBodyPart.Common) continue;
                var partData = healthCtrl.GetBodyPartHealth(part);
                totalHealth += partData.Current;
                totalMaxHealth += partData.Maximum;
            }

            // 水分与能量
            string hydStr = $"HYDRATN : {healthCtrl.Hydration.Current:F0}/{healthCtrl.Hydration.Maximum:F0} {healthCtrl.HydrationRate:+0.00;-0.00;0.00}/min";
            string engStr = $"ENERGY  : {healthCtrl.Energy.Current:F0}/{healthCtrl.Energy.Maximum:F0} {healthCtrl.EnergyRate:+0.00;-0.00;0.00}/min";
            string tempStr = $"TEMP    : {healthCtrl.Temperature.Current:F1} °C";

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"OVERALL : {totalHealth:F0}/{totalMaxHealth:F0} {healthCtrl.HealthRate:+0.00;-0.00;0.00}/min", mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), hydStr, mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), engStr, mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), tempStr, mainColor, textStyle); currentY += lh;

            currentY += 5f * finalScale; // 区块间距

            // ==========================================
            // 区块 2：肢体诊断 (Limb Diagnostics) & 原生状态
            // ==========================================
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), "<b>[ LIMB DIAGNOSTICS ]</b>", mainColor, titleStyle); currentY += lh;

            // 遍历所有肢体部位
            EBodyPart[] partsToDraw = { EBodyPart.Head, EBodyPart.Chest, EBodyPart.Stomach, EBodyPart.LeftArm, EBodyPart.RightArm, EBodyPart.LeftLeg, EBodyPart.RightLeg };
            foreach (var part in partsToDraw)
            {
                var hp = healthCtrl.GetBodyPartHealth(part);
                string partName = part.ToString().Localized();
                string statusText = "";

                // 核心突破：直接调用你找到的原生方法，获取该部位所有激活的负面/正面效果！
                var activeEffects = healthCtrl.GetAllActiveEffects(part);

                if (activeEffects != null)
                {
                    foreach (var effect in activeEffects)
                    {
                        // 塔科夫的 Effect 类名通常是 "Fracture", "HeavyBleeding", "LightBleeding"
                        // 偶尔可能带后缀，保险起见我们 Replace 掉 "Effect" 字眼，然后直接扔给原生字典去翻译
                        var variation = effect.DisplayableVariations?.FirstOrDefault();

                        string effectName = (variation != null && variation.BuffType != GClass3056.EBuffType.Stimulant)
                            ? variation.Buffs?.FirstOrDefault()?.Text ?? ""
                            : "";
                        var notInBlackList = (
                            effectName != "SevereMusclePain" && 
                            effectName != "MildMusclePain" &&
                            effectName != "OVERWEIGHT_EFFECT_OVERWEIGHT" &&
                            effectName != "OVERWEIGHT_EFFECT_HUGE_OVERWEIGHT" &&
                            effectName != "Exhaustion"
                            );
                        if (!effectName.IsNullOrEmpty() && notInBlackList && part!=EBodyPart.Head && part!=EBodyPart.Chest)
                        {
                            statusText += $"[{effectName}] ";//.Localized()
                        }
                    }
                }

                // 如果没有检测到任何特殊状态，给一个兜底显示
                if (string.IsNullOrEmpty(statusText))
                {
                    statusText = hp.Current <= 0 ? "[损毁]" : "[OK]";
                }

                string line = $"{partName.PadRight(10)} : {hp.Current:F0}/{hp.Maximum:F0}  {statusText}";
                HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), line, mainColor, textStyle); currentY += lh;
            }

            currentY += 5f * finalScale;

            // ==========================================
            // 区块 3：战斗与耐力 (Combat & Stamina)
            // ==========================================
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), "<b>[ COMBAT & STAMINA ]</b>", mainColor, titleStyle); currentY += lh;

            float weight = physCtrl.IobserverToPlayerBridge_0.TotalWeight;
            float overWeight = physCtrl.BaseOverweightLimits.x;
            float maxWeight = physCtrl.BaseOverweightLimits.y;
            float weightLimit = weight>= overWeight ? maxWeight : overWeight;

            // 简单判断状态
            string weightStatus = "[NORMAL]";
            if (weight >= overWeight) weightStatus = "[OVERWEIGHT]";
            if (weight >= maxWeight) weightStatus = "[CRITICAL]";

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"WEIGHT  : {weight:F2}/{weightLimit} {weightStatus}", mainColor, textStyle); currentY += lh;

            // 手部（上肢）耐力与腿部（下肢）耐力
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"OXYGEN : {physCtrl.Oxygen.Current:F1}/{physCtrl.Oxygen.TotalCapacity.Value:F1} ({(physCtrl.Oxygen.Current / physCtrl.Oxygen.TotalCapacity * 100):F0}%)", mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"UPPER STM : {physCtrl.HandsStamina.Current:F1}/{physCtrl.HandsStamina.TotalCapacity.Value:F1} ({(physCtrl.HandsStamina.Current / physCtrl.HandsStamina.TotalCapacity * 100):F0}%)", mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"LOWER STM : {physCtrl.Stamina.Current:F1}/{physCtrl.Stamina.TotalCapacity.Value:F1} ({(physCtrl.Stamina.Current / physCtrl.Stamina.TotalCapacity * 100):F0}%)", mainColor, textStyle); currentY += lh;
            var globalEffects = healthCtrl.GetAllActiveEffects(EBodyPart.Head);

            // 1. 改为使用 List 来收集独立的状态条目
            List<string> activeBuffs = new List<string>();
            List<string> activeDebuffs = new List<string>();

            if (globalEffects != null)
            {
                foreach (var effect in globalEffects)
                {
                    var variation = effect.DisplayableVariations?.FirstOrDefault();
                    if (variation == null) continue;

                    string buffName = variation.Buffs?.FirstOrDefault()?.Text ?? "";
                    if (string.IsNullOrEmpty(buffName)) continue;

                    bool isStimulant = variation.BuffType == GClass3056.EBuffType.Stimulant;
                    bool isPainkiller = buffName.Contains("Pain") || buffName.Contains("Analgesic");
                    bool isDebuff = buffName == "Tremor" || buffName == "Toxication" || buffName == "Dehydration" || buffName == "Contusion";

                    if (isStimulant || isPainkiller || isDebuff)
                    {
                        float timeLeft = effect.TimeLeft;
                        // 改成更紧凑的垂直显示格式： 止痛药 (120S)
                        string timeStr = $" ({timeLeft:F0}S)";
                        string formattedBuff = $"{buffName.Localized()}{timeStr}";

                        if (isDebuff) activeDebuffs.Add(formattedBuff);
                        else activeBuffs.Add(formattedBuff);
                    }
                }
            }

            // 2. 独立坐标轴渲染 (绘制在左侧)
            // 假设留出 150 个像素的宽度给 Buff 栏
            float buffPanelWidth = 150f * finalScale;
            float buffStartX = finalX - buffPanelWidth; // 往左推移
            float buffY = finalY; // 和健康面板的顶部对齐，独立向下延伸

            // 绘制增益 Buff 列表
            if (activeBuffs.Count > 0)
            {
                HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), "<b>[ ACTIVE ]</b>", mainColor, titleStyle);
                buffY += lh;
                foreach (var buff in activeBuffs)
                {
                    HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), buff, mainColor, textStyle);
                    buffY += lh;
                }
                buffY += 5f * finalScale; // 留点间距
            }

            // 绘制减益 Debuff 列表
            if (activeDebuffs.Count > 0)
            {
                HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), "<b>[ WARNING ]</b>", UnityEngine.Color.red, titleStyle);
                buffY += lh;
                foreach (var debuff in activeDebuffs)
                {
                    HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), debuff, UnityEngine.Color.red, textStyle);
                    buffY += lh;
                }
            }

            // 返回真实高度
            return currentY;
        }
    }
}