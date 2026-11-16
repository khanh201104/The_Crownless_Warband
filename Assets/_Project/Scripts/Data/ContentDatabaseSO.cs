using System.Collections.Generic;
using CrownlessWarband.Core.Battle;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace CrownlessWarband.Data
{
    /// <summary>
    /// Cơ sở dữ liệu nội dung ScriptableObject cho The Crownless Warband (GDD Sections 34 & 47).
    /// Chứa danh sách nhân vật phe Player, kẻ địch/Boss và toàn bộ kỹ năng, kèm tiện ích sinh asset tự động trong Editor.
    /// </summary>
    [CreateAssetMenu(fileName = "DefaultContentDatabase", menuName = "CrownlessWarband/Content Database")]
    public class ContentDatabaseSO : ScriptableObject
    {
        public List<UnitDataSO> PlayerUnits = new List<UnitDataSO>();
        public List<UnitDataSO> EnemyUnits = new List<UnitDataSO>();
        public List<SkillDataSO> AllSkills = new List<SkillDataSO>();

        public static ContentDatabaseSO CreateRuntimeDefaultDatabase()
        {
            ContentDatabaseSO db = CreateInstance<ContentDatabaseSO>();
            db.name = "DefaultContentDatabase";
            PopulateDatabase(db, persistToEditorAssets: false);
            return db;
        }

        private static void PopulateDatabase(ContentDatabaseSO db, bool persistToEditorAssets)
        {
            db.PlayerUnits.Clear();
            db.EnemyUnits.Clear();
            db.AllSkills.Clear();

            // 1. Kỹ năng mặc định cho 5 Class Player
            SkillDataSO pGBasic = BuildSkill("p_g_basic", "Shield Strike", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, false, persistToEditorAssets, db);
            SkillDataSO pGS1 = BuildSkill("p_g_s1", "Protect Ally", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleAlly, SkillEffectType.ProtectAlly, 0, 2, 1.0f, false, persistToEditorAssets, db);
            SkillDataSO pGS2 = BuildSkill("p_g_s2", "Bastion Shield", SkillSlotType.Skill2, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 2, 2.0f, false, persistToEditorAssets, db);

            SkillDataSO pVBasic = BuildSkill("p_v_basic", "Heavy Slash", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, false, persistToEditorAssets, db);
            SkillDataSO pVS1 = BuildSkill("p_v_s1", "Stunning Blow", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.2f, false, persistToEditorAssets, db);

            SkillDataSO pMBasic = BuildSkill("p_m_basic", "Arcane Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true, persistToEditorAssets, db);
            SkillDataSO pMS1 = BuildSkill("p_m_s1", "Disable Frost", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.3f, true, persistToEditorAssets, db);
            SkillDataSO pMUlt = BuildSkill("p_m_ult", "Meteor Storm", SkillSlotType.Ultimate, SkillControlMode.Manual, SkillTargetType.AllEnemies, SkillEffectType.Damage, 30, 3, 1.8f, true, persistToEditorAssets, db);

            SkillDataSO pRBasic = BuildSkill("p_r_basic", "Quick Shot", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.0f, true, persistToEditorAssets, db);
            SkillDataSO pRS1 = BuildSkill("p_r_s1", "Hunter's Mark", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Mark, 0, 2, 1.1f, true, persistToEditorAssets, db);
            SkillDataSO pRUlt = BuildSkill("p_r_ult", "Snipe Execute", SkillSlotType.Ultimate, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.ExecuteDamage, 30, 3, 2.2f, true, persistToEditorAssets, db);

            SkillDataSO pMedBasic = BuildSkill("p_med_basic", "Light Bolt", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 0.9f, true, persistToEditorAssets, db);
            SkillDataSO pMedS1 = BuildSkill("p_med_s1", "Emergency Heal", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.LowestHpAlly, SkillEffectType.Heal, 0, 1, 2.1f, false, persistToEditorAssets, db);

            // 2. Kỹ năng mặc định cho Boss The Warden
            SkillDataSO bwBasic = BuildSkill("bw_basic", "Crushing Greathammer", SkillSlotType.BasicAttack, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Damage, 0, 0, 1.2f, false, persistToEditorAssets, db);
            SkillDataSO bwShield = BuildSkill("bw_shield", "Fortress Barrier", SkillSlotType.Skill1, SkillControlMode.Auto, SkillTargetType.Self, SkillEffectType.Shield, 20, 2, 2.0f, false, persistToEditorAssets, db);
            SkillDataSO bwSweep = BuildSkill("bw_sweep", "Earthshatter Sweep", SkillSlotType.Ultimate, SkillControlMode.Auto, SkillTargetType.AllEnemies, SkillEffectType.Damage, 30, 3, 1.2f, true, persistToEditorAssets, db);
            SkillDataSO bwStun = BuildSkill("bw_stun", "Oppressive Aura", SkillSlotType.Skill2, SkillControlMode.Auto, SkillTargetType.SingleEnemy, SkillEffectType.Stun, 20, 2, 1.0f, false, persistToEditorAssets, db);

            // 3. 5 Unit Player mặc định (GDD Section 53)
            UnitDataSO guardian = BuildUnit("Guardian_SO", "p_guardian", "Guardian", TeamSide.Player, UnitRole.Guardian, RowType.Front, 0, 320, 28, 26, 11, 20f, new[] { pGBasic, pGS1, pGS2 }, persistToEditorAssets);
            UnitDataSO vanguard = BuildUnit("Vanguard_SO", "p_vanguard", "Vanguard", TeamSide.Player, UnitRole.Vanguard, RowType.Front, 1, 250, 38, 18, 14, 25f, new[] { pVBasic, pVS1 }, persistToEditorAssets);
            UnitDataSO mage = BuildUnit("Mage_SO", "p_mage", "Mage", TeamSide.Player, UnitRole.Mage, RowType.Mid, 1, 175, 45, 10, 13, 35f, new[] { pMBasic, pMS1, pMUlt }, persistToEditorAssets);
            UnitDataSO ranger = BuildUnit("Ranger_SO", "p_ranger", "Ranger", TeamSide.Player, UnitRole.Ranger, RowType.Back, 0, 165, 50, 8, 16, 40f, new[] { pRBasic, pRS1, pRUlt }, persistToEditorAssets);
            UnitDataSO medic = BuildUnit("Medic_SO", "p_medic", "Medic", TeamSide.Player, UnitRole.Medic, RowType.Back, 2, 180, 38, 10, 15, 35f, new[] { pMedBasic, pMedS1 }, persistToEditorAssets);

            db.PlayerUnits.Add(guardian);
            db.PlayerUnits.Add(vanguard);
            db.PlayerUnits.Add(mage);
            db.PlayerUnits.Add(ranger);
            db.PlayerUnits.Add(medic);

            // 4. Boss The Warden (GDD Section 38)
            UnitDataSO theWarden = BuildUnit("TheWarden_SO", "boss_warden", "The Warden", TeamSide.Enemy, UnitRole.Guardian, RowType.Front, 1, 340, 30, 20, 12, 100f, new[] { bwBasic, bwShield, bwSweep, bwStun }, persistToEditorAssets);
            db.EnemyUnits.Add(theWarden);
        }

        private static SkillDataSO BuildSkill(
            string skillId,
            string skillName,
            SkillSlotType slot,
            SkillControlMode controlMode,
            SkillTargetType targetType,
            SkillEffectType effectType,
            int energyCost,
            int cooldownTurns,
            float powerMultiplier,
            bool canTargetBackline,
            bool persistToEditorAssets,
            ContentDatabaseSO db)
        {
            SkillDataSO skill = CreateInstance<SkillDataSO>();
            skill.name = skillId;
            skill.SkillId = skillId;
            skill.SkillName = skillName;
            skill.Slot = slot;
            skill.DefaultControlMode = controlMode;
            skill.TargetType = targetType;
            skill.EffectType = effectType;
            skill.EnergyCost = energyCost;
            skill.CooldownTurns = cooldownTurns;
            skill.PowerMultiplier = powerMultiplier;
            skill.CanTargetBackline = canTargetBackline;

#if UNITY_EDITOR
            if (persistToEditorAssets)
            {
                string path = $"Assets/_Project/ScriptableObjects/Skills/{skillId}.asset";
                skill = SaveOrUpdateEditorAsset(skill, path);
            }
#endif

            db.AllSkills.Add(skill);
            return skill;
        }

        private static UnitDataSO BuildUnit(
            string assetFileName,
            string characterId,
            string unitName,
            TeamSide team,
            UnitRole role,
            RowType row,
            int col,
            int maxHp,
            int atk,
            int def,
            int spd,
            float baseThreat,
            SkillDataSO[] skills,
            bool persistToEditorAssets)
        {
            UnitDataSO unit = CreateInstance<UnitDataSO>();
            unit.name = assetFileName;
            unit.CharacterId = characterId;
            unit.UnitName = unitName;
            unit.DefaultTeam = team;
            unit.Role = role;
            unit.DefaultRow = row;
            unit.DefaultColumn = col;
            unit.MaxHP = maxHp;
            unit.Attack = atk;
            unit.Defense = def;
            unit.Speed = spd;
            unit.BaseThreat = baseThreat;
            unit.DefaultAffix = EnemyAffixType.None;

            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null)
                {
                    unit.Skills.Add(skills[i]);
                }
            }

#if UNITY_EDITOR
            if (persistToEditorAssets)
            {
                string path = $"Assets/_Project/ScriptableObjects/Units/{assetFileName}.asset";
                unit = SaveOrUpdateEditorAsset(unit, path);
            }
#endif

            return unit;
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Crownless Warband/Generate Default ScriptableObject Assets")]
        public static void GenerateDefaultScriptableObjectAssets()
        {
            EnsureFolderExists("Assets/_Project/ScriptableObjects");
            EnsureFolderExists("Assets/_Project/ScriptableObjects/Skills");
            EnsureFolderExists("Assets/_Project/ScriptableObjects/Units");

            ContentDatabaseSO db = CreateInstance<ContentDatabaseSO>();
            db.name = "DefaultContentDatabase";
            PopulateDatabase(db, persistToEditorAssets: true);

            SaveOrUpdateEditorAsset(db, "Assets/_Project/ScriptableObjects/DefaultContentDatabase.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CrownlessWarband] Generated Default ScriptableObject Assets in Assets/_Project/ScriptableObjects/");
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (!Directory.Exists(assetFolderPath))
            {
                Directory.CreateDirectory(assetFolderPath);
            }
        }

        private static T SaveOrUpdateEditorAsset<T>(T source, string assetPath) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(source, existing);
                EditorUtility.SetDirty(existing);
                DestroyImmediate(source);
                return existing;
            }

            AssetDatabase.CreateAsset(source, assetPath);
            EditorUtility.SetDirty(source);
            return source;
        }
#endif
    }
}
