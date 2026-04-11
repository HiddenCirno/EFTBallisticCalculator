using BepInEx.Configuration;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class HUDManager
    {
        // --- 左侧 HUD 全局设置 ---
        public static ConfigEntry<float> GlobalOffsetX;
        public static ConfigEntry<float> GlobalStartYOffset;
        public static ConfigEntry<float> GlobalScale;
        public static ConfigEntry<float> PanelSpacing;

        // --- 右侧 HUD 全局设置 ---
        public static ConfigEntry<float> RightGlobalOffsetX;
        public static ConfigEntry<float> RightGlobalStartYOffset;
        public static ConfigEntry<float> RightGlobalScale;
        public static ConfigEntry<float> RightPanelSpacing;

        // --- 其他全局特效 ---
        public static ConfigEntry<float> RainbowUISpeed;
        public static ConfigEntry<bool> RainbowUI;
        public static Color RainbowColor = new Color(1f, 1f, 1f, 0.85f);

        private static readonly Regex _colorRegex = new Regex(@"<color[^>]*>|</color>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void InitCfg(ConfigFile config)
        {
            // ==================== 左侧 HUD ====================
            GlobalOffsetX = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局X轴偏移", 30f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_x_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_x_name"), IsAdvanced = true }));

            GlobalStartYOffset = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局Y轴偏移", -180f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_y_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_y_name"), IsAdvanced = true }));

            GlobalScale = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_scale_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_scale_name"), IsAdvanced = true }));

            PanelSpacing = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "面板间距", 15f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_space_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_space_name"), IsAdvanced = true }));

            // ==================== 右侧 HUD ====================
            RightGlobalOffsetX = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "全局X轴偏移", 0f, // 默认给380，因为要容纳面板宽度
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_x_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_x_name"), IsAdvanced = true }));

            RightGlobalStartYOffset = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "全局Y轴偏移", -180f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_y_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_y_name"), IsAdvanced = true }));

            RightGlobalScale = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "全局缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_scale_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_scale_name"), IsAdvanced = true }));

            RightPanelSpacing = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "面板间距", 15f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_space_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_space_name"), IsAdvanced = true }));

            // ==================== 视觉特效 ====================
            RainbowUI = config.Bind("HUD Visuals / 视觉特效", "彩虹UI", false,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_rb_ui_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_rb_ui_name") }));

            RainbowUISpeed = config.Bind("HUD Visuals / 视觉特效", "彩虹UI滚动速度", 0.25f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_rb_spd_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_rb_spd_name"), IsAdvanced = true }));

            // 初始化子面板
            FCSPanel.InitCfg(config);
            EnvPanel.InitCfg(config);
            HealthPanel.InitCfg(config);
            ActiveBuffPanel.InitCfg(config);
            TeamPanel.InitCfg(config);
        }

        public static void UpdateRainbowColor()
        {
            if (RainbowUI.Value)
            {
                float hue = Mathf.Repeat(Time.time * RainbowUISpeed.Value, 1f); // 用上配置的速度
                Color hsvColor = UnityEngine.Color.HSVToRGB(hue, 0.8f, 1f);
                RainbowColor = new Color(hsvColor.r, hsvColor.g, hsvColor.b, 0.85f);
            }
        }

        public static void DrawGUI()
        {
            if (Camera.main == null || PluginsCore.CorrectPlayer == null) return;

            bool hasWeapon = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController != null;
            UpdateRainbowColor();

            // ==========================================
            // 1. 渲染左侧面板流 (FCS + Env)
            // ==========================================
            float leftStartX = GlobalOffsetX.Value;
            float leftCurrentY = (Screen.height / 2f) + GlobalStartYOffset.Value;
            float leftScale = GlobalScale.Value;

            leftCurrentY = FCSPanel.Draw(leftStartX, leftCurrentY, leftScale, hasWeapon);
            leftCurrentY += PanelSpacing.Value * leftScale;
            EnvPanel.Draw(leftStartX, leftCurrentY, leftScale);

            // ==========================================
            // 2. 渲染右侧面板流 (Health & 队友状态)
            // ==========================================
            // 注意：因为是从左往右画，所以起点要用 Screen.width 减去配置的值（相当于预留出面板的宽度）
            // 初始化起点 (屏幕右边缘减去边距)
            float rightAnchorX = Screen.width - RightGlobalOffsetX.Value;
            float rightCurrentY = (Screen.height / 2f) + RightGlobalStartYOffset.Value;
            float rightScale = RightGlobalScale.Value;

            // 1. 渲染健康面板 (最靠右)
            rightAnchorX = HealthPanel.Draw(rightAnchorX, rightCurrentY, rightScale);

            // 2. 渲染队伍面板 (贴在健康面板左侧)
            rightAnchorX = TeamPanel.Draw(rightAnchorX, rightCurrentY, rightScale);

            // 3. 渲染状态面板 (贴在队伍面板左侧，如果没有状态则不占空间)
            rightAnchorX = ActiveBuffPanel.Draw(rightAnchorX, rightCurrentY, rightScale);
            // 未来这里还可以继续： rightCurrentY += RightPanelSpacing.Value * rightScale; TeamPanel.Draw(...)

            // ==========================================
            // 3. 中心锁定标记
            // ==========================================
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

        public static void DrawShadowLabel(Rect rect, string text, Color textColor, GUIStyle style)
        {
            string shadowText = text;

            // 2. 性能极致优化的拦截器：
            // 只有当字符串真的包含 "<color" 或 "</color>" 时，才执行正则替换。
            // StringComparison.OrdinalIgnoreCase 是内存分配为 0 的超快匹配方式。
            if (!string.IsNullOrEmpty(text) &&
               (text.IndexOf("<color", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("</color", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                // 只剥离颜色标签，保留 <b> 和 <i> 标签，让阴影的粗细和倾斜度完美贴合主文本！
                shadowText = _colorRegex.Replace(text, string.Empty);
            }

            // 3. 绘制阴影（使用剥离了颜色标签的干净文本）
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Rect shadowRect = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height);
            GUI.Label(shadowRect, shadowText, style);

            // 4. 绘制主文本（保留原生塔科夫带颜色的富文本标签）
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