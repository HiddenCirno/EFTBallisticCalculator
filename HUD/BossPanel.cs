using BepInEx.Configuration;
using EFT;
using EFTBallisticCalculator.Locale; // 如果没用到可以删掉，这里保留兼容
using System.Collections.Generic;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public enum EBossStatus
    {
        Alive,
        Dead
    }

    public static class BossPanel
    {
        public static ConfigEntry<bool> Active;

        public class BossRecord
        {
            public string AccountId;
            public string Name;
            public string RolePrefix; // 存放带有颜色的身份标签，例如 "<color=#CE0000>Boss</color>"
            public Player PlayerRef;
            public EBossStatus Status = EBossStatus.Alive;
            public int LastDistance = 0; // 缓存最后距离，死了也能知道在哪
        }

        private static EFT.GameWorld _lastGameWorld = null;
        public static readonly Dictionary<string, BossRecord> _roster = new Dictionary<string, BossRecord>();
        private static float _lastScanTime = 0f;

        public static void InitCfg(ConfigFile config)
        {
            // 独立开关
            Active = config.Bind("Boss Panel / 敌对首领", "显示面板", true,
                new ConfigDescription("是否在HUD中显示Boss追踪面板"));
        }

        private static void UpdateRoster()
        {
            // 限制扫描频率，每1秒扫描一次足够了，省性能
            if (Time.time - _lastScanTime < 1f) return;
            _lastScanTime = Time.time;

            var gw = PluginsCore.CorrectGameWorld;
            var myPlayer = PluginsCore.CorrectPlayer;

            if (gw != null && gw != _lastGameWorld)
            {
                _roster.Clear();
                _lastGameWorld = gw;
            }

            if (gw == null || myPlayer == null) return;

            HashSet<string> aliveProfileIds = new HashSet<string>();

            // 1. 扫描当前存活列表，抓取Boss
            foreach (var player in gw.AllAlivePlayersList)
            {
                if (player == null || player == myPlayer) continue;

                var profile = player.Profile;
                if (profile == null || profile.Info == null) continue;

                var role = profile.Info.Settings?.Role.ToString().ToLower() ?? "assault";

                // 判定是否是我们关心的特殊单位 (套用你的ESP逻辑)
                if (TryGetBossPrefix(role, out string rolePrefix))
                {
                    string profileId = profile.Id;
                    aliveProfileIds.Add(profileId);

                    if (!_roster.TryGetValue(profileId, out var record))
                    {
                        string safeName = GetLatinName(profile.Info.Nickname);
                        record = new BossRecord
                        {
                            AccountId = profileId,
                            Name = safeName,
                            RolePrefix = rolePrefix,
                            Status = EBossStatus.Alive
                        };
                        _roster[profileId] = record;
                    }

                    record.PlayerRef = player;

                    // 更新距离 (只在活着的时候更新，死了就定格在最后位置)
                    if (record.Status == EBossStatus.Alive && player.Transform != null && myPlayer.Transform != null)
                    {
                        record.LastDistance = Mathf.RoundToInt(Vector3.Distance(myPlayer.Transform.position, player.Transform.position));
                    }

                    // 原版死亡判定：血条归零
                    if (player.HealthController != null && !player.HealthController.IsAlive)
                    {
                        record.Status = EBossStatus.Dead;
                    }
                }
            }

            // 2. 兜底消失判定 (被系统刷掉的尸体)
            foreach (var kvp in _roster)
            {
                var record = kvp.Value;
                if (record.Status == EBossStatus.Alive && !aliveProfileIds.Contains(record.AccountId))
                {
                    // 活着但从AliveList里消失了，判定为死亡
                    record.Status = EBossStatus.Dead;
                }
            }
        }

        public static float Draw(float x, float y, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return y;

            UpdateRoster();

            // 如果地图上没有任何Boss，直接不画这个面板，把坑位让出来
            if (_roster.Count == 0) return y;

            float finalScale = globalScale * TeamPanel.Scale.Value; // 复用TeamPanel的缩放
            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = TeamPanel.RectWidth.Value * finalScale; // 复用TeamPanel的宽度

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : TeamPanel.Color.Value;

            float currentY = y;

            // 绘制标题
            // 你可以在 Locale 里加上 "boss_title" : "<b>[ BOSS RADAR ]</b>"
            string titleText = LocaleManager.Get("boss_title") == "boss_title" ? "<b>[ HOSTILE TRACKER ]</b>" : LocaleManager.Get("boss_title");
            HUDManager.DrawShadowLabel(new Rect(x, currentY, rectWidth, 25), titleText, mainColor, titleStyle);
            currentY += lh;

            // 绘制Boss花名册
            foreach (var record in _roster.Values)
            {
                DrawBossLine(record, x, ref currentY, rectWidth, lh, textStyle, mainColor);
            }

            return currentY;
        }

        private static void DrawBossLine(BossRecord record, float x, ref float y, float width, float lh, GUIStyle style, Color defaultColor)
        {
            if (record.Status == EBossStatus.Dead)
            {
                // 死亡状态显示：灰色名字，标红 [DEAD]，保留最后距离
                string deadLine = $"<color=#808080>{record.RolePrefix} {record.Name}</color> - <color=#FFFF00>{record.LastDistance}m</color> <color=#BA0303>[DEAD]</color>";
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), deadLine, defaultColor, style);
                y += lh;
                return;
            }

            var player = record.PlayerRef;
            if (player == null || player.HealthController == null) return;

            var healthCtrl = player.HealthController;
            float totalHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Current;
            float maxHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Maximum;

            // 复用你写好的血量颜色计算
            string hpColor = LocaleManager.Get($"health_bio_hp_color_{HealthPanel.HealthStatusLow(totalHp, maxHp)}");
            if (hpColor.StartsWith("health_bio_hp_color_")) hpColor = "#55ff55"; // 兜底颜色

            // 存活状态显示：身份前缀 + 名字 + 距离 + 血量
            string mainLine = $"{record.RolePrefix} {record.Name} - <color=#FFFF00>{record.LastDistance}m</color> <color={hpColor}>({totalHp:F0}/{maxHp:F0})</color>";

            HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), mainLine, defaultColor, style);
            y += lh;
            // Boss不需要额外换行渲染吃喝，所以非常紧凑
        }

        // =====================================
        // 工具方法区域
        // =====================================

        private static bool TryGetBossPrefix(string role, out string prefix)
        {
            prefix = "";

            // 核心Boss
            if (role.Contains("boss"))
            {
                prefix = "<color=#CE0000>[BOSS]</color>";
                return true;
            }
            // 护卫/小弟
            if (role.Contains("follower") || role == "tagillahelperagro")
            {
                prefix = "<color=#FF2DE9>[GUARD]</color>";
                return true;
            }
            // 邪教徒
            if (role.Contains("sectant") || role == "sectantoni" || role == "sectantpredvestnik" || role == "sectantprizark")
            {
                prefix = "<color=#ADFF2F>[CULTIST]</color>";
                return true;
            }
            // 特殊单位补充 (可根据需要开启)
            /*
            if (role == "bossboarsniper" || role == "marksman") { prefix = "<color=#00FA9A>[SNIPER]</color>"; return true; }
            if (role == "pmcbot" || role == "exusec") { prefix = "<color=#7300A6>[ROGUE]</color>"; return true; }
            if (role.Contains("black")) { prefix = "<color=#DC143C>[BLACKFOX]</color>"; return true; }
            */

            return false;
        }

        private static string GetLatinName(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return "UNKNOWN";
            if (IsAllEnglish(nickname)) return nickname;
            try { return GStruct21.ConvertToLatinic(nickname); }
            catch { return nickname; }
        }

        private static bool IsAllEnglish(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') && (c < '0' || c > '9') && c != ' ' && c != '-' && c != '_')
                    return false;
            }
            return true;
        }
    }
}