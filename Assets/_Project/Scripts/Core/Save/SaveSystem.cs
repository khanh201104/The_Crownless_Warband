using System;
using System.IO;
using CrownlessWarband.Core.Battle;
using CrownlessWarband.Core.Roguelite;
using Newtonsoft.Json;

namespace CrownlessWarband.Core.Save
{
    /// <summary>
    /// Hệ thống lưu trữ và nạp dữ liệu trận đấu và chiến dịch dưới dạng JSON (GDD Section 25 & 46).
    /// Pure C# - Không phụ thuộc UnityEngine hay MonoBehaviour.
    /// </summary>
    public static class SaveSystem
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        /// <summary>
        /// Chuyển đổi trạng thái Campaign RunState sang chuỗi JSON.
        /// </summary>
        public static string SerializeRun(RunState run)
        {
            if (run == null) return string.Empty;
            return JsonConvert.SerializeObject(run, SerializerSettings);
        }

        /// <summary>
        /// Khôi phục RunState từ chuỗi JSON.
        /// </summary>
        public static RunState DeserializeRun(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<RunState>(json, SerializerSettings);
        }

        /// <summary>
        /// Lưu trạng thái RunState vào file JSON tại đường dẫn filePath.
        /// </summary>
        public static void SaveRunToFile(RunState run, string filePath)
        {
            if (run == null || string.IsNullOrWhiteSpace(filePath)) return;

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = SerializeRun(run);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Đọc và khôi phục RunState từ file JSON tại filePath.
        /// </summary>
        public static RunState LoadRunFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return DeserializeRun(json);
        }

        /// <summary>
        /// Chuyển đổi trạng thái BattleState sang chuỗi JSON.
        /// </summary>
        public static string SerializeBattle(BattleState battle)
        {
            if (battle == null) return string.Empty;
            return JsonConvert.SerializeObject(battle, SerializerSettings);
        }

        /// <summary>
        /// Khôi phục BattleState từ chuỗi JSON.
        /// </summary>
        public static BattleState DeserializeBattle(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<BattleState>(json, SerializerSettings);
        }

        /// <summary>
        /// Lưu trạng thái BattleState vào file JSON tại đường dẫn filePath.
        /// </summary>
        public static void SaveBattleToFile(BattleState battle, string filePath)
        {
            if (battle == null || string.IsNullOrWhiteSpace(filePath)) return;

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = SerializeBattle(battle);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Đọc và khôi phục BattleState từ file JSON tại filePath.
        /// </summary>
        public static BattleState LoadBattleFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return DeserializeBattle(json);
        }
    }
}

