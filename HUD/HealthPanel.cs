using BepInEx.Configuration;
using EFT;
using System;
using System.Linq;
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
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Health Panel / 健康数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Health Panel / 健康数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Health Panel / 健康数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_active_name") }));

            Color = config.Bind("Health Panel / 健康数据", "颜色设置", new Color(1f, 0.7f, 0.8f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_color_name") }));
        }

        // 返回占用后的最左侧 X 坐标 (现在它就是自身的 finalX - width)
        public static float Draw(float anchorRightX, float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return anchorRightX;

            var healthCtrl = PluginsCore.CorrectPlayer.ActiveHealthController;
            var physCtrl = PluginsCore.CorrectPlayer.Physical;
            if (healthCtrl == null || physCtrl == null) return anchorRightX;

            float finalScale = globalScale * Scale.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = 300f * finalScale;

            // 动态向左推演：从右侧锚点减去自身宽度
            float spacing = 0f * finalScale; // 面板间隙
            float finalX = anchorRightX - rectWidth - spacing + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            float currentY = finalY;

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), "<b>[ BIOMETRIC SENSORS ACTIVE ]</b>", mainColor, titleStyle);
            currentY += lh;

            // --- 区块 1：基本生存指标 ---
            float totalHealth = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Current;
            float totalMaxHealth = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Maximum;

            string hydStr = $"HYDRATN : {healthCtrl.Hydration.Current:F0}/{healthCtrl.Hydration.Maximum:F0} {healthCtrl.HydrationRate:+0.00;-0.00;0.00}/min";
            string engStr = $"ENERGY  : {healthCtrl.Energy.Current:F0}/{healthCtrl.Energy.Maximum:F0} {healthCtrl.EnergyRate:+0.00;-0.00;0.00}/min";
            string tempStr = $"TEMP    : {healthCtrl.Temperature.Current:F1} °C";

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), $"OVERALL : {totalHealth:F0}/{totalMaxHealth:F0} {healthCtrl.HealthRate:+0.00;-0.00;0.00}/min", mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), hydStr, mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), engStr, mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), tempStr, mainColor, textStyle); currentY += lh;

            currentY += 5f * finalScale;

            // --- 区块 2：肢体诊断 ---
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), "<b>[ LIMB DIAGNOSTICS ]</b>", mainColor, titleStyle); currentY += lh;

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
                        string effectName = (variation != null && variation.BuffType != GClass3056.EBuffType.Stimulant) ? variation.Buffs?.FirstOrDefault()?.Text ?? "" : "";

                        var notInBlackList = (effectName != "SevereMusclePain" && effectName != "MildMusclePain" && effectName != "Exhaustion");
                        if (!string.IsNullOrEmpty(effectName) && notInBlackList && part != EBodyPart.Head && part != EBodyPart.Chest)
                        {
                            statusText += $"[{effectName}] ";
                        }
                    }
                }

                if (string.IsNullOrEmpty(statusText)) statusText = hp.Current <= 0 ? "[损毁]" : "[OK]";

                string line = $"{partName.PadRight(10)} : {hp.Current:F0}/{hp.Maximum:F0}  {statusText}";
                HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), line, mainColor, textStyle); currentY += lh;
            }

            currentY += 5f * finalScale;

            // --- 区块 3：战斗与耐力 ---
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

            return finalX; // 返回占用后的最左侧坐标
        }
    }
}