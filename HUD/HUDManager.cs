using BepInEx.Configuration;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class HUDManager
    {
        public static ConfigEntry<float> GlobalOffsetX;
        public static ConfigEntry<float> GlobalStartYOffset;
        public static ConfigEntry<float> GlobalScale;
        public static ConfigEntry<float> PanelSpacing;
        public static ConfigEntry<float> RainbowUISpeed;
        public static ConfigEntry<bool> RainbowUI;
        public static Color RainbowColor = new Color(1f, 1f, 1f, 0.85f);

        public static void InitCfg(ConfigFile config)
        {
            GlobalOffsetX = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局X轴偏移", 30f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_x_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_x_name") }));

            GlobalStartYOffset = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局Y轴偏移", -180f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_y_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_y_name") }));

            GlobalScale = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_scale_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_scale_name") }));

            PanelSpacing = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "面板间距", 15f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_space_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_space_name") }));

            RainbowUI = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "彩虹UI", false,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_rb_ui_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_rb_ui_name") }));

            RainbowUISpeed = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "彩虹UI滚动速度", 0.25f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_rb_spd_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_rb_spd_name") }));

            // 初始化子面板
            FCSPanel.InitCfg(config);
            EnvPanel.InitCfg(config);
        }
        public static void UpdateRainbowColor()
        {
            if (RainbowUI.Value)
            {
                // Time.time * 0.25f 控制颜色变化的速度，Mathf.Repeat 保证色相在 0-1 之间循环
                float hue = Mathf.Repeat(Time.time * 0.25f, 1f);
                // 转换 HSV 到 RGB (饱和度 0.8，明度 1.0，保证颜色鲜艳不刺眼)
                Color hsvColor = UnityEngine.Color.HSVToRGB(hue, 0.8f, 1f);
                // 重新拼装 Color，保留 0.85 的透明度
                RainbowColor = new Color(hsvColor.r, hsvColor.g, hsvColor.b, 0.85f);
            }
        }
        public static void DrawGUI()
        {
            if (Camera.main == null || PluginsCore.CorrectPlayer == null) return;

            bool hasWeapon = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController != null;

            float startX = GlobalOffsetX.Value;
            float currentY = (Screen.height / 2f) + GlobalStartYOffset.Value;
            float scale = GlobalScale.Value;
            UpdateRainbowColor();
            // 绘制顺序流
            currentY = FCSPanel.Draw(startX, currentY, scale, hasWeapon);
            currentY += PanelSpacing.Value * scale;
            EnvPanel.Draw(startX, currentY, scale);

            // 中心锁定标记
            if (hasWeapon && PluginsCore._lockedHorizontalDist > 0f)
            {
                DrawCenterMarker();
            }
        }

        private static void DrawCenterMarker()
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;
            float size = 50f;
            float thick = 2f;
            float length = 15f;

            float alphaPulse = 0.5f + Mathf.PingPong(Time.time * 2f, 0.5f);
            GUI.color = new Color(0.2f, 1f, 0.4f, alphaPulse);

            GUI.DrawTexture(new Rect(cx - size, cy - size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - size, cy - size, thick, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size - length, cy - size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size, cy - size, thick, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - size, cy + size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - size, cy + size - length, thick, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size - length, cy + size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size, cy + size - length, thick, length), Texture2D.whiteTexture);
        }

        // 公共绘图工具
        public static void DrawShadowLabel(Rect rect, string text, Color textColor, GUIStyle style)
        {
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Rect shadowRect = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height);
            GUI.Label(shadowRect, text, style);

            GUI.color = textColor;
            GUI.Label(rect, text, style);
        }

        public static string GetCompassDir(float az)
        {
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };
            string[] dirsch = { "北", "北偏东", "东北", "东偏北", "东", "东偏南", "东南", "南偏东", "南", "南偏西", "西南", "西偏南", "西", "西偏北", "西北", "北偏西", "北" };
            if (LocaleManager.CurrentLanguage.Value == AppLanguage.简体中文)
            {
                return dirsch[(int)Mathf.Round(((az % 360) / 22.5f))];
            }
            return dirs[(int)Mathf.Round(((az % 360) / 22.5f))];
        }
    }
}