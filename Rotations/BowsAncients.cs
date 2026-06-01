using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using ImGuiNET;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

using SColor = System.Drawing.Color;

namespace SkillFlow;


public class BowsAncientsRotation : IRotation{
    public string Name => "Bows : Return of the Ancients";
    public string Desc => "Iceshot+Snipe, LA+Rod";

    public BowsAncientsSettings Settings { get; private set; }
    public Settings PluginSettings { get; set; }

    public BowsAncientsRotation(Settings settings) {
        PluginSettings = settings;
        Settings = settings.Rotation_BowsAncients;
    }

    private class Skill {
        public string Name { get; set; } = "";
        public string ID { get; set; } = "";
        public int PreferredWeaponSet { get; set; } = 0;
        public bool CanBeUsed { get; set; } = false;
        public int CastTime { get; set; } = 0;
        public Skill(string name, string id, int preferredWeaponSet = 0) { Name = name; ID = id; PreferredWeaponSet = preferredWeaponSet; }
        public void Reset() {
            CanBeUsed = false;
            CastTime = 0;
        }
        public void Set(ActorSkill skill) {
            CanBeUsed = skill.CanBeUsed;
            CastTime = (int)skill.CastTime.TotalMilliseconds;
        }
    }
    private class Buff {
        public string Name { get; set; } = "";
        public string ID { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public float Duration { get; set; } = 0;
        public int Charges { get; set; } = 0;
        public Buff(string name, string id) { Name = name; ID = id; }
        public void Reset() {
            IsActive = false;
            Charges = 0;
            Duration = 0;
        }
        public void Set(ExileCore2.PoEMemory.Components.Buff buff) {
            IsActive = true;
            Charges = buff.BuffCharges;
            Duration = buff.Timer;
        }
    }
    private class RotationSnapshot {
        private readonly Dictionary<FieldInfo, object> _defaults;
        private readonly Dictionary<string, Skill> _skillById;
        private readonly Dictionary<string, Buff> _buffById;
        private static readonly HashSet<string> _greaterTargetBossNames = new() {
            "Zalmarath, the Colossus",
        };

        public Buff Buff_Barrage = new("Barrage", "empower_barrage_visual");
        public Buff Buff_IceTip = new("Ice Tip", "shearing_bolts");
        public Buff Buff_FreezingMark = new("Freezing Mark", "freezing_mark_damage_buff");
        public Buff Buff_FreezingSalvo = new("Freezing Salvo", "skill_seals");

        public Buff Debuff_Frozen = new("Frozen", "frozen");
        public Buff Debuff_ElementalExposure = new("Elemental Exposure", "exposure_elemental");
        public Buff Debuff_FreezingMark = new("Freezing Mark", "freezing_mark");

        public Skill Skill_Barrage = new("Barrage", "BarragePlayer");
        public Skill Skill_Snipe = new("Snipe", "SnipePlayer");
        public Skill Skill_IceShot = new("Ice Shot", "IceShotPlayer");
        public Skill Skill_LightningRod = new("Lightning Rod", "LightningRodPlayer");
        public Skill Skill_FrostBomb = new("Frost Bomb", "FrostBombPlayer");
        public Skill Skill_IceTipArrow = new("Ice Tipped Arrows", "IceTippedArrowsPlayer");
        public Skill Skill_FreezingSalvo = new("Freezing Salvo", "FreezingSalvoPlayer");
        public Skill Skill_LightningArrow = new("Lightning Arrow", "LightningArrowPlayer");
        public Skill Skill_FreezingMark = new("Freezing Mark", "FreezingMarkPlayer", 1);
        public Skill Skill_TornadoShot = new("Tornado Shot", "TornadoShotPlayer");

        public bool MonsterTargetted = false;
        public bool MonsterGreaterTargetted = false;
        public int ActiveRodCount = 0;
        public bool ActiveTornado = false;

        public RotationSnapshot() {
            var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

            _defaults = fields
                .Where(f => f.FieldType.IsPrimitive || f.FieldType == typeof(string))
                .ToDictionary(f => f, f => f.GetValue(this)!);

            _skillById = fields
                .Where(f => f.FieldType == typeof(Skill))
                .Select(f => f.GetValue(this) as Skill)
                .OfType<Skill>()
                .ToDictionary(s => s.ID);

            _buffById = fields
                .Where(f => f.FieldType == typeof(Buff))
                .Select(f => f.GetValue(this) as Buff)
                .OfType<Buff>()
                .ToDictionary(b => b.ID);
        }

