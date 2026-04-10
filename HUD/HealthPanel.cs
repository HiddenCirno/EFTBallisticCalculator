using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        // ==========================================
        // 性能优化：静态预分配内存，避免 OnGUI 中产生 GC 垃圾
        // ==========================================
        private class AggregatedBuff
        {
            public string BaseName;
            public float ValueSum;
            public string ValueSuffix;
            public bool HasValue;
            public float MaxTimeLeft;
            public bool IsDebuff;
        }

        private static readonly List<string> _activeBuffs = new List<string>();
        private static readonly List<string> _activeDebuffs = new List<string>();

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

                var activeEffects = healthCtrl.GetAllActiveEffects(part);

                if (activeEffects != null)
                {
                    foreach (var effect in activeEffects)
                    {
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
                        if (!effectName.IsNullOrEmpty() && notInBlackList && part != EBodyPart.Head && part != EBodyPart.Chest)
                        {
                            statusText += $"[{effectName}] ";//.Localized()
                        }
                    }
                }

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
            float weightLimit = weight >= overWeight ? maxWeight : overWeight;

            string weightStatus = "[NORMAL]";
            if (weight >= overWeight) weightStatus = "[OVERWEIGHT]";
            if (weight >= maxWeight) weightStatus = "[CRITICAL]";

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"WEIGHT  : {weight:F2}/{weightLimit:F0} {weightStatus}", mainColor, textStyle); currentY += lh;

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"OXYGEN : {physCtrl.Oxygen.Current:F1}/{physCtrl.Oxygen.TotalCapacity.Value:F1} ({(physCtrl.Oxygen.Current / physCtrl.Oxygen.TotalCapacity * 100):F0}%)", mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"UPPER STM : {physCtrl.HandsStamina.Current:F1}/{physCtrl.HandsStamina.TotalCapacity.Value:F1} ({(physCtrl.HandsStamina.Current / physCtrl.HandsStamina.TotalCapacity * 100):F0}%)", mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"LOWER STM : {physCtrl.Stamina.Current:F1}/{physCtrl.Stamina.TotalCapacity.Value:F1} ({(physCtrl.Stamina.Current / physCtrl.Stamina.TotalCapacity * 100):F0}%)", mainColor, textStyle); currentY += lh;

            var buffs = ActiveBuffManager.PositiveEffects;
            var debuffs = ActiveBuffManager.NegativeEffects;

            float buffPanelWidth = 150f * finalScale;
            float buffStartX = finalX - buffPanelWidth;
            float buffY = finalY;

            if (buffs.Count > 0)
            {
                HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), "<b>[ ACTIVE ]</b>", mainColor, titleStyle);
                buffY += lh;
                foreach (var buff in buffs)
                {
                    string timeStr = buff.TimeLeft > 0 ? $" ({buff.TimeLeft:F0}s)" : "";
                    string valueStr = buff.Strength != 0 ? $" {(buff.Strength > 0 ? "+" : "")}{buff.Strength:G3}" : "";
                    string display = $"{buff.Name}{valueStr}{timeStr}";
                    HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), display, mainColor, textStyle);
                    buffY += lh;
                }
                buffY += 5f * finalScale;
            }

            if (debuffs.Count > 0)
            {
                HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), "<b>[ WARNING ]</b>", UnityEngine.Color.red, titleStyle);
                buffY += lh;
                foreach (var debuff in debuffs)
                {
                    string timeStr = debuff.TimeLeft > 0 ? $" ({debuff.TimeLeft:F0}s)" : "";
                    string valueStr = debuff.Strength != 0 ? $" {(debuff.Strength > 0 ? "+" : "")}{debuff.Strength:G3}" : "";
                    string display = $"{debuff.Name}{valueStr}{timeStr}";
                    HUDManager.DrawShadowLabel(new Rect(buffStartX, buffY, buffPanelWidth, lh), display, UnityEngine.Color.red, textStyle);
                    buffY += lh;
                }
            }

            // 返回真实高度
            return currentY;
        }
    }
}