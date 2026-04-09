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

            string rangeStr = "---";
            if (hasWeapon) rangeStr = hasLockedDistance ? $"{PluginsCore._lockedHorizontalDist:F1} M  (3D: {dist3D:F1} M)" : "NO LOCK";

            string tofStr = isFcsLocked ? $"{PluginsCore._lockedTOF:F3} SEC" : "---";
            string inclineStr = hasWeapon ? $"{vertAngle:-0.0;+0.0;0.0}°" : "---";
            string cantStr = hasWeapon ? $"{rollAngle:+0.0;-0.0;0.0}°" : "---";
            string speedStr = (hasWeapon && PluginsCore._hasAmmo) ? $"{PluginsCore._currentSpeed:F1} M/S" : "---";
            string massStr = (hasWeapon && PluginsCore._hasAmmo) ? $"{PluginsCore._currentMass:F1} G" : "---";
            string bcStr = (hasWeapon && PluginsCore._hasAmmo) ? $"{PluginsCore._currentBC:F3}" : "---";

            string fcsStatusText;
            if (!hasWeapon) fcsStatusText = "[ DIRECTOR FCS: NO WEAPON ]";
            else if (!PluginsCore._hasAmmo) fcsStatusText = "[ DIRECTOR FCS: NO AMMO ]";
            else if (hasLockedDistance) fcsStatusText = "[ DIRECTOR FCS: TARGET LOCKED ]";
            else fcsStatusText = "[ DIRECTOR FCS: STANDBY ]";

            HUDManager.DrawShadowLabel(new Rect(finalX, finalY, 400, 25), $"<b>{fcsStatusText}</b>", mainColor, titleStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 1, rectWidth, lh), $"HEADING   : {PluginsCore.GetAzimuth().eulerAngles.y:000}° [{compassHeading}]", mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 2, rectWidth, lh), $"TGT RANGE : {rangeStr}", mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 3, rectWidth, lh), $"INCLINE   : {inclineStr}", mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 4, rectWidth, lh), $"CANT ANGL : {cantStr}", mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 5, rectWidth, lh), $"TIME FLGT : {tofStr}", mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 6, rectWidth, lh), $"MUZZLE VEL: {speedStr}", mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 7, rectWidth, lh), $"PROJ MASS : {massStr} {(hasWeapon ? $"(BC: {bcStr})" : "")}", mainColor, textStyle);

            string aimStatus = "OFFLINE";
            string hexCode = "0x0000";

            if (hasWeapon)
            {
                hexCode = "0x" + UnityEngine.Random.Range(0x1000, 0xFFFF).ToString("X4");
                var pwa = PluginsCore.CorrectPlayer.ProceduralWeaponAnimation;
                bool isAiming = (pwa != null && pwa.IsAiming);

                if (!PluginsCore._hasAmmo) aimStatus = "NO_AMMO";
                else if (!isAiming) aimStatus = "STANDBY";
                else
                {
                    aimStatus = hasLockedDistance ? "TRACKED" : "OPTIC_SYNC";
                }
            }

            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 8f, rectWidth, lh), $"SYSTEM    : {aimStatus} | {hexCode}", mainColor, textStyle);

            // 返回不包含子面板Y偏移的真实底部边界，方便排版叠加
            return startY + (lh * 10f);
        }
    }
}