        public void SnapshotSkills(Actor playerActor) {
            foreach (var skill in playerActor.ActorSkills) {
                if (!_skillById.TryGetValue(skill.Name, out var s)) continue;
                // preferred set and it can be used, always take it
                if (skill.WeaponSetBinding == s.PreferredWeaponSet && skill.CanBeUsed) s.Set(skill);
                // fallback - only write if no usable result
                else if (!s.CanBeUsed) s.Set(skill);
            }
        }
        public void snapshotPlayerBuffs(Entity player) {
            foreach (var buff in player.Buffs) {
                if (_buffById.TryGetValue(buff.Name, out var b)) b.Set(buff);
            }            
        }
        public void SnapshotEntities(GameController gameController, BowsAncientsSettings settings) {
            var entities = gameController.Entities;
            var cursorGridPos = gameController.IngameState.ServerData.WorldMousePosition.WorldToGrid();

            bool checkRods = Skill_LightningRod.CanBeUsed;
            bool checkTornados = Skill_TornadoShot.CanBeUsed;

            foreach (var entity in entities) {
                if (entity == null || !entity.IsValid) continue;

                if (entity.Type == EntityType.Monster) {
                    if (entity.IsDead || entity.IsHidden || !entity.IsHostile) continue;

                    float distanceToCursor = Vector2.Distance(entity.GridPos, cursorGridPos);
                    if (distanceToCursor > 50) continue;
                    float targetRadius = _greaterTargetBossNames.Contains(entity.RenderName)
                        ? 40
                        : settings.TargetCursorRadius;

                    bool isTargeted = distanceToCursor < targetRadius;
                    bool isGreaterTargeted = isTargeted && entity.Rarity >= MonsterRarity.Rare;

                    if (isTargeted) MonsterTargetted = true;
                    if (isGreaterTargeted) MonsterGreaterTargetted = true;

                    foreach (var buff in entity.Buffs) {
                        if (buff.Name == Debuff_FreezingMark.ID) {
                            Debuff_FreezingMark.Set(buff);
                            continue;
                        }
                        if (isGreaterTargeted && _buffById.TryGetValue(buff.Name, out var b))
                            b.Set(buff);                        
                    }
                    continue;
                }
                if (!ActiveTornado && checkTornados && entity.Type == EntityType.MiscellaneousObjects &&
                    entity.Path == "Metadata/MiscellaneousObjects/TornadoShotTornado" &&
                    Vector2.Distance(entity.GridPos, cursorGridPos) <= settings.TornadoCursorRadius) {
                    ActiveTornado = true;
                }
                if (checkRods && entity.Type == EntityType.MiscellaneousObjects &&                    
                    entity.Path.Contains("LightningRod") &&
                    Vector2.Distance(entity.GridPos, cursorGridPos) <= settings.RodCursorRadius) {
                    ActiveRodCount++;
                }
            }
        }

        public void Monitor() {
            foreach (var skill in _skillById.Values) {
                DBug.Monitor("Skills", skill.Name, skill.CanBeUsed);
                DBug.Monitor("Skills", skill.Name + ", CastTime(ms):", skill.CastTime);
            }
            foreach (var buff in _buffById.Values) {
                DBug.Monitor("Buffs", buff.Name, buff.IsActive);
                DBug.Monitor("Buffs", buff.Name + ", Duration:", buff.Duration);
                DBug.Monitor("Buffs", buff.Name + ", Charges:", buff.Charges);
            }

            DBug.Monitor("Target", "Targetted", MonsterTargetted);
            DBug.Monitor("Target", "GreaterTargetted", MonsterGreaterTargetted);
            DBug.Monitor("Target", "ActiveRodCount", ActiveRodCount);
            DBug.Monitor("Target", "ActiveTornado", ActiveTornado);
        }
        public void Reset() {
            foreach (var (field, val) in _defaults) field.SetValue(this, val);
            foreach (var skill in _skillById.Values) skill.Reset();
            foreach (var buff in _buffById.Values) buff.Reset();
        }

        }
    private RotationSnapshot SS = new RotationSnapshot();

