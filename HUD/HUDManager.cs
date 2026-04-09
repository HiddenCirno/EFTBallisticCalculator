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

        public static void Init(ConfigFile config)
        {
            GlobalOffsetX = config.Bind("Left HUD Pannel Global", "Global X Offset", 30f, "HUD 整体距离屏幕左侧绝对距离");
            GlobalStartYOffset = config.Bind("Left HUD Pannel Global", "Global Y Offset", -180f, "HUD 整体相对屏幕中心的 Y 轴偏移");
            GlobalScale = config.Bind("Left HUD Pannel Global", "Global Scale", 1.0f, "全局 UI 缩放比例");
            PanelSpacing = config.Bind("Left HUD Pannel Global", "Panel Spacing", 15f, "面板之间的垂直间距");

            // 初始化子面板
            FCSPanel.Init(config);
            EnvPanel.Init(config);
        }

        public static void DrawGUI()
        {
            if (Camera.main == null || PluginsCore.CorrectPlayer == null) return;

            bool hasWeapon = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController != null;

            float startX = GlobalOffsetX.Value;
            float currentY = (Screen.height / 2f) + GlobalStartYOffset.Value;
            float scale = GlobalScale.Value;

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
            return dirs[(int)Mathf.Round(((az % 360) / 22.5f))];
        }
    }
}