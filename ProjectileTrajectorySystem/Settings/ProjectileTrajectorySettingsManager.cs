// ProjectileTrajectorySettingsManager.cs
using System;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using TaleWorlds.Library;

namespace ProjectileTrajectorySystem
{
    public static class ProjectileTrajectorySettingsManager
    {
        private static readonly string ConfigFolder =
            Path.Combine(BasePath.Name, "Modules", "ProjectileTrajectorySystem");

        private static readonly string XmlPath =
            Path.Combine(ConfigFolder, "ProjectileTrajectorySettings.xml");

        private static readonly object _lock = new object();
        private static FileSystemWatcher _watcher;
        private static bool _reloadRequested;
        private static DateTime _lastReloadTime = DateTime.MinValue;

        public static ProjectileTrajectorySettingsData Data { get; private set; }

        public static ProjectileTrajectorySettingsData Settings
        {
            get
            {
                if (Data == null)
                    Load();
                return Data;
            }
        }

        public static void Load()
        {
            lock (_lock)
            {
                if (!Directory.Exists(ConfigFolder))
                    Directory.CreateDirectory(ConfigFolder);

                if (File.Exists(XmlPath))
                {
                    LoadFromXml();
                }
                else
                {
                    Data = new ProjectileTrajectorySettingsData();
                    SaveToXml();
                }

                StartWatcher();
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                SaveToXml();
            }
        }

        private static void LoadFromXml()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ProjectileTrajectorySettingsData));
                using var stream = new FileStream(XmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Data = (ProjectileTrajectorySettingsData)serializer.Deserialize(stream)
                       ?? new ProjectileTrajectorySettingsData();
            }
            catch
            {
                Data = new ProjectileTrajectorySettingsData();
            }
        }

        private static void SaveToXml()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(ProjectileTrajectorySettingsData));
                using var stream = new FileStream(XmlPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                serializer.Serialize(stream, Data ?? new ProjectileTrajectorySettingsData());
            }
            catch { }
        }

        private static void StartWatcher()
        {
            if (_watcher != null)
                return;

            _watcher = new FileSystemWatcher(ConfigFolder, "ProjectileTrajectorySettings.xml");
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            _watcher.Changed += OnXmlChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private static void OnXmlChanged(object sender, FileSystemEventArgs e)
        {
            // 防止连续触发
            var now = DateTime.Now;
            if ((now - _lastReloadTime).TotalMilliseconds < 300)
                return;

            _lastReloadTime = now;
            _reloadRequested = true;
        }

        /// <summary>
        /// 在主线程（如 Mission Tick）中调用
        /// </summary>
        public static void ProcessPendingReload()
        {
            if (!_reloadRequested)
                return;

            lock (_lock)
            {
                _reloadRequested = false;
                LoadFromXml();
            }
        }

        public static void SyncFromMCM(ProjectileTrajectorySettings mcm)
        {
            if (mcm == null) return;
            lock (_lock)
            {
                if (Data == null) Data = new ProjectileTrajectorySettingsData();

                Data.EnableTrajectory = mcm.EnableTrajectory;
                Data.PlayerTrajectory = mcm.PlayerTrajectory;
                Data.EnemyTrajectory = mcm.EnemyTrajectory;
                Data.MissileTrajectory = mcm.MissileTrajectory;
                Data.EnemyHighlight = mcm.EnemyHighlight;
                Data.UseGameEntityDisplay = mcm.UseGameEntityDisplay;
                Data.UseDebugLineDisplay = mcm.UseDebugLineDisplay;
                Data.UseGameEntityForPlayerRanged = mcm.UseGameEntityForPlayerRanged;
                Data.UseGameEntityForPlayerSiege = mcm.UseGameEntityForPlayerSiege;
                Data.MaxTrackedEnemiesLegacy = mcm.MaxTrackedEnemiesLegacy;
                Data.EnableSlowMotion = mcm.EnableSlowMotion;
                Data.SlowMoEnemyRadius = mcm.SlowMoEnemyRadius;
                Data.SlowMoTimeScale = mcm.SlowMoTimeScale;
                Data.EnableLeadPrediction = mcm.EnableLeadPrediction;
                Data.SiegeWeaponShootFix = mcm.SiegeWeaponShootFix;
                Data.SiegeWeaponSmartSkip = mcm.SiegeWeaponSmartSkip;
                Data.SiegeWeaponDebugText = mcm.SiegeWeaponDebugText;

                SaveToXml();
            }
        }
    }
}