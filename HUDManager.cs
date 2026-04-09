using System;
using UnityEngine;

namespace EFTBallisticCalculator
{
    public static class HUDManager
    {
        public static void DrawGUI()
        {
            if (!PluginsCore._isHudActive) return;
            if (Camera.main == null || PluginsCore.CorrectPlayer == null) return;

            GUIStyle hudStyle = new GUIStyle(GUI.skin.label) { richText = true };
            bool hasWeapon = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController != null;

            float startX = PluginsCore._hudOffsetX;
            float currentY = (Screen.height / 2f) + PluginsCore._hudStartYOffset;

            currentY = DrawFCSPanel(startX, currentY, PluginsCore._hudScale, hasWeapon);

            currentY += PluginsCore._panelSpacing * PluginsCore._hudScale;
            DrawEnvPanel(startX, currentY, PluginsCore._hudScale);

            // 绘制屏幕中心准星
            if (hasWeapon && PluginsCore._lockedHorizontalDist > 0f)
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
        }

        private static float DrawFCSPanel(float startX, float startY, float scale, bool hasWeapon)
        {
            float lh = 20f * scale;
            int titleSize = (int)(15 * scale);
            int textSize = (int)(13 * scale);
            float rectWidth = 300f * scale;

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

            string compassHeading = GetCompassDir(PluginsCore.GetAzimuth().eulerAngles.y);
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

            DrawShadowLabel(new Rect(startX, startY, 400, 25), $"<b>{fcsStatusText}</b>", mainColor, titleStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 1, rectWidth, lh), $"HEADING   : {PluginsCore.GetAzimuth().eulerAngles.y:000}° [{compassHeading}]", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 2, rectWidth, lh), $"TGT RANGE : {rangeStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 3, rectWidth, lh), $"INCLINE   : {inclineStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 4, rectWidth, lh), $"CANT ANGL : {cantStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 5, rectWidth, lh), $"TIME FLGT : {tofStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 6, rectWidth, lh), $"MUZZLE VEL: {speedStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 7, rectWidth, lh), $"PROJ MASS : {massStr} {(hasWeapon ? $"(BC: {bcStr})" : "")}", mainColor, textStyle);

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

            DrawShadowLabel(new Rect(startX, startY + lh * 8f, rectWidth, lh), $"SYSTEM    : {aimStatus} | {hexCode}", mainColor, textStyle);
            return startY + (lh * 10f);
        }

        private static void DrawEnvPanel(float startX, float startY, float scale)
        {
            float lh = 20f * scale;
            int titleSize = (int)(15 * scale);
            int textSize = (int)(13 * scale);
            float rectWidth = 300f * scale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };
            Color atmosColor = new Color(0.3f, 0.8f, 0.9f, 0.85f);

            string realTimeStr = DateTime.Now.ToString("HH:mm:ss");
            string tarkovTimeStr = "UNKNOWN";
            if (PluginsCore.CorrectGameWorld != null && PluginsCore.CorrectGameWorld.GameDateTime != null)
            {
                tarkovTimeStr = PluginsCore.CorrectGameWorld.GameDateTime.Calculate().ToString("HH:mm:ss");
            }

            Vector3 playerTransform = PluginsCore.CorrectPlayer.Transform.position;
            float altitude = playerTransform.y;
            float GetSwing(float x, float y, float muti) { return (Mathf.PerlinNoise(x, y) * 2f - 1f) * muti; }

            float windSpeed = PluginsCore._weatherSeedMap.windSpeed.b + PluginsCore._weatherSeedMap.windSpeed.r * 5f + GetSwing(Time.time * 0.003f, PluginsCore._weatherSeedGlobal + 2f, 2f);
            float windDir = Mathf.Repeat(PluginsCore._weatherSeedMap.windDirection.b + GetSwing(Time.time * 0.00025f, PluginsCore._weatherSeedGlobal + 7f, 30f), 360f);
            float humidity = PluginsCore._weatherSeedMap.humidity.b + PluginsCore._weatherSeedMap.humidity.r * 35f + GetSwing(Time.time * 0.0015f, PluginsCore._weatherSeedGlobal + 41f, 10f);
            float tempC = PluginsCore._weatherSeedMap.temperature.b + PluginsCore._weatherSeedMap.temperature.r * 6f + GetSwing(Time.time * 0.0005f, PluginsCore._weatherSeedGlobal + 67f, 6f);
            float tempF = tempC * 1.8f + 32;
            float hPa = 1013.25f - (altitude * 0.012f) + (Mathf.PerlinNoise(Time.time * 0.0035f, PluginsCore._weatherSeedGlobal + 101f) * 2.25f - 1.05f);

            float relativeWindAngle = windDir - PluginsCore.GetAzimuth().eulerAngles.y;
            float crossWind = Mathf.Sin(relativeWindAngle * Mathf.Deg2Rad) * windSpeed;
            float headWind = Mathf.Cos(relativeWindAngle * Mathf.Deg2Rad) * windSpeed;
            string crossDir = crossWind > 0 ? "◄ L" : "R ►";
            string headDir = headWind > 0 ? "HEAD" : "TAIL";

            double baseLat = 60.051200;
            double baseLon = 29.351400;
            double currentLat = baseLat + (playerTransform.z * 0.000009);
            double currentLon = baseLon + (playerTransform.x * 0.000018);
            string gpsLat = $"{currentLat:F5}° N";
            string gpsLon = $"{currentLon:F5}° E";

            DrawShadowLabel(new Rect(startX, startY, 400, 25), "<b>[ ENVIRONMENT SENSORS ACTIVE ]</b>", atmosColor, titleStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 1, rectWidth, lh), $"LOCATION  : {PluginsCore._cachedLocationName}", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 2, rectWidth, lh), $"GPS COORD : {gpsLat} | {gpsLon}", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 3, rectWidth, lh), $"LOCAL TIME: {tarkovTimeStr} | REAL: {realTimeStr}", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 4, rectWidth, lh), $"WIND DIR  : {windDir:000}° [{GetCompassDir(windDir)}] | {windSpeed:F1} M/S", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 5, rectWidth, lh), $"CROSSWIND : {Mathf.Abs(crossWind):F1} M/S [{crossDir}]", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 6, rectWidth, lh), $"VECT WIND : {Mathf.Abs(headWind):F1} M/S [{headDir}]", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 7, rectWidth, lh), $"ALT (MSL) : {altitude:F1} M", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 8, rectWidth, lh), $"PRESSURE  : {hPa:F1} HPA", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 9, rectWidth, lh), $"HUMIDITY  : {humidity:F1} %", atmosColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 10, rectWidth, lh), $"TEMP      : {tempC:F1} °C | {tempF:F1} °F", atmosColor, textStyle);
        }

        private static void DrawShadowLabel(Rect rect, string text, Color textColor, GUIStyle style)
        {
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Rect shadowRect = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height);
            GUI.Label(shadowRect, text, style);

            GUI.color = textColor;
            GUI.Label(rect, text, style);
        }

        private static string GetCompassDir(float az)
        {
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };
            return dirs[(int)Mathf.Round(((az % 360) / 22.5f))];
        }
    }
}