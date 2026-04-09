using System;
using BepInEx.Configuration;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class EnvPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> Active;

        public static void Init(ConfigFile config)
        {
            OffsetX = config.Bind("Environment Panel", "X Offset", 0f, "环境面板独立 X 轴偏移");
            OffsetY = config.Bind("Environment Panel", "Y Offset", 0f, "环境面板独立 Y 轴偏移");
            Scale = config.Bind("Environment Panel", "Scale", 1.0f, "环境面板独立缩放比例");
            Active = config.Bind("Environment Panel", "Active", true, "显示环境面板");
        }

        public static void Draw(float startX, float startY, float globalScale)
        {
            if (!Active.Value) return;
            float finalScale = globalScale * Scale.Value;
            float finalX = startX + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = 300f * finalScale;

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

            HUDManager.DrawShadowLabel(new Rect(finalX, finalY, 400, 25), "<b>[ ENVIRONMENT SENSORS ACTIVE ]</b>", atmosColor, titleStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 1, rectWidth, lh), $"LOCATION  : {PluginsCore._cachedLocationName}", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 2, rectWidth, lh), $"GPS COORD : {gpsLat} | {gpsLon}", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 3, rectWidth, lh), $"LOCAL TIME: {tarkovTimeStr} | REAL: {realTimeStr}", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 4, rectWidth, lh), $"WIND DIR  : {windDir:000}° [{HUDManager.GetCompassDir(windDir)}] | {windSpeed:F1} M/S", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 5, rectWidth, lh), $"CROSSWIND : {Mathf.Abs(crossWind):F1} M/S [{crossDir}]", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 6, rectWidth, lh), $"VECT WIND : {Mathf.Abs(headWind):F1} M/S [{headDir}]", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 7, rectWidth, lh), $"ALT (MSL) : {altitude:F1} M", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 8, rectWidth, lh), $"PRESSURE  : {hPa:F1} HPA", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 9, rectWidth, lh), $"HUMIDITY  : {humidity:F1} %", atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 10, rectWidth, lh), $"TEMP      : {tempC:F1} °C | {tempF:F1} °F", atmosColor, textStyle);
        }
    }
}