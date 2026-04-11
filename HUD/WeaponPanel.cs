using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using System.Linq;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class WeaponPanel
    {
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        public static void InitCfg(ConfigFile config)
        {
            OffsetY = config.Bind("Top Panel / 武器火控", "武器板 Y轴偏移", 15f, new ConfigDescription("面板的纵向微调"));
            Scale = config.Bind("Top Panel / 武器火控", "武器板 缩放比例", 1.0f, new ConfigDescription("武器面板整体缩放"));
            Active = config.Bind("Top Panel / 武器火控", "显示武器面板", true, new ConfigDescription("是否启用武器火控显示"));
            Color = config.Bind("Top Panel / 武器火控", "颜色设置", new Color(0.6f, 0.9f, 1f, 0.9f), new ConfigDescription("默认战术UI颜色"));
        }

        // 接收全局的 Top Y 坐标，返回占用后的新 Y 坐标
        public static float Draw(float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return startY;

            var player = PluginsCore.CorrectPlayer;
            if (player.HandsController == null || !(player.HandsController.Item is Weapon weapon))
            {
                return startY; // 没拿武器直接跳过，不占空间
            }

            float finalScale = globalScale * Scale.Value;

            // 增大字体，拉开层级
            int nameSize = (int)(22 * finalScale);  // 巨大的武器名
            int statSize = (int)(15 * finalScale);  // 枪械属性
            int ammoSize = (int)(16 * finalScale);  // 弹药火控

            float currentY = startY + OffsetY.Value * finalScale;

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = nameSize, alignment = TextAnchor.MiddleCenter };
            GUIStyle statStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = statSize, alignment = TextAnchor.MiddleCenter };
            GUIStyle ammoStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = ammoSize, alignment = TextAnchor.MiddleCenter };
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : Color.Value;

            // ==========================================
            // 核心数据提取
            // ==========================================
            string weaponName = weapon.Name.Localized(); // 使用全名更霸气
            string fireMode = weapon.SelectedFireMode.ToString().ToUpper();

            float dur = weapon.Repairable?.Durability ?? 100f;
            float maxDur = weapon.Repairable?.MaxDurability ?? 100f;
            string durColor = (dur / maxDur) < 0.5f ? "#ff4444" : "#ffffff";

            float moa = weapon.TotalAccuracy;
            float ergo = weapon.ErgonomicsTotal;
            int recoilUp = (int)weapon.RecoilDelta;
            int recoilBack = (int)weapon.RecoilForceBack;

            // 弹药逻辑
            string chamberAmmoName = "EMPTY";
            string nextAmmoName = "EMPTY";
            int currentMagCount = 0;
            int maxMagCount = 0;

            var chamber = weapon.Chambers?.FirstOrDefault();
            if (chamber?.ContainedItem is AmmoItemClass chamberBullet)
            {
                chamberAmmoName = chamberBullet.ShortName.Localized();
            }

            var mag = weapon.GetCurrentMagazine();
            if (mag != null)
            {
                currentMagCount = mag.Count;
                maxMagCount = mag.MaxCount;

                if (mag.Cartridges?.Items?.LastOrDefault() is AmmoItemClass topBullet)
                {
                    nextAmmoName = topBullet.ShortName.Localized();
                }
            }

            // ==========================================
            // UI 渲染 (三行布局)
            // ==========================================
            float screenW = Screen.width;

            // 第一行：武器全名
            float nameHeight = 30f * finalScale;
            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, nameHeight), $"<b>[ {weaponName} ]</b>", mainColor, nameStyle);
            currentY += nameHeight;

            // 第二行：机械属性 (耐久 / 人机 / 后坐力 / 精度)
            string statLine = $"DUR: <color={durColor}>{dur:F1}</color>  |  ERGO: {ergo:F1}  |  REC: {recoilUp}/{recoilBack}  |  MOA: {moa:F2}";
            float statHeight = 24f * finalScale;
            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, statHeight), statLine, new Color(0.8f, 0.8f, 0.8f, 0.9f), statStyle);
            currentY += statHeight;

            // 第三行：火控与弹药
            string chamberColor = chamberAmmoName == "EMPTY" ? "#ff4444" : "#55ff55";
            string magColor = currentMagCount == 0 ? "#ff4444" : (currentMagCount <= maxMagCount * 0.3f ? "#ffaa00" : "#ffffff");
            string ammoCountStr = mag != null ? $"MAG: <color={magColor}>{currentMagCount}/{maxMagCount}</color>" : "MAG: --/--";

            string ammoLine = $"MODE: <color=#ffaa00>{fireMode}</color>   ||   {ammoCountStr}   |   CHBR: <color={chamberColor}>{chamberAmmoName}</color>  >>  NXT: {nextAmmoName}";
            float ammoHeight = 26f * finalScale;
            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, ammoHeight), ammoLine, mainColor, ammoStyle);
            currentY += ammoHeight + (10f * finalScale); // 留出底部边距

            return currentY;
        }
    }
}