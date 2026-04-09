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
        #region BepInEx 快捷键配置
        public static ConfigEntry<KeyboardShortcut> KeyFcsToggle; // HUD 面板总开关
        public static ConfigEntry<KeyboardShortcut> KeyFcsClear;  // 解除目标锁定（清空距离数据）

        public static ConfigEntry<KeyboardShortcut> KeyDistUp100; // 手动增加距离 100m
        public static ConfigEntry<KeyboardShortcut> KeyDistDown100;// 手动减少距离 100m
        public static ConfigEntry<KeyboardShortcut> KeyDistUp10;  // 手动增加距离 10m
        public static ConfigEntry<KeyboardShortcut> KeyDistDown10;// 手动减少距离 10m
        public static ConfigEntry<KeyboardShortcut> KeyDistUp1;   // 手动增加距离 1m
        public static ConfigEntry<KeyboardShortcut> KeyDistDown1; // 手动减少距离 1m
        #endregion

        #region 火控状态与实时缓存
        public static bool _isFcsActive = false;         // 预留的火控核心工作状态开关
        public static bool _isHudActive = true;          // 控制左侧火控HUD面板的显示状态
        public static bool _hasLockedDistance = false;   // 目标距离锁定状态
        public static bool _hasAmmo = false;             // 枪膛内是否有弹药的状态标记

        // 当前武器与弹药的实时物理数据缓存
        public static float _currentSpeed = 0f;          // 枪口初速 (结合武器枪管系数)
        public static float _currentMass = 0f;           // 弹头质量 (克)
        public static float _currentBC = 0f;             // 弹道系数 (Ballistic Coefficient)
        public static float _currentDiam = 0f;           // 弹头直径 (毫米)

        // 目标锁定数据
        public static float _lockedHorizontalDist;       // 锁定的水平距离
        public static float _lockedTOF;                  // 缓存的子弹飞行时间 (Time of Flight)
        #endregion

        #region HUD UI 布局与排版配置
        public static float _hudOffsetX = 30f;           // HUD 整体距离屏幕左侧的 X 轴绝对距离
        public static float _hudStartYOffset = -180f;    // HUD 整体顶部距离屏幕中心的 Y 轴偏移量
        public static float _hudScale = 1.0f;            // 全局 UI 缩放比例，保证多分辨率下的排版一致性
        public static float _panelSpacing = 15f;         // 多个面板之间的垂直间距
        #endregion

        #region 环境与天气系统数据
        public static float _weatherSeedGlobal;          // 战局全局天气随机种子
        public static System.Random _weatherRng;         // 基于种子的随机数生成器

        // 天气参数结构体，用于生成平滑的伪随机气象数据
        public class WeatherSeed
        {
            public float b; // Base value (基准值)
            public float x; // Perlin noise X offset
            public float y; // Perlin noise Y offset
            public float r; // Range/Multiplier (波动范围)
        }

        // 气象数据集合
        public class WeatherSeedMap
        {
            public WeatherSeed windSpeed = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
            public WeatherSeed windDirection = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
            public WeatherSeed humidity = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
            public WeatherSeed temperature = new WeatherSeed { b = 0f, x = 0f, y = 0f, r = 0f };
        }

        public static WeatherSeedMap _weatherSeedMap = new WeatherSeedMap();
        public static string _cachedLocationName = null; // 当前战局地图名称缓存
        #endregion

        #region 游戏对象与组件缓存
        public static int _layerMask = -1;               // 激光测距射线检测的层级遮罩
        public static Player CorrectPlayer { get; set; } // 当前玩家实例
        public static GameWorld CorrectGameWorld { get; set; }// 当前战局世界实例

        private EFT.Player.FirearmController _currentFC; // 当前玩家手持的武器控制器
        private static Camera _cachedOpticCamera;        // 瞄准镜画中画 (PIP) 摄像机缓存
        private GameObject _impactMarker;                // 3D 弹着点预测标记物 (小黄球)
        #endregion

        public void Awake()
        {
            // 初始化快捷键配置
            KeyFcsToggle = Config.Bind("1. Controls", "Toggle HUD", new KeyboardShortcut(KeyCode.KeypadDivide), "开启/关闭火控面板");
            KeyFcsClear = Config.Bind("1. Controls", "Clear Target (Unlock)", new KeyboardShortcut(KeyCode.Backspace), "脱锁并清除距离数据");

            KeyDistUp100 = Config.Bind("2. Manual Dial", "Distance +100m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftShift), "手动增加距离 100m");
            KeyDistDown100 = Config.Bind("2. Manual Dial", "Distance -100m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftShift), "手动减少距离 100m");

            KeyDistUp10 = Config.Bind("2. Manual Dial", "Distance +10m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftAlt), "手动增加距离 10m");
            KeyDistDown10 = Config.Bind("2. Manual Dial", "Distance -10m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftAlt), "手动减少距离 10m");

            KeyDistUp1 = Config.Bind("2. Manual Dial", "Distance +1m", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl), "手动增加距离 1m");
            KeyDistDown1 = Config.Bind("2. Manual Dial", "Distance -1m", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl), "手动减少距离 1m");

            // 注册 Harmony 补丁
            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
        }

        public void Update()
        {
            if (CorrectPlayer == null || Camera.main == null) return;

            // 初始化测距射线检测层，包含地形、高精度碰撞体和默认层
            if (_layerMask == -1) _layerMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "Default");

            _currentFC = CorrectPlayer.HandsController as EFT.Player.FirearmController;

            // 1. 监测当前武器弹药状态，并更新实时火控弹道参数
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

            // 2. 状态判定：拥有武器且有弹药，并且获取到目标距离时，才视为进入完全锁定状态
            bool hasLockedDistance = _lockedHorizontalDist > 0f;
            bool isFcsLocked = (_currentFC != null) && _hasAmmo && hasLockedDistance;

            UpdateOpticCache();

            // ==========================================
            // 快捷键监听与火控数据处理模块
            // ==========================================

            // 监听面板显隐开关
            if (KeyFcsToggle.Value.IsDown()) _isHudActive = !_isHudActive;

            // 监听激光测距快捷键 (T键覆盖当前距离)
            if (Input.GetKeyDown(KeyCode.T))
            {
                ExecuteFcsLogic();
            }

            if (_currentFC != null)
            {
                // 监听手动脱锁
                if (KeyFcsClear.Value.IsDown())
                {
                    _lockedHorizontalDist = 0f;
                }

                // 监听手动表尺微调输入
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
                    _lockedHorizontalDist = (int)_lockedHorizontalDist; // 保持整数米为单位

                    // 数据底线防呆：距离归零或为负数时等同于脱锁
                    if (_lockedHorizontalDist <= 0f)
                    {
                        _lockedHorizontalDist = 0f;
                    }
                }
            }

            // 3. 3D 落点仿真模块 (仅在完全锁定且开镜瞄准时计算并渲染)
            if (isFcsLocked)
            {
                bool isAiming = false;
                var pwa = CorrectPlayer.ProceduralWeaponAnimation;
                if (pwa != null) isAiming = pwa.IsAiming;

                if (isAiming)
                {
                    if (_impactMarker == null) InitializeMarker();

                    // 获取武器枪口的世界坐标与方向
                    Vector3 currentFireportPos = _currentFC.CurrentFireport.position;
                    Vector3 currentBoreDir = _currentFC.WeaponDirection.normalized;
                    Vector3 currentVelocity = currentBoreDir * _currentSpeed;

                    // 调用弹道引擎计算预测落点
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

                    // 根据摄像机距离动态调整标记球的大小，使其在视野中保持恒定大小
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
                if (_impactMarker != null && _impactMarker.activeSelf) _impactMarker.SetActive(false);
            }
        }

        /// <summary>
        /// 执行火控测距逻辑，发射中心射线获取目标点坐标
        /// </summary>
        private void ExecuteFcsLogic()
        {
            if (_currentFC == null) return;

            // 从屏幕中心向前方发射射线
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _layerMask))
            {
                Vector3 fireportPos = _currentFC.CurrentFireport.position;
                // 计算目标相对于枪口的水平距离与垂直高差
                _lockedHorizontalDist = new Vector2(hit.point.x - fireportPos.x, hit.point.z - fireportPos.z).magnitude;
            }
            else
            {
                _lockedHorizontalDist = 0f;
            }
        }

        /// <summary>
        /// 将方位角转换为16方位的指南针字符串
        /// </summary>
        string GetCompassDir(float az)
        {
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };
            return dirs[(int)Mathf.Round(((az % 360) / 22.5f))];
        }

        /// <summary>
        /// 获取当前视角的 Transform（优先使用瞄准镜摄像机）
        /// </summary>
        Transform GetAzimuth()
        {
            return (_cachedOpticCamera != null && _cachedOpticCamera.isActiveAndEnabled)
                                     ? _cachedOpticCamera.transform
                                     : Camera.main.transform;
        }

        public void OnGUI()
        {
            if (!_isHudActive) return;
            if (Camera.main == null || CorrectPlayer == null) return;

            GUIStyle hudStyle = new GUIStyle(GUI.skin.label) { richText = true };
            bool hasWeapon = _currentFC != null;

            // 计算 HUD 全局起始坐标
            float startX = _hudOffsetX;
            float currentY = (Screen.height / 2f) + _hudStartYOffset;

            // 绘制火控数据面板
            currentY = DrawFCSPanel(startX, currentY, _hudScale, hasWeapon);

            // 叠加间距后绘制环境数据面板
            currentY += _panelSpacing * _hudScale;
            DrawEnvPanel(startX, currentY, _hudScale);

            // 绘制科幻风格的屏幕中心锁定准星
            if (hasWeapon && _lockedHorizontalDist > 0f)
            {
                float cx = Screen.width / 2f;
                float cy = Screen.height / 2f;
                float size = 50f;    // 准星边框跨度
                float thick = 2f;    // 线条粗细
                float length = 15f;  // 准星折角长度

                // 准星透明度脉冲动画
                float alphaPulse = 0.5f + Mathf.PingPong(Time.time * 2f, 0.5f);
                GUI.color = new Color(0.2f, 1f, 0.4f, alphaPulse);

                // 绘制左上角
                GUI.DrawTexture(new Rect(cx - size, cy - size, length, thick), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - size, cy - size, thick, length), Texture2D.whiteTexture);
                // 绘制右上角
                GUI.DrawTexture(new Rect(cx + size - length, cy - size, length, thick), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + size, cy - size, thick, length), Texture2D.whiteTexture);
                // 绘制左下角
                GUI.DrawTexture(new Rect(cx - size, cy + size, length, thick), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - size, cy + size - length, thick, length), Texture2D.whiteTexture);
                // 绘制右下角
                GUI.DrawTexture(new Rect(cx + size - length, cy + size, length, thick), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + size, cy + size - length, thick, length), Texture2D.whiteTexture);
            }
        }

        /// <summary>
        /// 渲染火控核心参数面板
        /// </summary>
        private float DrawFCSPanel(float startX, float startY, float scale, bool hasWeapon)
        {
            // 基于缩放比例计算排版参数
            float lh = 20f * scale;
            int titleSize = (int)(15 * scale);
            int textSize = (int)(13 * scale);
            float rectWidth = 300f * scale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };

            Color mainColor = new Color(0.2f, 1f, 0.4f, 0.9f);

            // 获取坐标（若无武器则以玩家头部视角作为参考点兜底）
            Vector3 currentPos = hasWeapon ? _currentFC.CurrentFireport.position : Camera.main.transform.position;

            bool hasLockedDistance = _lockedHorizontalDist > 0f;
            bool isFcsLocked = hasWeapon && _hasAmmo && hasLockedDistance;

            // 计算视线到 3D 预测落点的绝对直线距离
            float dist3D = 0f;
            if (isFcsLocked)
            {
                dist3D = Vector3.Distance(currentPos, _impactMarker != null ? _impactMarker.transform.position : currentPos + _currentFC.WeaponDirection * _lockedHorizontalDist);
            }

            string compassHeading = GetCompassDir(GetAzimuth().eulerAngles.y);

            // 获取并格式化滚转角与俯仰角（将角度转换到 -180 ~ 180 区间）
            float rollAngle = GetAzimuth().eulerAngles.z;
            if (rollAngle > 180f) rollAngle -= 360f;
            float vertAngle = GetAzimuth().eulerAngles.x;
            if (vertAngle > 180f) vertAngle -= 360f;

            // 格式化输出数据文本
            string rangeStr = "---";
            if (hasWeapon)
            {
                rangeStr = hasLockedDistance ? $"{_lockedHorizontalDist:F1} M  (3D: {dist3D:F1} M)" : "NO LOCK";
            }

            string tofStr = isFcsLocked ? $"{_lockedTOF:F3} SEC" : "---";
            string inclineStr = hasWeapon ? $"{vertAngle:-0.0;+0.0;0.0}°" : "---";
            string cantStr = hasWeapon ? $"{rollAngle:+0.0;-0.0;0.0}°" : "---";
            string speedStr = (hasWeapon && _hasAmmo) ? $"{_currentSpeed:F1} M/S" : "---";
            string massStr = (hasWeapon && _hasAmmo) ? $"{_currentMass:F1} G" : "---";
            string bcStr = (hasWeapon && _hasAmmo) ? $"{_currentBC:F3}" : "---";

            // 决定系统状态标题栏文本
            string fcsStatusText;
            if (!hasWeapon)
            {
                fcsStatusText = "[ DIRECTOR FCS: NO WEAPON ]";
            }
            else if (!_hasAmmo)
            {
                fcsStatusText = "[ DIRECTOR FCS: NO AMMO ]";
            }
            else if (hasLockedDistance)
            {
                fcsStatusText = "[ DIRECTOR FCS: TARGET LOCKED ]";
            }
            else
            {
                fcsStatusText = "[ DIRECTOR FCS: STANDBY ]";
            }

            // 绘制面板各行数据
            DrawShadowLabel(new Rect(startX, startY, 400, 25), $"<b>{fcsStatusText}</b>", mainColor, titleStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 1, rectWidth, lh), $"HEADING   : {GetAzimuth().eulerAngles.y:000}° [{compassHeading}]", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 2, rectWidth, lh), $"TGT RANGE : {rangeStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 3, rectWidth, lh), $"INCLINE   : {inclineStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 4, rectWidth, lh), $"CANT ANGL : {cantStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 5, rectWidth, lh), $"TIME FLGT : {tofStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 6, rectWidth, lh), $"MUZZLE VEL: {speedStr}", mainColor, textStyle);
            DrawShadowLabel(new Rect(startX, startY + lh * 7, rectWidth, lh), $"PROJ MASS : {massStr} {(hasWeapon ? $"(BC: {bcStr})" : "")}", mainColor, textStyle);

            // 判定并生成系统底层运行状态标识
            string aimStatus = "OFFLINE";
            string hexCode = "0x0000";

            if (hasWeapon)
            {
                // 生成动态的伪16进制心跳码以增加UI视觉效果
                hexCode = "0x" + UnityEngine.Random.Range(0x1000, 0xFFFF).ToString("X4");

                var pwa = CorrectPlayer.ProceduralWeaponAnimation;
                bool isAiming = (pwa != null && pwa.IsAiming);

                if (!_hasAmmo)
                {
                    aimStatus = "NO_AMMO";
                }
                else if (!isAiming)
                {
                    aimStatus = "STANDBY";
                }
                else
                {
                    if (hasLockedDistance)
                    {
                        aimStatus = "TRACKED";
                    }
                    else
                    {
                        aimStatus = "OPTIC_SYNC";
                    }
                }
            }

            DrawShadowLabel(new Rect(startX, startY + lh * 8f, rectWidth, lh), $"SYSTEM    : {aimStatus} | {hexCode}", mainColor, textStyle);
            return startY + (lh * 10f); // 返回计算后的面板底部 Y 坐标，供下一模块堆叠
        }

        /// <summary>
        /// 渲染环境传感器面板 (大气压、风速、温湿度等)
        /// </summary>
        private void DrawEnvPanel(float startX, float startY, float scale)
        {
            float lh = 20f * scale;
            int titleSize = (int)(15 * scale);
            int textSize = (int)(13 * scale);
            float rectWidth = 300f * scale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize };

            Color atmosColor = new Color(0.3f, 0.8f, 0.9f, 0.85f);

            // 获取时间信息
            string realTimeStr = DateTime.Now.ToString("HH:mm:ss");
            string tarkovTimeStr = "UNKNOWN";
            if (CorrectGameWorld != null && CorrectGameWorld.GameDateTime != null)
            {
                tarkovTimeStr = CorrectGameWorld.GameDateTime.Calculate().ToString("HH:mm:ss");
            }

            // 动态气象参数计算 (基于 Perlin 噪声和平滑插值)
            Vector3 playerTransform = CorrectPlayer.Transform.position;
            float altitude = playerTransform.y;
            float GetSwing(float x, float y, float muti) { return (Mathf.PerlinNoise(x, y) * 2f - 1f) * muti; }

            float windSpeed = _weatherSeedMap.windSpeed.b + _weatherSeedMap.windSpeed.r * 5f + GetSwing(Time.time * 0.003f, _weatherSeedGlobal + 2f, 2f);
            float windDir = Mathf.Repeat(_weatherSeedMap.windDirection.b + GetSwing(Time.time * 0.00025f, _weatherSeedGlobal + 7f, 30f), 360f);
            float humidity = _weatherSeedMap.humidity.b + _weatherSeedMap.humidity.r * 35f + GetSwing(Time.time * 0.0015f, _weatherSeedGlobal + 41f, 10f);
            float tempC = _weatherSeedMap.temperature.b + _weatherSeedMap.temperature.r * 6f + GetSwing(Time.time * 0.0005f, _weatherSeedGlobal + 67f, 6f);
            float tempF = tempC * 1.8f + 32;

            // 简单气压模拟 (高度递减 + 噪声波动)
            float hPa = 1013.25f - (altitude * 0.012f) + (Mathf.PerlinNoise(Time.time * 0.0035f, _weatherSeedGlobal + 101f) * 2.25f - 1.05f);

            // 向量风计算 (横风与顶头风分解)
            float relativeWindAngle = windDir - GetAzimuth().eulerAngles.y;
            float crossWind = Mathf.Sin(relativeWindAngle * Mathf.Deg2Rad) * windSpeed;
            float headWind = Mathf.Cos(relativeWindAngle * Mathf.Deg2Rad) * windSpeed;
            string crossDir = crossWind > 0 ? "◄ L" : "R ►";
            string headDir = headWind > 0 ? "HEAD" : "TAIL";

            // 伪造 GPS 坐标数据
            double baseLat = 60.051200;
            double baseLon = 29.351400;
            double currentLat = baseLat + (playerTransform.z * 0.000009);
            double currentLon = baseLon + (playerTransform.x * 0.000018);
            string gpsLat = $"{currentLat:F5}° N";
            string gpsLon = $"{currentLon:F5}° E";

            // 绘制环境参数界面
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

        /// <summary>
        /// 实用工具：绘制带有右下角黑色阴影的文本，提高亮背景下的可视度
        /// </summary>
        private void DrawShadowLabel(Rect rect, string text, Color textColor, GUIStyle style)
        {
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            Rect shadowRect = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height);
            GUI.Label(shadowRect, text, style);

            GUI.color = textColor;
            GUI.Label(rect, text, style);
        }

        /// <summary>
        /// 缓存寻找处于激活状态的瞄准镜 PIP 摄像机
        /// </summary>
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

        /// <summary>
        /// 实例化 3D 预测弹着点的指示器球体，并移除其物理碰撞属性
        /// </summary>
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

    /// <summary>
    /// GameWorld 启动补丁，在此阶段拦截实例并初始化相关的环境种子数据
    /// </summary>
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

            // 为风速生成 Perlin 参数
            PluginsCore._weatherSeedMap.windSpeed.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windSpeed.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windSpeed.b = PluginsCore._weatherRng.Next(20, 50) / 10f; // 最终映射到 2-5
            PluginsCore._weatherSeedMap.windSpeed.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.windSpeed.x, PluginsCore._weatherSeedMap.windSpeed.y);

            // 为风向生成 Perlin 参数
            PluginsCore._weatherSeedMap.windDirection.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windDirection.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windDirection.b = PluginsCore._weatherRng.Next(0, 360);
            PluginsCore._weatherSeedMap.windDirection.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.windDirection.x, PluginsCore._weatherSeedMap.windDirection.y);

            // 为湿度生成 Perlin 参数
            PluginsCore._weatherSeedMap.humidity.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.humidity.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.humidity.b = PluginsCore._weatherRng.Next(1500, 3500) / 100f;
            PluginsCore._weatherSeedMap.humidity.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.humidity.x, PluginsCore._weatherSeedMap.humidity.y);

            // 为温度生成 Perlin 参数
            PluginsCore._weatherSeedMap.temperature.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.temperature.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.temperature.b = PluginsCore._weatherRng.Next(60, 120) / 10f;
            PluginsCore._weatherSeedMap.temperature.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.temperature.x, PluginsCore._weatherSeedMap.temperature.y);

            // 缓存位置名称
            var locationId = __instance.LocationId;
            var location = locationId.Localized();
            PluginsCore._cachedLocationName = string.IsNullOrEmpty(location) ? locationId : location;
        }
    }

    /// <summary>
    /// 核心弹道解算器类，使用近似塔科夫 G1 空气动力学模型的算法模拟弹道轨迹
    /// </summary>
    public static class OracleBallistics
    {
        private static readonly Vector3 Gravity = Physics.gravity;

        /// <summary>
        /// 基于步长积分算法，推演子弹飞抵目标水平距离时的空间坐标
        /// </summary>
        /// <param name="startPos">开火点三维坐标</param>
        /// <param name="vel">初速矢量</param>
        /// <param name="mass">弹头质量 (克)</param>
        /// <param name="diam">弹头直径 (毫米)</param>
        /// <param name="bc">弹道系数</param>
        /// <param name="targetH">预期的目标水平距离</param>
        /// <param name="timeOfFlight">输出：该轨迹段的飞行耗时</param>
        /// <returns>最终落点的三维坐标</returns>
        public static Vector3 SimulateToHorizontalDistance(
         Vector3 startPos, Vector3 vel, float mass, float diam, float bc, float targetH, out float timeOfFlight)
        {
            float mKg = mass / 1000f;
            float dM = diam / 1000f;

            // 计算基础阻力系数标量
            float dragMult = (mKg * 0.0014223f) / (dM * dM * bc);
            float airFactor = 1.2f * (dM * dM * Mathf.PI / 4f);

            Vector3 pos = startPos;
            float dt = 0.01f; // 积分时间步长
            timeOfFlight = 0f;

            for (int tick = 0; tick < 1000; tick++)
            {
                float vMag = vel.magnitude;
                float dragAcc = EftBulletClass.CalculateG1DragCoefficient(vMag) * dragMult;

                // 加速度 = 重力 + 空气阻力
                Vector3 acc = Gravity + (airFactor * -dragAcc * vMag * vMag / (mKg * 2f)) * vel.normalized;

                Vector3 nextPos = pos + vel * dt + 0.5f * acc * dt * dt;
                Vector3 nextVel = vel + acc * dt;

                // 检查当前步与下一步在水平面上距离开火点的绝对距离
                float currH = new Vector2(pos.x - startPos.x, pos.z - startPos.z).magnitude;
                float nextH = new Vector2(nextPos.x - startPos.x, nextPos.z - startPos.z).magnitude;

                // 如果落入区间，则使用线性插值求解精确的触达点与时间
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