    public void UpdateTelemetry(GameController gameController) { }
    public RotationAction GetNextAction(GameController gameController) {
        if (gameController == null) return null;

        var player = gameController.Player;
        if (!player.TryGetComponent<Actor>(out var player_actor)) return null;

        // SNIPE CHECKING
        if (player_actor.Animation == AnimationE.SnipeChannel) {
            // SNIPE Release
            if (player_actor.AnimationController.CurrentAnimationStage > 20) return new RotationAction(SS.Skill_Snipe.Name, "Release Snipe", Settings.Snipe_Key, ActionBehavior.KeyRelease, 200);
            // Snipe Hold
            return new RotationAction(SS.Skill_Snipe.Name, "Holding Snipe", Settings.Snipe_Key, ActionBehavior.KeyHold, 50);
        }
        // RUNNING
        bool rot_1 = Input.IsKeyDown(Settings.Rotation1_Key); 
        bool rot_2 = Input.IsKeyDown(Settings.Rotation2_Key); 
        bool rot_3 = Input.IsKeyDown(Settings.Rotation3_Key); 
        if (!rot_1 && !rot_2 && !rot_3) { return null; }
        // bail if finishing a snipe
        if (player_actor.Animation == AnimationE.SnipeChannelEnd || player_actor.Animation == AnimationE.SnipeChannelEndPerfect) return null;
        // SNAPSHOTTING
        SS.Reset();
        SS.SnapshotSkills(player_actor);
        SS.snapshotPlayerBuffs(player);
        SS.SnapshotEntities(gameController, Settings);
        if (PluginSettings.DXT.DBug.ShowMonitor) SS.Monitor();
        // SNIPE TEST
        if (Settings.SnipeTest) {
            // BARRAGE 
            if (!SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed && SS.Skill_Barrage.CanBeUsed) {
                return new RotationAction(SS.Skill_Barrage.Name, "SnipeTest: [Barrage] !buff_barrage", Settings.Barrage_Key, SS.Skill_Barrage.CastTime);
            }
            // SNIPE
            if (SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed) {
                return new RotationAction(SS.Skill_Snipe.Name, $"SnipeTest: [Snipe] buff_barrage, cast time: {SS.Skill_Snipe.CastTime}", Settings.Snipe_Key, ActionBehavior.KeyHold, 50);
            }
            if (!SS.Skill_Barrage.CanBeUsed && SS.Skill_Snipe.CanBeUsed) {
                return new RotationAction(SS.Skill_Snipe.Name, $"SnipeTest: [Snipe] cast time: {SS.Skill_Snipe.CastTime}", Settings.Snipe_Key, ActionBehavior.KeyHold, 50);
            }
            return null;
        }
        // ROTATION 1
        if (rot_1) {
            // Unique or Rare Targetted
            if (SS.MonsterGreaterTargetted) {
                if (Settings.Rot1_BarrageSnipe) {
                    // BARRAGE 
                    if (SS.Debuff_Frozen.IsActive && !SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed && SS.Skill_Barrage.CanBeUsed) {
                        return new RotationAction(SS.Skill_Barrage.Name, "Debuff_Frozen && !Buff_Barrage", Settings.Barrage_Key, SS.Skill_Barrage.CastTime);
                    }
                    // SNIPE 
                    if (SS.Debuff_Frozen.IsActive && SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed) {
                        return new RotationAction(SS.Skill_Snipe.Name, "Debuff_Frozen && Buff_Barrage", Settings.Snipe_Key, ActionBehavior.KeyHold, 50);
                    }
                }
                // FROST BOMB
                if (Settings.Rot1_FrostBomb && !SS.Debuff_ElementalExposure.IsActive && SS.Skill_FrostBomb.CanBeUsed) {
                    return new RotationAction(SS.Skill_FrostBomb.Name, "!Debuff_ElementalExposure", Settings.FrostBomb_Key, SS.Skill_FrostBomb.CastTime);
                }
                // ICE TIPPED ARROWS
                if (Settings.Rot1_IceTipArrow && !SS.Buff_IceTip.IsActive && SS.Skill_IceTipArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceTipArrow.Name, "!Buff_IceTip", Settings.IceTipArrow_Key, SS.Skill_IceTipArrow.CastTime);
                }
                // FREEZING MARK RARE
                if (Settings.Rot1_FreezingMark && !SS.Debuff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Debuff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // FREEZING MARK PLAYER BUFF
                if (Settings.Rot1_FreezingMarkBuff && !SS.Debuff_FreezingMark.IsActive && !SS.Buff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Buff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // TORNADO SHOT
                if (Settings.Rot1_TornadoShot && SS.Skill_TornadoShot.CanBeUsed && !SS.ActiveTornado) {
                    return new RotationAction(SS.Skill_TornadoShot.Name, "!ActiveTornado", Settings.TornadoShot_Key, SS.Skill_TornadoShot.CastTime);
                }
                // FREEZING SALVO
                if (Settings.Rot1_FreezingSalvo && !SS.Debuff_Frozen.IsActive && SS.Skill_FreezingSalvo.CanBeUsed && SS.Buff_FreezingSalvo.Charges > 8) {
                    return new RotationAction(SS.Skill_FreezingSalvo.Name, "!Debuff_Frozen && !Debuff_FreezingMark", Settings.FreezingSalvo_Key, SS.Skill_FreezingSalvo.CastTime);
                }
                // LIGHTNING RODS
                if (Settings.Rot1_LightningRod && SS.Skill_LightningRod.CanBeUsed) {
                    // BARRAGE
                    if (!SS.Buff_Barrage.IsActive && SS.Skill_Barrage.CanBeUsed) {
                        return new RotationAction(SS.Skill_Barrage.Name, "!Buff_Barrage", Settings.Barrage_Key, SS.Skill_Barrage.CastTime);
                    }
                    // BARRAGED LIGHTNING ROD
                    if (SS.Buff_Barrage.IsActive && SS.ActiveRodCount < Settings.Rot1_BarragedRodRequiredCount) {
                        return new RotationAction(SS.Skill_LightningRod.Name, "Deploy Barrage Rods", Settings.LightningRod_Key, SS.Skill_LightningRod.CastTime);
                    }
                    // LIGHTNING ROD
                    if (SS.ActiveRodCount < Settings.Rot1_RodRequiredCount) {
                        return new RotationAction(SS.Skill_LightningRod.Name, "Deploy Normal Rods", Settings.LightningRod_Key, SS.Skill_LightningRod.CastTime);
                    }
                }
                // LIGHTNING ARROW
                if (SS.Skill_LightningArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_LightningArrow.Name, "filler", Settings.LightningArrow_Key, SS.Skill_LightningArrow.CastTime);
                }
                // ICE SHOT
                if (SS.Skill_IceShot.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceShot.Name, "filler", Settings.Iceshot_Key, SS.Skill_IceShot.CastTime);
                }
            }
            // Normal or Magic Monster Targgeted
            else {
                // ICE TIPPED ARROWS
                if (Settings.Rot1_use_IceTipArrow && !SS.Buff_IceTip.IsActive && SS.Skill_IceTipArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceTipArrow.Name, "!Buff_IceTip", Settings.IceTipArrow_Key, SS.Skill_IceTipArrow.CastTime);
                }
                // FREEZING MARK
                if (Settings.Rot1_use_FreezingMark && SS.MonsterTargetted && !SS.Debuff_FreezingMark.IsActive && !SS.Buff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Debuff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // LIGHTNING ARROW
                if (SS.Skill_LightningArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_LightningArrow.Name, "filler", Settings.LightningArrow_Key, 200);
                }
                // ICE SHOT
                if (SS.Skill_IceShot.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceShot.Name, "filler", Settings.Iceshot_Key, 200);
                }
            }
        }
        // ROTATION 2
        if (rot_2) {
            // Unique or Rare Targetted
            if (SS.MonsterGreaterTargetted) {
                if (Settings.Rot2_BarrageSnipe) {
                    // BARRAGE 
                    if (SS.Debuff_Frozen.IsActive && !SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed && SS.Skill_Barrage.CanBeUsed) {
                        return new RotationAction(SS.Skill_Barrage.Name, "Debuff_Frozen && !Buff_Barrage", Settings.Barrage_Key, SS.Skill_Barrage.CastTime);
                    }
                    // SNIPE 
                    if (SS.Debuff_Frozen.IsActive && SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed) {
                        return new RotationAction(SS.Skill_Snipe.Name, "Debuff_Frozen && Buff_Barrage", Settings.Snipe_Key, ActionBehavior.KeyHold, 50);
                    }
                }
                // FROST BOMB
                if (Settings.Rot2_FrostBomb && !SS.Debuff_ElementalExposure.IsActive && SS.Skill_FrostBomb.CanBeUsed) {
                    return new RotationAction(SS.Skill_FrostBomb.Name, "!Debuff_ElementalExposure", Settings.FrostBomb_Key, SS.Skill_FrostBomb.CastTime);
                }
                // ICE TIPPED ARROWS
                if (Settings.Rot2_IceTipArrow && !SS.Buff_IceTip.IsActive && SS.Skill_IceTipArrow.CanBeUsed ) {
                    return new RotationAction(SS.Skill_IceTipArrow.Name, "!Buff_IceTip", Settings.IceTipArrow_Key, SS.Skill_IceTipArrow.CastTime);
                }
                // FREEZING MARK RARE
                if (Settings.Rot2_FreezingMark && !SS.Debuff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Debuff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // FREEZING MARK PLAYER BUFF
                if (Settings.Rot2_FreezingMarkBuff && !SS.Debuff_FreezingMark.IsActive && !SS.Buff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Buff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // TORNADO SHOT
                if (Settings.Rot2_TornadoShot && SS.Skill_TornadoShot.CanBeUsed && !SS.ActiveTornado) {
                    return new RotationAction(SS.Skill_TornadoShot.Name, "!ActiveTornado", Settings.TornadoShot_Key, SS.Skill_TornadoShot.CastTime);
                }
            // FREEZING SALVO
            if (Settings.Rot2_FreezingSalvo && !SS.Debuff_Frozen.IsActive && SS.Skill_FreezingSalvo.CanBeUsed && SS.Buff_FreezingSalvo.Charges > 8) {
                    return new RotationAction(SS.Skill_FreezingSalvo.Name, "!Debuff_Frozen && !Debuff_FreezingMark", Settings.FreezingSalvo_Key, SS.Skill_FreezingSalvo.CastTime);
                }
                // LIGHTNING RODS
                if (Settings.Rot2_LightningRod && SS.Skill_LightningRod.CanBeUsed) {
                    // BARRAGE
                    if (!SS.Buff_Barrage.IsActive && SS.Skill_Barrage.CanBeUsed) {
                        return new RotationAction(SS.Skill_Barrage.Name, "!Buff_Barrage", Settings.Barrage_Key, SS.Skill_Barrage.CastTime);
                    }
                    // BARRAGED LIGHTNING ROD
                    if (SS.Buff_Barrage.IsActive && SS.ActiveRodCount < Settings.Rot2_BarragedRodRequiredCount) {
                        return new RotationAction(SS.Skill_LightningRod.Name, "Deploy Barrage Rods", Settings.LightningRod_Key, SS.Skill_LightningRod.CastTime);
                    }
                    // LIGHTNING ROD
                    if (SS.ActiveRodCount < Settings.Rot2_RodRequiredCount) {
                        return new RotationAction(SS.Skill_LightningRod.Name, "Deploy Normal Rods", Settings.LightningRod_Key, SS.Skill_LightningRod.CastTime);
                    }
                }
                // LIGHTNING ARROW
                if (SS.Skill_LightningArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_LightningArrow.Name, "filler", Settings.LightningArrow_Key, 200);
                }
                // ICE SHOT
                if (SS.Skill_IceShot.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceShot.Name, "filler", Settings.Iceshot_Key, 200);
                }
            }
            // Normal or Magic Monster Targetted
            else {
                // ICE TIPPED ARROWS
                if (Settings.Rot2_use_IceTipArrow && !SS.Buff_IceTip.IsActive && SS.Skill_IceTipArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceTipArrow.Name, "!Buff_IceTip", Settings.IceTipArrow_Key, SS.Skill_IceTipArrow.CastTime);
                }
                // FREEZING MARK
                if (Settings.Rot2_use_FreezingMark && SS.MonsterTargetted && !SS.Debuff_FreezingMark.IsActive && !SS.Buff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Debuff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // LIGHTNING ARROW
                if (SS.Skill_LightningArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_LightningArrow.Name, "filler", Settings.LightningArrow_Key, 200);
                }
                // ICE SHOT
                if (SS.Skill_IceShot.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceShot.Name, "filler", Settings.Iceshot_Key, 200);
                }
            }
        }
        // ROTATION 3
        if (rot_3) {
            // Unique or Rare Targetted
            if (SS.MonsterGreaterTargetted) {
                if (Settings.Rot3_BarrageSnipe) {
                    // BARRAGE 
                    if (SS.Debuff_Frozen.IsActive && !SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed && SS.Skill_Barrage.CanBeUsed) {
                        return new RotationAction(SS.Skill_Barrage.Name, "Debuff_Frozen && !Buff_Barrage", Settings.Barrage_Key, SS.Skill_Barrage.CastTime);
                    }
                    // SNIPE 
                    if (SS.Debuff_Frozen.IsActive && SS.Buff_Barrage.IsActive && SS.Skill_Snipe.CanBeUsed) {
                        return new RotationAction(SS.Skill_Snipe.Name, "Debuff_Frozen && Buff_Barrage", Settings.Snipe_Key, ActionBehavior.KeyHold, 50);
                    }
                }
                // FROST BOMB
                if (Settings.Rot3_FrostBomb && !SS.Debuff_ElementalExposure.IsActive && SS.Skill_FrostBomb.CanBeUsed) {
                    return new RotationAction(SS.Skill_FrostBomb.Name, "!Debuff_ElementalExposure", Settings.FrostBomb_Key, SS.Skill_FrostBomb.CastTime);
                }
                // ICE TIPPED ARROWS
                if (Settings.Rot3_IceTipArrow && !SS.Buff_IceTip.IsActive && SS.Skill_IceTipArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceTipArrow.Name, "!Buff_IceTip", Settings.IceTipArrow_Key, SS.Skill_IceTipArrow.CastTime);
                }
                // FREEZING MARK RARE
                if (Settings.Rot3_FreezingMark && !SS.Debuff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Debuff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // FREEZING MARK PLAYER BUFF
                if (Settings.Rot3_FreezingMarkBuff && !SS.Debuff_FreezingMark.IsActive && !SS.Buff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Buff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // TORNADO SHOT
                if (Settings.Rot3_TornadoShot && SS.Skill_TornadoShot.CanBeUsed && !SS.ActiveTornado) {
                    return new RotationAction(SS.Skill_TornadoShot.Name, "!ActiveTornado", Settings.TornadoShot_Key, SS.Skill_TornadoShot.CastTime);
                }
                // FREEZING SALVO
                if (Settings.Rot3_FreezingSalvo && !SS.Debuff_Frozen.IsActive && SS.Skill_FreezingSalvo.CanBeUsed && SS.Buff_FreezingSalvo.Charges > 8) {
                    return new RotationAction(SS.Skill_FreezingSalvo.Name, "!Debuff_Frozen && !Debuff_FreezingMark", Settings.FreezingSalvo_Key, SS.Skill_FreezingSalvo.CastTime);
                }
                // LIGHTNING RODS
                if (Settings.Rot3_LightningRod && SS.Skill_LightningRod.CanBeUsed) {
                    // BARRAGE
                    if (!SS.Buff_Barrage.IsActive && SS.Skill_Barrage.CanBeUsed) {
                        return new RotationAction(SS.Skill_Barrage.Name, "!Buff_Barrage", Settings.Barrage_Key, SS.Skill_Barrage.CastTime);
                    }
                    // BARRAGED LIGHTNING ROD
                    if (SS.Buff_Barrage.IsActive && SS.ActiveRodCount < Settings.Rot3_BarragedRodRequiredCount) {
                        return new RotationAction(SS.Skill_LightningRod.Name, "Deploy Barrage Rods", Settings.LightningRod_Key, SS.Skill_LightningRod.CastTime);
                    }
                    // LIGHTNING ROD
                    if (SS.ActiveRodCount < Settings.Rot3_RodRequiredCount) {
                        return new RotationAction(SS.Skill_LightningRod.Name, "Deploy Normal Rods", Settings.LightningRod_Key, SS.Skill_LightningRod.CastTime);
                    }
                }
                // LIGHTNING ARROW
                if (SS.Skill_LightningArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_LightningArrow.Name, "filler", Settings.LightningArrow_Key, 200);
                }
                // ICE SHOT
                if (SS.Skill_IceShot.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceShot.Name, "filler", Settings.Iceshot_Key, 200);
                }
            }
            // Normal or Magic Monster Targetted
            else {
                // ICE TIPPED ARROWS
                if (Settings.Rot3_use_IceTipArrow && !SS.Buff_IceTip.IsActive && SS.Skill_IceTipArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceTipArrow.Name, "!Buff_IceTip", Settings.IceTipArrow_Key, SS.Skill_IceTipArrow.CastTime);
                }
                // FREEZING MARK
                if (Settings.Rot3_use_FreezingMark && SS.MonsterTargetted && !SS.Debuff_FreezingMark.IsActive && !SS.Buff_FreezingMark.IsActive && SS.Skill_FreezingMark.CanBeUsed) {
                    return new RotationAction(SS.Skill_FreezingMark.Name, "!Debuff_FreezingMark", Settings.FreezingMark_Key, SS.Skill_FreezingMark.CastTime);
                }
                // LIGHTNING ARROW
                if (SS.Skill_LightningArrow.CanBeUsed) {
                    return new RotationAction(SS.Skill_LightningArrow.Name, "filler", Settings.LightningArrow_Key, 200);
                }
                // ICE SHOT
                if (SS.Skill_IceShot.CanBeUsed) {
                    return new RotationAction(SS.Skill_IceShot.Name, "filler", Settings.Iceshot_Key, 200);
                }
            }
        }
        return null;
    }


    public void Render(GameController gameController, ExileCore2.Graphics graphics) {
        var player = gameController?.Player;
        if (player == null) return;


        if (Settings.TargetCursorRadiusTest) {
            var cursorGridPos = gameController?.IngameState?.ServerData?.WorldMousePosition;
            if (cursorGridPos == null) return;

            var cursorWorldPos = new Vector3(cursorGridPos.Value.X, cursorGridPos.Value.Y, player.Pos.Z);
            graphics.DrawCircleInWorld(cursorWorldPos, Settings.TargetCursorRadius * 11f, Color.Yellow);
        }
        if (Settings.RodCursorRadiusTest) {
            var cursorGridPos = gameController?.IngameState?.ServerData?.WorldMousePosition;
            if (cursorGridPos == null) return;

            var cursorWorldPos = new Vector3(cursorGridPos.Value.X, cursorGridPos.Value.Y, player.Pos.Z);
            graphics.DrawCircleInWorld(cursorWorldPos, Settings.RodCursorRadius * 11f, Color.Blue);
        }

    }


    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);


    public void DrawHotkey(string name, ref Keys key) {
        DXT.Button.Draw("##" + name, new DXT.Button.Options {
            Label = $"{key.ToString()} ",
            Width = 100,
            Height = 22,
        });
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Hover and press ANY key to bind it instantly!");

            var io = ImGui.GetIO();
            for (int i = 1; i < 255; i++) {
                if ((GetAsyncKeyState(i) & 0x8000) != 0) {
                    if ((i == 1 || i == 2) && !Settings.BindMouseButtons) continue;
                    key = (System.Windows.Forms.Keys)i;
                    break;
                }
            }
        }
        ImGui.SameLine();
        ImGui.Text($"{name}");
    }
    
    private void DrawSkillCheckbox(string id, ref bool value, string label, string description) {
        ImGui.Checkbox(id, ref value);
        ImGui.SameLine();
        ImGui.TextColored(DXTC.Colors.TextYellow.ToImguiVec4(), label);
        ImGui.SameLine();
        ImGui.TextColored(DXTC.Colors.Text.ToImguiVec4(), description);
    }
    public void DrawSettings() {
        if (DXT.CollapsingHeader("Rotation Settings", ref Settings.RotationSettingsOpen)) {
            ImGui.Indent();
            ImGui.PushItemWidth(100);

            ImGui.Checkbox("Test Snipe", ref Settings.SnipeTest);

            ImGui.SliderInt("##TargetCursorRadius", ref Settings.TargetCursorRadius, 10, 50);
            ImGui.SameLine();
            ImGui.Checkbox("##TargetCursorRadiusTest", ref Settings.TargetCursorRadiusTest);
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.Text("Draw a circle around the cursor showing the monster targeting radius.");
                ImGui.EndTooltip();
            }
            ImGui.SameLine();
            ImGui.Text("Monster Search Radius around cursor");

            ImGui.SliderInt("##RodCursorRadius", ref Settings.RodCursorRadius, 10, 50);
            ImGui.SameLine();
            ImGui.Checkbox("##RodCursorRadiusTest", ref Settings.RodCursorRadiusTest);
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.Text("Draws a circle around the cursor showing the radius searched for active lightning rods.");
                ImGui.EndTooltip();
            }
            ImGui.SameLine();
            ImGui.Text("Rod Search Radius around cursor");

            ImGui.PopItemWidth();

            ImGui.Unindent();
        }

        if (DXT.CollapsingHeader("Rotation Hotkeys", ref Settings.HotkeySettingsOpen)) {
            ImGui.Indent();

            ImGui.Checkbox("Allow Mouse Button Binding", ref Settings.BindMouseButtons);

            DrawHotkey("Rotation 1", ref Settings.Rotation1_Key);
            DrawHotkey("Rotation 2", ref Settings.Rotation2_Key);
            DrawHotkey("Rotation 3", ref Settings.Rotation3_Key);
            ImGui.Separator();
            DrawHotkey("Snipe", ref Settings.Snipe_Key);
            DrawHotkey("Barrage", ref Settings.Barrage_Key);
            DrawHotkey("Frost Bomb", ref Settings.FrostBomb_Key);
            DrawHotkey("Ice Tipped Arrows", ref Settings.IceTipArrow_Key);
            DrawHotkey("Freezing Mark", ref Settings.FreezingMark_Key);
            DrawHotkey("Freezing Salvo", ref Settings.FreezingSalvo_Key);
            DrawHotkey("Tornado Shot", ref Settings.TornadoShot_Key);
            DrawHotkey("Lightning Rod", ref Settings.LightningRod_Key);
            DrawHotkey("Lightning Arrow", ref Settings.LightningArrow_Key);
            DrawHotkey("Iceshot", ref Settings.Iceshot_Key);

            ImGui.Unindent();
        }

        if (DXT.CollapsingHeader("Rotation 1 Settings", ref Settings.Rotation1SettingsOpen)) {
            ImGui.Indent();

            DrawSkillCheckbox("##Rot1_BarrageSnipe", ref Settings.Rot1_BarrageSnipe, "Barrage + Snipe:", "Use Barrage to set up a Snipe when a Rare or Unique target is frozen.");
            DrawSkillCheckbox("##Rot1_FrostBomb", ref Settings.Rot1_FrostBomb, "Frost Bomb:", "Use Frost Bomb when a Rare or Unique target has no Elemental Exposure.");
            DrawSkillCheckbox("##Rot1_IceTipArrow", ref Settings.Rot1_IceTipArrow, "Ice Tipped Arrows:", "Use Ice Tipped Arrows if the buff is not active.");
            DrawSkillCheckbox("##Rot1_FreezingMark", ref Settings.Rot1_FreezingMark, "Freezing Mark:", "Apply Freezing Mark when a Rare or Unique target does not have the debuff.");
            DrawSkillCheckbox("##Rot1_FreezingMarkBuff", ref Settings.Rot1_FreezingMarkBuff, "Freezing Mark Buff:", "Use Freezing Mark when the player buff is not active.");
            DrawSkillCheckbox("##Rot1_TornadoShot", ref Settings.Rot1_TornadoShot, "Tornado Shot:", "Use Tornado Shot when a Rare or Unique target is near the cursor.");
            DrawSkillCheckbox("##Rot1_FreezingSalvo", ref Settings.Rot1_FreezingSalvo, "Freezing Salvo:", "Use Freezing Salvo when a Rare or Unique target is not frozen and salvo has more than 8 seals.");
            DrawSkillCheckbox("##Rot1_LightningRod", ref Settings.Rot1_LightningRod, "Lightning Rod:", "Deploy Lightning Rods, using Barrage to empower them when available.");
            ImGui.PushItemWidth(100);
            ImGui.SliderInt("##Rot1_RodRequiredCount", ref Settings.Rot1_RodRequiredCount, 1, 10);
            ImGui.SameLine();
            ImGui.Text("Rod Count");
            ImGui.SliderInt("##Rot1_BarragedRodRequiredCount", ref Settings.Rot1_BarragedRodRequiredCount, 1, 10);
            ImGui.SameLine();
            ImGui.Text("Barraged Rod Count");
            ImGui.PopItemWidth();

            ImGui.Separator();

            DrawSkillCheckbox("##Rot1_LesserMonster_IceTipArrow", ref Settings.Rot1_use_IceTipArrow, "Ice Tipped Arrows:", "Use Ice Tipped Arrows when the buff is not active regardless of target.");
            DrawSkillCheckbox("##Rot1_use_FreezingMark", ref Settings.Rot1_use_FreezingMark, "Freezing Mark:", "Apply Freezing Mark to any targeted monster when the player buff is not active.");
            ImGui.Unindent();

        }

        if (DXT.CollapsingHeader("Rotation 2 Settings", ref Settings.Rotation2SettingsOpen)) {
            ImGui.Indent();

            DrawSkillCheckbox("##Rot2_BarrageSnipe", ref Settings.Rot2_BarrageSnipe, "Barrage + Snipe:", "Use Barrage to set up a Snipe when a Rare or Unique target is frozen.");
            DrawSkillCheckbox("##Rot2_FrostBomb", ref Settings.Rot2_FrostBomb, "Frost Bomb:", "Use Frost Bomb when a Rare or Unique target has no Elemental Exposure.");
            DrawSkillCheckbox("##Rot2_IceTipArrow", ref Settings.Rot2_IceTipArrow, "Ice Tipped Arrows:", "Use Ice Tipped Arrows if the buff is not active.");
            DrawSkillCheckbox("##Rot2_FreezingMark", ref Settings.Rot2_FreezingMark, "Freezing Mark:", "Apply Freezing Mark when a Rare or Unique target does not have the debuff.");
            DrawSkillCheckbox("##Rot2_FreezingMarkBuff", ref Settings.Rot2_FreezingMarkBuff, "Freezing Mark Buff:", "Use Freezing Mark when the player buff is not active.");
            DrawSkillCheckbox("##Rot2_TornadoShot", ref Settings.Rot2_TornadoShot, "Tornado Shot:", "Use Tornado Shot when a Rare or Unique target is near the cursor.");
            DrawSkillCheckbox("##Rot2_FreezingSalvo", ref Settings.Rot2_FreezingSalvo, "Freezing Salvo:", "Use Freezing Salvo when a Rare or Unique target is not frozen and salvo has more than 8 seals.");
            DrawSkillCheckbox("##Rot2_LightningRod", ref Settings.Rot2_LightningRod, "Lightning Rod:", "Deploy Lightning Rods, using Barrage to empower them when available.");
            ImGui.PushItemWidth(100);
            ImGui.SliderInt("##Rot2_RodRequiredCount", ref Settings.Rot2_RodRequiredCount, 1, 10);
            ImGui.SameLine();
            ImGui.Text("Rod Count");
            ImGui.SliderInt("##Rot2_BarragedRodRequiredCount", ref Settings.Rot2_BarragedRodRequiredCount, 1, 10);
            ImGui.SameLine();
            ImGui.Text("Barraged Rod Count");
            ImGui.PopItemWidth();

            ImGui.Separator();

            DrawSkillCheckbox("##Rot2_LesserMonster_IceTipArrow", ref Settings.Rot2_use_IceTipArrow, "Ice Tipped Arrows:", "Use Ice Tipped Arrows when the buff is not active regardless of target.");
            DrawSkillCheckbox("##Rot2_use_FreezingMark", ref Settings.Rot2_use_FreezingMark, "Freezing Mark:", "Apply Freezing Mark to any targeted monster when the player buff is not active.");
            ImGui.Unindent();
        }

        if (DXT.CollapsingHeader("Rotation 3 Settings", ref Settings.Rotation3SettingsOpen)) {
            ImGui.Indent();

            DrawSkillCheckbox("##Rot3_BarrageSnipe", ref Settings.Rot3_BarrageSnipe, "Barrage + Snipe:", "Use Barrage to set up a Snipe when a Rare or Unique target is frozen.");
            DrawSkillCheckbox("##Rot3_FrostBomb", ref Settings.Rot3_FrostBomb, "Frost Bomb:", "Use Frost Bomb when a Rare or Unique target has no Elemental Exposure.");
            DrawSkillCheckbox("##Rot3_IceTipArrow", ref Settings.Rot3_IceTipArrow, "Ice Tipped Arrows:", "Use Ice Tipped Arrows if the buff is not active.");
            DrawSkillCheckbox("##Rot3_FreezingMark", ref Settings.Rot3_FreezingMark, "Freezing Mark:", "Apply Freezing Mark when a Rare or Unique target does not have the debuff.");
            DrawSkillCheckbox("##Rot3_FreezingMarkBuff", ref Settings.Rot3_FreezingMarkBuff, "Freezing Mark Buff:", "Use Freezing Mark when the player buff is not active.");
            DrawSkillCheckbox("##Rot3_TornadoShot", ref Settings.Rot3_TornadoShot, "Tornado Shot:", "Use Tornado Shot when a Rare or Unique target is near the cursor.");
            DrawSkillCheckbox("##Rot3_FreezingSalvo", ref Settings.Rot3_FreezingSalvo, "Freezing Salvo:", "Use Freezing Salvo when a Rare or Unique target is not frozen and salvo has more than 8 seals.");
            DrawSkillCheckbox("##Rot3_LightningRod", ref Settings.Rot3_LightningRod, "Lightning Rod:", "Deploy Lightning Rods, using Barrage to empower them when available.");
            ImGui.PushItemWidth(100);
            ImGui.SliderInt("##Rot3_RodRequiredCount", ref Settings.Rot3_RodRequiredCount, 1, 10);
            ImGui.SameLine();
            ImGui.Text("Rod Count");
            ImGui.SliderInt("##Rot3_BarragedRodRequiredCount", ref Settings.Rot3_BarragedRodRequiredCount, 1, 10);
            ImGui.SameLine();
            ImGui.Text("Barraged Rod Count");
            ImGui.PopItemWidth();

            ImGui.Separator();

            DrawSkillCheckbox("##Rot3_LesserMonster_IceTipArrow", ref Settings.Rot3_use_IceTipArrow, "Ice Tipped Arrows:", "Use Ice Tipped Arrows when the buff is not active regardless of target.");
            DrawSkillCheckbox("##Rot3_use_FreezingMark", ref Settings.Rot3_use_FreezingMark, "Freezing Mark:", "Apply Freezing Mark to any targeted monster when the player buff is not active.");
            ImGui.Unindent();
        }
    }







}
