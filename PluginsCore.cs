using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;

namespace EFTBallisticCalculator
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class PluginsCore : BaseUnityPlugin
    {
        // --- BepInEx 快捷键配置 ---
        public static ConfigEntry<KeyboardShortcut> KeyFcsToggle; // 面板总开关
        public static ConfigEntry<KeyboardShortcut> KeyFcsClear;  // 脱锁 (清空距离)

        public static ConfigEntry<KeyboardShortcut> KeyDistUp100; // 距离 +100
        public static ConfigEntry<KeyboardShortcut> KeyDistDown100;// 距离 -100
        public static ConfigEntry<KeyboardShortcut> KeyDistUp10;  // 距离 +10
        public static ConfigEntry<KeyboardShortcut> KeyDistDown10;// 距离 -10
        public static ConfigEntry<KeyboardShortcut> KeyDistUp1;   // 距离 +1
        public static ConfigEntry<KeyboardShortcut> KeyDistDown1; // 距离 -1
        // --- 火控状态缓存 ---
        public static bool _isFcsActive = false;
        public static bool _isHudActive = true;     // 控制左侧火控面板是否显示（默认为 true，或者你可以加个按键开关它）
        public static bool _hasLockedDistance = false;   // 控制 3D 落点标记是否显示，以及面板是否显示锁定数据
        public static bool _hasAmmo = false;           // 当前枪膛是否有弹药

        // --- 当前武器实时数据 (用于未锁定时的兜底显示) ---
        public static float _currentSpeed = 0f;
        public static float _currentMass = 0f;
        public static float _currentBC = 0f;
        public static float _currentDiam = 0f; // 新增：实时直径

        public static float _lockedHorizontalDist;
        public static float _lockedVerticalDist;
        public static float _lockedTOF; // 缓存的飞行时间

        // --- HUD 全局排版配置 ---
        public static float _hudOffsetX = 30f;       // 整个 HUD 距离屏幕左侧的绝对距离
        public static float _hudStartYOffset = -180f;// 整个 HUD 顶部距离屏幕中心的 Y 轴偏移
        public static float _hudScale = 1.0f;        // 全局统一缩放比例 (保证字号、行宽绝对统一)

        // --- 面板内部控制 ---
        public static float _panelSpacing = 15f;

        // --- 火控面板配置 ---
        public static float _fcsOffsetX = 30f;
        public static float _fcsOffsetY = -180f; // 相对于屏幕中心的 Y 偏移
        public static float _fcsScale = 1.0f;

        // --- 环境面板配置 ---
        public static float _envOffsetX = 30f;
        public static float _envOffsetY = 20f;   // 相对于屏幕中心的 Y 偏移
        public static float _envScale = 1.0f;

        // 子弹物理属性缓存
        public static float _lockedSpeed;
        public static float _lockedMass;
        public static float _lockedDiam;
        public static float _lockedBC;
        public static float _weatherSeedGlobal;

        public static System.Random _weatherRng;

        public class WeatherSeed
        {
            public float b;
            public float x;
            public float y;
            public float r;
        }
        public class WeatherSeedMap
        {
            public WeatherSeed windSpeed = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
            public WeatherSeed windDirection = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
            public WeatherSeed humidity = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
            public WeatherSeed temperature = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
        }

        public static WeatherSeedMap _weatherSeedMap = new WeatherSeedMap();

        public static int _layerMask = -1;
        public static Player CorrectPlayer { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }

        public static string _cachedLocationName = null;

        private EFT.Player.FirearmController _currentFC;
        private static Camera _cachedOpticCamera;

        // --- 3D 实体标记 ---
        private GameObject _impactMarker;

        public void Awake()
        {
            KeyFcsToggle = Config.Bind("1. Controls", "Toggle HUD", new KeyboardShortcut(KeyCode.KeypadDivide), "开启/关闭火控面板");
            KeyFcsClear = Config.Bind("1. Controls", "Clear Target (Unlock)", new KeyboardShortcut(KeyCode.Backspace), "脱锁并清除距离数据");

            // 绑定组合键参数：主键，修饰键 (例如：上箭头 + 左Shift)
            KeyDistUp100 = Config.Bind("2. Manual Dial", "Distance +100m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftShift), "手动增加距离 100m");
            KeyDistDown100 = Config.Bind("2. Manual Dial", "Distance -100m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftShift), "手动减少距离 100m");

            KeyDistUp10 = Config.Bind("2. Manual Dial", "Distance +10m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftAlt), "手动增加距离 10m");
            KeyDistDown10 = Config.Bind("2. Manual Dial", "Distance -10m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftAlt), "手动减少距离 10m");

            KeyDistUp1 = Config.Bind("2. Manual Dial", "Distance +1m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl), "手动增加距离 1m");
            KeyDistDown1 = Config.Bind("2. Manual Dial", "Distance -1m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl), "手动减少距离 1m");

            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
        }

        public void Update()
        {
            if (CorrectPlayer == null || Camera.main == null) return;
            if (_layerMask == -1) _layerMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "Default");

            _currentFC = CorrectPlayer.HandsController as EFT.Player.FirearmController;

            // 1. 判断弹药状态，更新实时火控参数
            if (_currentFC != null && _currentFC.Item != null && _currentFC.Item.Chambers.Length > 0 && _currentFC.Item.Chambers[0].ContainedItem is AmmoItemClass currentAmmo)
            {
                _hasAmmo = true;
                _currentSpeed = currentAmmo.AmmoTemplate.InitialSpeed * _currentFC.Item.SpeedFactor;
                _currentMass = currentAmmo.AmmoTemplate.BulletMassGram;
                _currentBC = currentAmmo.AmmoTemplate.BallisticCoeficient;
                _currentDiam = currentAmmo.AmmoTemplate.BulletDiameterMilimeters;
            }
            else
            {
                _hasAmmo = false;
            }

            // 2. 终极状态判定：只有在 (有枪 + 有弹 + 有距离) 时，才是真正的完全锁定！
            bool hasLockedDistance = _lockedHorizontalDist > 0f;
            bool isFcsLocked = (_currentFC != null) && _hasAmmo && hasLockedDistance;

            UpdateOpticCache();

            // ==========================================
            // --- 快捷键与火控数据输入模块 ---
            // ==========================================

            // 1. 面板显隐
            if (KeyFcsToggle.Value.IsDown()) _isHudActive = !_isHudActive;

            // 2. 激光测距 (直接覆盖数据)
            if (Input.GetKeyDown(KeyCode.T))
            {
                ExecuteFcsLogic();
            }

            if (_currentFC != null)
            {
                // 3. 手动脱锁 (数据归零)
                if (KeyFcsClear.Value.IsDown())
                {
                    _lockedHorizontalDist = 0f;
                    _lockedVerticalDist = 0f; // 脱锁时高度差也一并归零
                }

                // 4. 手动表尺微调 (组合键增减距离)
                float deltaDist = 0f;
                if (KeyDistUp100.Value.IsDown()) deltaDist += 100f;
                if (KeyDistDown100.Value.IsDown()) deltaDist -= 100f;
                if (KeyDistUp10.Value.IsDown()) deltaDist += 10f;
                if (KeyDistDown10.Value.IsDown()) deltaDist -= 10f;
                if (KeyDistUp1.Value.IsDown()) deltaDist += 1f;
                if (KeyDistDown1.Value.IsDown()) deltaDist -= 1f;

                if (deltaDist != 0f)
                {
                    _lockedHorizontalDist += deltaDist;
                    _lockedHorizontalDist = (int)_lockedHorizontalDist;

                    // 数据底线防呆：如果手动减到 0 或负数，等同于脱锁
                    if (_lockedHorizontalDist <= 0f)
                    {
                        _lockedHorizontalDist = 0f;
                        _lockedVerticalDist = 0f;
                    }
                }
            }

            // 4. 只有完全锁定状态，才进行 3D 落点仿真！
            if (isFcsLocked)
            {
                bool isAiming = false;
                var pwa = CorrectPlayer.ProceduralWeaponAnimation;
                if (pwa != null) isAiming = pwa.IsAiming;

                if (isAiming)
                {
                    if (_impactMarker == null) InitializeMarker();

                    Vector3 currentFireportPos = _currentFC.CurrentFireport.position;
                    Vector3 currentBoreDir = _currentFC.WeaponDirection.normalized;
                    Vector3 currentVelocity = currentBoreDir * _currentSpeed;

                    Vector3 impactPoint3D = OracleBallistics.SimulateToHorizontalDistance(
                        currentFireportPos,
                        currentVelocity,
                        _currentMass,
                        _currentDiam,
                        _currentBC,
                        _lockedHorizontalDist,
                        out _lockedTOF
                    );

                    _impactMarker.transform.position = impactPoint3D;

                    Camera activeCam = (_cachedOpticCamera != null && _cachedOpticCamera.isActiveAndEnabled) ? _cachedOpticCamera : Camera.main;
                    float distToCamera = Vector3.Distance(activeCam.transform.position, impactPoint3D);
                    float dynamicScale = distToCamera * Mathf.Tan(activeCam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.015f;
                    _impactMarker.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);

                    if (!_impactMarker.activeSelf) _impactMarker.SetActive(true);
                }
                else
                {
                    if (_impactMarker != null && _impactMarker.activeSelf) _impactMarker.SetActive(false);
                }
            }
            else
            {
                // 脱锁状态下关闭 3D 准星
                if (_impactMarker != null && _impactMarker.activeSelf) _impactMarker.SetActive(false);
            }
        }

        private void ExecuteFcsLogic()
        {
            if (_currentFC == null) return; // 没拿枪不能测距

            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _layerMask))
            {
                Vector3 fireportPos = _currentFC.CurrentFireport.position;
                _lockedHorizontalDist = new Vector2(hit.point.x - fireportPos.x, hit.point.z - fireportPos.z).magnitude;
                _lockedVerticalDist = hit.point.y - fireportPos.y;
            }
            else
            {
                _lockedHorizontalDist = 0f;
            }
        }
        string GetCompassDir(float az)
        {
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };
            return dirs[(int)Mathf.Round(((az % 360) / 22.5f))];
        }
        Transform GetAzimuth()
        {
            return (_cachedOpticCamera != null && _cachedOpticCamera.isActiveAndEnabled)
                                     ? _cachedOpticCamera.transform
                                     : Camera.main.transform;
        }
        public void OnGUI()
        {
            // 【改动1】：现在只判断面板总开关，不再判断 _currentFC 是否为空
            if (!_isHudActive) return;
            if (Camera.main == null || CorrectPlayer == null) return;
            GUIStyle hudStyle = new GUIStyle(GUI.skin.label) { richText = true };

            // 判断当前是否有武器接入
            bool hasWeapon = _currentFC != null;






            // 1. 确定整个 HUD 的统一起点 X 和 初始 Y
            float startX = _hudOffsetX;
            float currentY = (Screen.height / 2f) + _hudStartYOffset;

            currentY = DrawFCSPanel(startX, currentY, _hudScale, hasWeapon);
            // ==========================================

            // --- 你的绘制代码完全不用改，原样放进来 ---

            currentY += _panelSpacing * _hudScale; // 间距也乘以缩放，保证大比例下不拥挤
            DrawEnvPanel(startX, currentY, _hudScale);


            // ==========================================

            // --- 屏幕中心：科幻风锁定准星 (没拿枪时不绘制准星) ---
            if (hasWeapon && _lockedHorizontalDist > 0f)
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
        private float DrawFCSPanel(float startX, float startY, float scale, bool hasWeapon)
        {
            // 所有的排版参数都基于传入的 scale
            float lh = 20f * scale;
            int titleSize = (int)(15 * scale);
            int textSize = (int)(13 * scale);
            float rectWidth = 300f * scale;

            // 3. 动态生成缩放后的样式
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };

            Color mainColor = new Color(0.2f, 1f, 0.4f, 0.9f);

            // 【改动2】：坐标获取兜底机制。如果有枪用枪口位置，没枪用主摄像机（玩家头部）位置
            Vector3 currentPos = hasWeapon ? _currentFC.CurrentFireport.position : Camera.main.transform.position;

            bool hasLockedDistance = _lockedHorizontalDist > 0f;
            bool isFcsLocked = hasWeapon && _hasAmmo && hasLockedDistance;
            // 距离计算也需要安全判断
            float dist3D = 0f;
            if (isFcsLocked)
            {
                dist3D = Vector3.Distance(currentPos, _impactMarker != null ? _impactMarker.transform.position : currentPos + _currentFC.WeaponDirection * _lockedHorizontalDist);
            }

            string compassHeading = GetCompassDir(GetAzimuth().eulerAngles.y);

            float rollAngle = GetAzimuth().eulerAngles.z;
            if (rollAngle > 180f) rollAngle -= 360f;
            float vertAngle = GetAzimuth().eulerAngles.x;
            if (vertAngle > 180f) vertAngle -= 360f;

            string rangeStr = "---";
            if (hasWeapon)
            {
                rangeStr = hasLockedDistance ? $"{_lockedHorizontalDist:F1} M  (3D: {dist3D:F1} M)" : "NO LOCK";
            }

            // --- 【改动3】：根据是否持有武器，格式化火控 UI 数据 ---
            // 只有完全锁定才显示飞行时间
            string tofStr = isFcsLocked ? $"{_lockedTOF:F3} SEC" : "---";

            // 有枪就有角度
            string inclineStr = hasWeapon ? $"{vertAngle:-0.0;+0.0;0.0}°" : "---";
            string cantStr = hasWeapon ? $"{rollAngle:+0.0;-0.0;0.0}°" : "---";

            // 有枪且有弹药，才显示子弹物理参数
            string speedStr = (hasWeapon && _hasAmmo) ? $"{_currentSpeed:F1} M/S" : "---";
            string massStr = (hasWeapon && _hasAmmo) ? $"{_currentMass:F1} G" : "---";
            string bcStr = (hasWeapon && _hasAmmo) ? $"{_currentBC:F3}" : "---";

            // 顶部状态栏：没武器显示暗淡的 NO WEAPON
            string fcsStatusText;

            if (!hasWeapon)
            {
                fcsStatusText = "[ DIRECTOR FCS: NO WEAPON ]";
            }
            else if (!_hasAmmo)
            {
                fcsStatusText = "[ DIRECTOR FCS: NO AMMO ]"; // 枪膛空了！
            }
            else if (hasLockedDistance)
            {
                fcsStatusText = "[ DIRECTOR FCS: TARGET LOCKED ]"; // 弹药和距离记忆都在！
            }
            else
            {
                fcsStatusText = "[ DIRECTOR FCS: STANDBY ]"; // 有枪有弹，等待按 T 测距
            }

            // [上部]：核心火控数据
            DrawShadowLabel(new Rect(startX, startY, 400, 25), $"<b>{fcsStatusText}</b>", mainColor, titleStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 1, rectWidth, lh), $"HEADING   : {GetAzimuth().eulerAngles.y:000}° [{compassHeading}]", mainColor, textStyle);
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
                // 系统在线，生成动态心跳码
                hexCode = "0x" + UnityEngine.Random.Range(0x1000, 0xFFFF).ToString("X4");

                var pwa = CorrectPlayer.ProceduralWeaponAnimation;
                bool isAiming = (pwa != null && pwa.IsAiming);
                if (!_hasAmmo)
                {
                    aimStatus = "NO_AMMO";   // 错误：没子弹，算不出落点
                }
                else if (!isAiming)
                {
                    // 没开镜时，光学投射器关闭，火控处于最基础的待机状态
                    aimStatus = "STANDBY";
                }
                else
                {
                    // 玩家开镜了，光学系统激活，开始自检
                    if (hasLockedDistance)
                    {
                        aimStatus = "TRACKED"; // 完美：弹药+距离齐全，正在投射 3D 落点
                    }
                    else
                    {
                        aimStatus = "OPTIC_SYNC"; // 基础：有子弹但没测距，常规瞄具同步中
                    }
                }
            }

            DrawShadowLabel(new Rect(startX, startY + lh * 8f, rectWidth, lh), $"SYSTEM    : {aimStatus} | {hexCode}", mainColor, textStyle);
            return startY + (lh * 10f);
        }
        private void DrawEnvPanel(float startX, float startY, float scale)
        {
            // 拿到上面传下来的 startX 和 startY，直接开画，不需要自己加 Offset
            float lh = 20f * scale;
            int titleSize = (int)(15 * scale);
            int textSize = (int)(13 * scale);
            float rectWidth = 300f * scale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };

            Color atmosColor = new Color(0.3f, 0.8f, 0.9f, 0.85f);

            // --- 战局时钟与天气计算 (保持不变，因为不依赖武器) ---
            string realTimeStr = DateTime.Now.ToString("HH:mm:ss");
            string tarkovTimeStr = "UNKNOWN";
            if (CorrectGameWorld != null && CorrectGameWorld.GameDateTime != null)
            {
                tarkovTimeStr = CorrectGameWorld.GameDateTime.Calculate().ToString("HH:mm:ss");
            }
            Vector3 playerTransform = CorrectPlayer.Transform.position;
            float altitude = playerTransform.y;
            float GetSwing(float x, float y, float muti) { return (Mathf.PerlinNoise(x, y) * 2f - 1f) * muti; }
            float windSpeed = _weatherSeedMap.windSpeed.b + _weatherSeedMap.windSpeed.r * 5f + GetSwing(Time.time * 0.003f, _weatherSeedGlobal + 2f, 2f);
            float windDir = Mathf.Repeat(_weatherSeedMap.windDirection.b + GetSwing(Time.time * 0.00025f, _weatherSeedGlobal + 7f, 30f), 360f);
            float humidity = _weatherSeedMap.humidity.b + _weatherSeedMap.humidity.r * 35f + GetSwing(Time.time * 0.0015f, _weatherSeedGlobal + 41f, 10f);
            float tempC = _weatherSeedMap.temperature.b + _weatherSeedMap.temperature.r * 6f + GetSwing(Time.time * 0.0005f, _weatherSeedGlobal + 67f, 6f);
            float tempF = tempC * 1.8f + 32;
            float hPa = 1013.25f - (altitude * 0.012f) + (Mathf.PerlinNoise(Time.time * 0.0035f, _weatherSeedGlobal + 101f) * 2.25f - 1.05f);

            float relativeWindAngle = windDir - GetAzimuth().eulerAngles.y;
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

            // [下部]：环境传感器数据
            DrawShadowLabel(new Rect(startX, startY, 400, 25), "<b>[ ENVIRONMENT SENSORS ACTIVE ]</b>", atmosColor, titleStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 1, rectWidth, lh), $"LOCATION  : {_cachedLocationName}", atmosColor, textStyle);
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
        // --- 核心工具：绘制带黑色描边/阴影的文字，防瞎眼 ---
        private void DrawShadowLabel(Rect rect, string text, Color textColor, GUIStyle style)
        {
            // 先画黑色的阴影底色 (向右下角偏移 1.5 像素)
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Rect shadowRect = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height);
            GUI.Label(shadowRect, text, style);

            // 再画本体颜色
            GUI.color = textColor;
            GUI.Label(rect, text, style);
        }

        private void UpdateOpticCache()
        {
            var pwa = CorrectPlayer.ProceduralWeaponAnimation;
            if (pwa != null && pwa.IsAiming)
            {
                if (_cachedOpticCamera == null || !_cachedOpticCamera.isActiveAndEnabled)
                {
                    foreach (Camera cam in Camera.allCameras)
                    {
                        if (cam.isActiveAndEnabled && cam != Camera.main && cam.targetTexture != null)
                        {
                            _cachedOpticCamera = cam;
                            break;
                        }
                    }
                }
            }
            else { _cachedOpticCamera = null; }
        }

        private void InitializeMarker()
        {
            _impactMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_impactMarker.GetComponent<Collider>());

            Material mat = new Material(Shader.Find("GUI/Text Shader"));
            mat.color = Color.yellow;

            MeshRenderer renderer = _impactMarker.GetComponent<MeshRenderer>();
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    [HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
    public class GameStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameWorld __instance)
        {
            PluginsCore.CorrectGameWorld = __instance;
            PluginsCore.CorrectPlayer = __instance.MainPlayer;
            PluginsCore._weatherSeedGlobal = UnityEngine.Random.Range(0f, 100000f);
            PluginsCore._weatherRng = new System.Random((int)PluginsCore._weatherSeedGlobal);
            PluginsCore._weatherSeedMap.windSpeed.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windSpeed.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windSpeed.b = PluginsCore._weatherRng.Next(20, 50) / 10f; //最终映射到2-5取float
            PluginsCore._weatherSeedMap.windSpeed.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.windSpeed.x, PluginsCore._weatherSeedMap.windSpeed.y);
            PluginsCore._weatherSeedMap.windDirection.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windDirection.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windDirection.b = PluginsCore._weatherRng.Next(0, 360);
            PluginsCore._weatherSeedMap.windDirection.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.windDirection.x, PluginsCore._weatherSeedMap.windDirection.y);
            PluginsCore._weatherSeedMap.humidity.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.humidity.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.humidity.b = PluginsCore._weatherRng.Next(1500, 3500) / 100f;
            PluginsCore._weatherSeedMap.humidity.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.humidity.x, PluginsCore._weatherSeedMap.humidity.y);
            PluginsCore._weatherSeedMap.temperature.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.temperature.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.temperature.b = PluginsCore._weatherRng.Next(60, 120) / 10f;
            PluginsCore._weatherSeedMap.temperature.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.temperature.x, PluginsCore._weatherSeedMap.temperature.y);
            var locationId = __instance.LocationId;
            var location = locationId.Localized();
            PluginsCore._cachedLocationName = string.IsNullOrEmpty(location) ? locationId : location;

        }
    }

    public static class OracleBallistics
    {
        private static readonly Vector3 Gravity = Physics.gravity;

        public static Vector3 SimulateToHorizontalDistance(
         Vector3 startPos, Vector3 vel, float mass, float diam, float bc, float targetH, out float timeOfFlight)
        {
            float mKg = mass / 1000f;
            float dM = diam / 1000f;
            float dragMult = (mKg * 0.0014223f) / (dM * dM * bc);
            float airFactor = 1.2f * (dM * dM * Mathf.PI / 4f);

            Vector3 pos = startPos;
            float dt = 0.01f;
            timeOfFlight = 0f;

            for (int tick = 0; tick < 1000; tick++)
            {
                float vMag = vel.magnitude;
                float dragAcc = EftBulletClass.CalculateG1DragCoefficient(vMag) * dragMult;
                Vector3 acc = Gravity + (airFactor * -dragAcc * vMag * vMag / (mKg * 2f)) * vel.normalized;

                Vector3 nextPos = pos + vel * dt + 0.5f * acc * dt * dt;
                Vector3 nextVel = vel + acc * dt;

                float currH = new Vector2(pos.x - startPos.x, pos.z - startPos.z).magnitude;
                float nextH = new Vector2(nextPos.x - startPos.x, nextPos.z - startPos.z).magnitude;

                if (nextH >= targetH)
                {
                    float t = (targetH - currH) / (nextH - currH);
                    timeOfFlight += dt * t;
                    return Vector3.Lerp(pos, nextPos, t);
                }

                pos = nextPos;
                vel = nextVel;
                timeOfFlight += dt;
            }
            return pos;
        }
    }
}