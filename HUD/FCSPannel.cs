using BepInEx.Configuration;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class FCSPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> Active;

        public static void Init(ConfigFile config)
        {
            OffsetX = config.Bind("FCS Panel", "X Offset", 0f, "火控面板独立 X 轴偏移");
            OffsetY = config.Bind("FCS Panel", "Y Offset", 0f, "火控面板独立 Y 轴偏移");
            Scale = config.Bind("FCS Panel", "Scale", 1.0f, "火控面板独立缩放比例");
            Active = config.Bind("FCS Panel", "Active", true, "显示火控面板");
        }

        public static float Draw(float startX, float startY, float globalScale, bool hasWeapon)
        {
            if (!Active.Value) return startY;
            float finalScale = globalScale * Scale.Value;
            float finalX = startX + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = 300f * finalScale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };
            Color mainColor = new Color(0.2f, 1f, 0.4f, 0.9f);

            var fc = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController;
            Vector3 currentPos = hasWeapon ? fc.CurrentFireport.position : Camera.main.transform.position;

            bool hasLockedDistance = PluginsCore._lockedHorizontalDist > 0f;
            bool isFcsLocked = hasWeapon && PluginsCore._hasAmmo && hasLockedDistance;

            float dist3D = 0f;
            if (isFcsLocked)
            {
                dist3D = Vector3.Distance(currentPos, PluginsCore.ImpactMarker != null ? PluginsCore.ImpactMarker.transform.position : currentPos + fc.WeaponDirection * PluginsCore._lockedHorizontalDist);
            }

            string compassHeading = HUDManager.GetCompassDir(PluginsCore.GetAzimuth().eulerAngles.y);
            float rollAngle = PluginsCore.GetAzimuth().eulerAngles.z;
            if (rollAngle > 180f) rollAngle -= 360f;
            float vertAngle = PluginsCore.GetAzimuth().eulerAngles.x;
            if (vertAngle > 180f) vertAngle -= 360f;

            string nodata = LocaleManager.Get("no_data");

            // --- 1. 预处理变量值 (Value) ---
            string headingVal = string.Format(LocaleManager.Get("fcs_val_heading"), PluginsCore.GetAzimuth().eulerAngles.y, compassHeading);

            string rangeStr = nodata;
            if (hasWeapon)
            {
                rangeStr = hasLockedDistance ? string.Format(LocaleManager.Get("fcs_val_range"), PluginsCore._lockedHorizontalDist) : LocaleManager.Get("fcs_no_lock");
            }

            string tofStr = isFcsLocked ? string.Format(LocaleManager.Get("fcs_val_tof"), PluginsCore._lockedTOF) : nodata;
            string inclineStr = hasWeapon ? string.Format(LocaleManager.Get("fcs_val_angle"), vertAngle) : nodata;
            string cantStr = hasWeapon ? string.Format(LocaleManager.Get("fcs_val_angle"), rollAngle) : nodata;
            string speedStr = (hasWeapon && PluginsCore._hasAmmo) ? string.Format(LocaleManager.Get("fcs_val_speed"), PluginsCore._currentSpeed) : nodata;

            string massVal = (hasWeapon && PluginsCore._hasAmmo) ? string.Format(LocaleManager.Get("fcs_val_mass"), PluginsCore._currentMass) : nodata;
            string bcVal = (hasWeapon && PluginsCore._hasAmmo) ? string.Format(LocaleManager.Get("fcs_val_bc"), PluginsCore._currentBC) : "";
            string massStr = string.IsNullOrEmpty(bcVal) ? massVal : $"{massVal} {bcVal}";

            // 顶部状态栏
            string fcsStatusText;
            if (!hasWeapon) fcsStatusText = LocaleManager.Get("fcs_title_no_weapon");
            else if (!PluginsCore._hasAmmo) fcsStatusText = LocaleManager.Get("fcs_title_no_ammo");
            else if (hasLockedDistance) fcsStatusText = LocaleManager.Get("fcs_title_locked");
            else fcsStatusText = LocaleManager.Get("fcs_title_standby");

            // --- 2. 注入标签模板并绘制 (Label) ---
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY, 400, 25), $"<b>{fcsStatusText}</b>", mainColor, titleStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 1, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_heading"), headingVal), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 2, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_range"), rangeStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 3, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_incline"), inclineStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 4, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_cant"), cantStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 5, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_tof"), tofStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 6, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_vel"), speedStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 7, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_mass"), massStr), mainColor, textStyle);

            // 系统状态
            string aimStatus = LocaleManager.Get("fcs_status_offline");
            string hexCode = "0x0000";
            if (hasWeapon)
            {
                hexCode = "0x" + UnityEngine.Random.Range(0x1000, 0xFFFF).ToString("X4");
                var pwa = PluginsCore.CorrectPlayer.ProceduralWeaponAnimation;
                bool isAiming = (pwa != null && pwa.IsAiming);

                if (!PluginsCore._hasAmmo) aimStatus = LocaleManager.Get("fcs_status_no_ammo");
                else if (!isAiming) aimStatus = LocaleManager.Get("fcs_status_standby");
                else aimStatus = hasLockedDistance ? LocaleManager.Get("fcs_status_tracked") : LocaleManager.Get("fcs_status_optic_sync");
            }

            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 8f, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_sys"), aimStatus, hexCode), mainColor, textStyle);

            // 返回不包含子面板Y偏移的真实底部边界，方便排版叠加
            return startY + (lh * 10f);
        }
    }
}