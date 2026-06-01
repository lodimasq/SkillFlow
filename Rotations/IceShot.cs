using DieselExileTools.ExileCore2;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using ImGuiNET;
using System.Diagnostics.Eventing.Reader;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SkillFlow;

public class IceShotSettings {
    public bool Enable = true;
    public bool HotkeySettingsOpen = true;
    public bool RotationSettingsOpen = true;
    public bool RotationDocsOpen = true;
    public bool SnipeTest = false;

    public Keys Rotation1Key = Keys.F13;
    public Keys Rotation2Key = Keys.F14;
    public Keys Rotation3Key = Keys.F15;

    public Keys BarrageKey = Keys.Oemcomma;
    public Keys SnipeKey = Keys.B;
    public Keys IceshotKey = Keys.V;

    public Keys FrostBombKey = Keys.N;
    public Keys LightningRodKey = Keys.E;
    public Keys LightningArrowKey = Keys.V;
    public Keys IceTipArrowKey = Keys.M;
    public Keys FreezingSalvoKey = Keys.T;
    public Keys FreezingMarkKey = Keys.F;
    
    public bool Rotation2_FrostBomb = true;

    public bool Rotation2_Rods = true;
    public int Rotation2_RodRequiredCount = 2;
    public bool Rotation2_Salvo = true;

    public int RodCursorRadius = 20;
    public int CursorTargetRadius = 20;

    public int RodRequiredCount = 2;
    public int BarrageRodRequiredCount = 4;
}


public class IceShotRotation : IRotation {
    public string Name => "Bows RotA Iceshot+Snipe,LA+Rod";
    private IceShotSettings Settings { get; set; }
    private Settings PluginSettings { get; set; }
    public IceShotRotation(Settings settings) {
        PluginSettings = settings;
        //Settings = settings.IceShotIceShotRotation;
    }

    private class Skill {
        public string Name { get; set; } = "";
        public bool CanBeUsed { get; set; } = false;
        public int CastTime = 0;
        public Skill(string name) { Name = name; }
        public void Reset() {
            CanBeUsed = false;
            CastTime = 0;
        }
    }

    // ROTATION KEYS
    private Keys rotationKey = Keys.F14;
    // PLAYER BUFFS
    private bool buff_barrage = false;
    private bool buff_icetip = false;
    private bool buff_freezingMark = false;
    private int salvoSeals = 0;
    // Player SKILLS
    private Skill lightningRod = new("Lightning Rod");
    private Skill barrage = new ("Barrage");
    private Skill snipe = new ("Snipe");
    private Skill iceShot = new ("Ice Shot");
    private Skill frostBomb = new ("Frost Bomb");
    private Skill iceTipArrow = new ("Ice Tipped Arrows");  
    private Skill freezingMark = new ("Freezing Mark");
    private Skill freezingSalvo = new ("Freezing Salvo");
    private Skill lightningArrow = new ("Lightning Arrow");

    // Monster BUFFS
    private bool debuff_eleExposure = false;
    private bool debuff_frozen = false;
    private bool debuff_freezingMark = false;
    // vars
    private bool monsterTargetted = false;
    private bool monsterGreaterTargetted = false; // Rare or Unique
    private int activeRodCount = 0;
    private void ResetSnapshot() {
        buff_barrage = false;
        buff_icetip = false;
        buff_freezingMark = false;
        salvoSeals = 0;

        activeRodCount = 0;

        debuff_eleExposure = false;
        debuff_frozen = false;
        debuff_freezingMark = false;

        monsterTargetted = false;
        monsterGreaterTargetted = false;

        lightningRod.Reset();
        barrage.Reset();
        snipe.Reset();
        iceShot.Reset();
        frostBomb.Reset();
        iceTipArrow.Reset();
        freezingMark.Reset();
        freezingSalvo.Reset();
        lightningArrow.Reset();
    }
    // Snapshots
    private void snapshotPlayerSkills(Actor player_actor) {
        foreach (var skill in player_actor.ActorSkills) {
            string name = skill.Name;

            if (skill.WeaponSetBinding == 0) {
                switch (name) {
                    case "BarragePlayer":
                        barrage.CanBeUsed = skill.CanBeUsed;
                        barrage.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        break;
                    case "SnipePlayer":
                        snipe.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        snipe.CanBeUsed = skill.CanBeUsed;
                        break;
                    case "IceShotPlayer":
                        iceShot.CanBeUsed = skill.CanBeUsed;
                        iceShot.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        break;
                    case "LightningRodPlayer":
                        lightningRod.CanBeUsed = skill.CanBeUsed;
                        lightningRod.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        break;
                    case "FrostBombPlayer":
                        frostBomb.CanBeUsed = skill.CanBeUsed;
                        frostBomb.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        break;
                    case "IceTippedArrowsPlayer":
                        iceTipArrow.CanBeUsed = skill.CanBeUsed;
                        iceTipArrow.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        break;
                    case "FreezingSalvoPlayer":
                        freezingSalvo.CanBeUsed = skill.CanBeUsed;
                        freezingSalvo.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        break;
                    case "LightningArrowPlayer":
                        lightningArrow.CanBeUsed = skill.CanBeUsed;
                        lightningArrow.CastTime = (int)skill.CastTime.TotalMilliseconds;
                        break;
                }
            }
            if (name == "FreezingMarkPlayer") {
                freezingMark.CanBeUsed = skill.CanBeUsed;
                freezingMark.CastTime = (int)skill.CastTime.TotalMilliseconds;
            }
        }
    }
    private void snapshotPlayerBuffs(Entity player) {
        foreach (var buff in player.Buffs) {
            switch (buff.Name) {
                case "empower_barrage_visual": buff_barrage = true; break;
                case "shearing_bolts": buff_icetip = true; break;
                case "freezing_mark_damage_buff": buff_freezingMark = true; break;
                case "freezing_salvo_seals": salvoSeals = buff.BuffCharges; break;
            }
        }
    }
    private void snapshotEntities(ICollection<Entity> entities, Vector2 cursorGridPos) {
        foreach (var entity in entities) {
            if (entity == null || !entity.IsValid) continue;

            if (entity.Type == EntityType.Monster) {
                if (entity.IsDead || entity.IsHidden || !entity.IsHostile) continue;

                float distanceToCursor = Vector2.Distance(entity.GridPos, cursorGridPos);
                if (distanceToCursor > 50) continue;

                bool thisTargetted = distanceToCursor < Settings.CursorTargetRadius;
                bool thisGreaterTargetted = thisTargetted && entity.Rarity >= MonsterRarity.Rare;

                if (!thisGreaterTargetted && entity.Rarity == MonsterRarity.Unique && entity.RenderName == "Zalmarath, the Colossus") thisGreaterTargetted = true;
                if (thisTargetted) monsterTargetted = true;
                if (thisGreaterTargetted) monsterGreaterTargetted = true;

                foreach (var buff in entity.Buffs) {
                    if (thisGreaterTargetted) {
                        if (buff.Name == "exposure_elemental") debuff_eleExposure = true;
                        if (buff.Name == "frozen") debuff_frozen = true;
                    }
                    if (buff.Name == "freezing_mark") debuff_freezingMark = true;
                }
                continue;
            }
            if (lightningRod.CanBeUsed && entity.Type == EntityType.MiscellaneousObjects) {
                if (entity.Path.Contains("LightningRod")) {
                    if (Vector2.Distance(entity.GridPos, cursorGridPos) <= Settings.RodCursorRadius) {
                        activeRodCount++;
                    }
                }
            }
        }
    }

    public void UpdateTelemetry(GameController gameController) {
        //if (player == null) return;
    }

    public void MonitorRotation() {
        if (!PluginSettings.DXT.DBug.ShowMonitor) return;


        DBug.Monitor("Skills", "Snipe", snipe.CanBeUsed);
        DBug.Monitor("Skills", "Snipe Cast Time:", snipe.CastTime);

        DBug.Monitor("Skills", "Barrage", barrage.CanBeUsed);
        DBug.Monitor("Skills", "Barrage Cast Time:", barrage.CastTime);
        DBug.Monitor("Skills", "IceShot", iceShot.CanBeUsed);
        DBug.Monitor("Skills", "IceShot Cast Time:", iceShot.CastTime);
        DBug.Monitor("Skills", "LightningRod", lightningRod.CanBeUsed);
        DBug.Monitor("Skills", "LightningRod Cast Time:", lightningRod.CastTime);
        DBug.Monitor("Skills", "FrostBomb", frostBomb.CanBeUsed);
        DBug.Monitor("Skills", "FrostBomb Cast Time:", frostBomb.CastTime);
        DBug.Monitor("Skills", "IceTippedArrows", iceTipArrow.CanBeUsed);
        DBug.Monitor("Skills", "IceTippedArrows Cast Time:", iceTipArrow.CastTime);
        DBug.Monitor("Skills", "FreezingMark", freezingMark.CanBeUsed);
        DBug.Monitor("Skills", "FreezingMark Cast Time:", freezingMark.CastTime);
        DBug.Monitor("Skills", "FreezingSalvo", freezingSalvo.CanBeUsed);
        DBug.Monitor("Skills", "FreezingSalvo Cast Time:", freezingSalvo.CastTime);
        DBug.Monitor("Skills", "LightningArrow", lightningArrow.CanBeUsed);
        DBug.Monitor("Skills", "LightningArrow Cast Time:", lightningArrow.CastTime);

        DBug.Monitor("Buffs", "Barrage", buff_barrage);
        DBug.Monitor("Buffs", "IceTip", buff_icetip);
        DBug.Monitor("Buffs", "FreezingMark", buff_freezingMark);

        DBug.Monitor("Debuffs", "EleExposure", debuff_eleExposure);
        DBug.Monitor("Debuffs", "Frozen", debuff_frozen);
        DBug.Monitor("Debuffs", "FreezingMark", debuff_freezingMark);

        DBug.Monitor("Target", "Targetted", monsterTargetted);
        DBug.Monitor("Target", "GreaterTargetted", monsterGreaterTargetted);
        DBug.Monitor("Target", "ActiveRodCount", activeRodCount);
        DBug.Monitor("Target", "SalvoSeals", salvoSeals);
    }

    public RotationAction GetNextAction(GameController gameController) {
        if (gameController == null) return null;
        var player = gameController.Player;

        if (!player.TryGetComponent<Actor>(out var player_actor)) return null;

        // SNIPE CHECKING
        if (player_actor.Animation == AnimationE.SnipeChannel) {
            // SNIPE Release
            if (player_actor.AnimationController.CurrentAnimationStage > 20) return new RotationAction(snipe.Name, "Release Snipe", Settings.SnipeKey, ActionBehavior.KeyRelease, 200);
            // Snipe Hold
            return new RotationAction(snipe.Name, "Holding Snipe", Settings.SnipeKey, ActionBehavior.KeyHold, 20);
        }
        // RUNNING 
        bool rot_1 = Input.IsKeyDown(Settings.Rotation1Key); // snipes rare & unique monsters
        bool rot_2 = Input.IsKeyDown(Settings.Rotation2Key); // no snipe for quick weaker monster dispatch
        bool rot_3 = Input.IsKeyDown(Settings.Rotation3Key); // base attack
        if (!rot_1 && !rot_2 && !rot_3) { return null; }

        // bail if finishing a snipe
        if (player_actor.Animation == AnimationE.SnipeChannelEnd || player_actor.Animation == AnimationE.SnipeChannelEndPerfect) return null;

        // SNAPSHOTTING
        var entities = gameController.Entities;
        var cursorGridPos = gameController.IngameState.ServerData.WorldMousePosition.WorldToGrid();
        ResetSnapshot();
        snapshotPlayerSkills(player_actor);
        snapshotPlayerBuffs(player);
        snapshotEntities(entities, cursorGridPos);

        MonitorRotation();
        // SNIPE TEST
        if (Settings.SnipeTest) {
            // BARRAGE 
            if (!buff_barrage && snipe.CanBeUsed && barrage.CanBeUsed) {
                return new RotationAction(barrage.Name, "SnipeTest: [Barrage] !buff_barrage", Settings.BarrageKey, barrage.CastTime);
            }
            // SNIPE
            if (buff_barrage && snipe.CanBeUsed) {
                return new RotationAction(snipe.Name, $"SnipeTest: [Snipe] buff_barrage, cast time: {snipe.CastTime}", Settings.SnipeKey, ActionBehavior.KeyHold, 50);
            }
            if (!barrage.CanBeUsed && snipe.CanBeUsed) {
                return new RotationAction(snipe.Name, $"SnipeTest: [Snipe] cast time: {snipe.CastTime}", Settings.SnipeKey, ActionBehavior.KeyHold, 50);
            }
            return null;
        }

        //~~~~~| ROTATION |~~~~~~
        if (monsterGreaterTargetted) {
            if (rot_1) {
                // BARRAGE 
                if (debuff_frozen && !buff_barrage && snipe.CanBeUsed && barrage.CanBeUsed) {
                    return new RotationAction(barrage.Name, "debuff_frozen && !buff_barrage", Settings.BarrageKey, barrage.CastTime);
                }
                // SNIPE
                if (debuff_frozen && buff_barrage && snipe.CanBeUsed) {
                    return new RotationAction(snipe.Name, "debuff_frozen && buff_barrage", Settings.SnipeKey, ActionBehavior.KeyHold, 50);
                }
            }
            // FROST BOMB
            if ((rot_1 || (rot_2 && Settings.Rotation2_FrostBomb)) && !debuff_eleExposure && frostBomb.CanBeUsed) {
                return new RotationAction(frostBomb.Name, "!debuff_eleExposure", Settings.FrostBombKey, frostBomb.CastTime);
            }
            // ICE TIPPED ARROWS
            if (!buff_icetip && iceTipArrow.CanBeUsed) {
                return new RotationAction(iceTipArrow.Name, "!buff_icetip", Settings.IceTipArrowKey, iceTipArrow.CastTime);
            }
            // FREEZING MARK
            if ((rot_1 || !buff_freezingMark) && !debuff_freezingMark && freezingMark.CanBeUsed) {
                return new RotationAction(freezingMark.Name, "!debuff_freezingMark", Settings.FreezingMarkKey, freezingMark.CastTime);
            }
            // FREEZING SALVO
            if ((rot_1 || (rot_2 && Settings.Rotation2_Salvo)) && !debuff_frozen && salvoSeals > 8 && freezingSalvo.CanBeUsed) {
                return new RotationAction(freezingSalvo.Name, "salvo burst", Settings.FreezingSalvoKey, freezingSalvo.CastTime);
            }
            // LIGHTNING RODS
            if ((rot_1 || (rot_2 && Settings.Rotation2_Rods)) && lightningRod.CanBeUsed) {
                int reqRods = rot_2 ? Settings.Rotation2_RodRequiredCount : Settings.RodRequiredCount;
                // BARRAGE
                if (!buff_barrage && activeRodCount < Settings.BarrageRodRequiredCount && barrage.CanBeUsed) {
                    return new RotationAction(barrage.Name, "Barrage for Rod setup", Settings.BarrageKey, barrage.CastTime);
                }
                // LIGHTNING ROD
                if (activeRodCount < reqRods) {
                    return new RotationAction(lightningRod.Name, "Deploy Normal Rods", Settings.LightningRodKey, lightningRod.CastTime);
                }
                // BARRAGED LIGHTNING ROD
                if (buff_barrage && activeRodCount < Settings.BarrageRodRequiredCount) {
                    return new RotationAction(lightningRod.Name, "Deploy Barrage Rods", Settings.LightningRodKey, lightningRod.CastTime);
                }
            }
            // LIGHTNING ARROW
            if (lightningArrow.CanBeUsed) {
                return new RotationAction(lightningArrow.Name, "filler", Settings.LightningArrowKey, lightningArrow.CastTime);
            }
            // ICE SHOT
            if (iceShot.CanBeUsed) {
                return new RotationAction(iceShot.Name, "filler", Settings.IceshotKey, iceShot.CastTime);
            }
        }
        else {
            // ICE TIPPED ARROWS
            if (!rot_3 && monsterTargetted && !buff_icetip && iceTipArrow.CanBeUsed) { 
                return new RotationAction(iceTipArrow.Name, "!buff_icetip", Settings.IceTipArrowKey, iceTipArrow.CastTime);
            }
            // FREEZING MARK
            if (!rot_3 && monsterTargetted && !debuff_freezingMark && freezingMark.CanBeUsed && !buff_freezingMark) {
                return new RotationAction(freezingMark.Name, "!buff_freezingMark", Settings.FreezingMarkKey, freezingMark.CastTime);
            }
            // LIGHTNING ARROW
            if (lightningArrow.CanBeUsed) {
                return new RotationAction(lightningArrow.Name, "clear", Settings.LightningArrowKey, lightningArrow.CastTime);
            }
            // ICE SHOT
            if (iceShot.CanBeUsed) {
                return new RotationAction(iceShot.Name, "clear", Settings.IceshotKey, iceShot.CastTime);
            }
        }


        return null;
    }


    public void Render(GameController gameController, ExileCore2.Graphics graphics) {

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

            // Check ImGui's IO layer for any active key press this frame
            var io = ImGui.GetIO();
            for (int i = 1; i < 255; i++) {
                // Check the high bit: if it's set, the key was pressed this frame
                if ((GetAsyncKeyState(i) & 0x8000) != 0) {
                    // Exclude left click (1) and right click (2) so you don't accidentally bind them
                    if (i == 1 || i == 2) continue;

                    // Directly assign the Windows Virtual Key to your Forms Keys property
                    key = (System.Windows.Forms.Keys)i;
                    break;
                }
            }
        }
        ImGui.SameLine();
        ImGui.Text($"{name}");
    }

    public void DrawSettings() {

        if (DXT.CollapsingHeader("Rotation Hotkeys", ref Settings.HotkeySettingsOpen)) {
            ImGui.Indent();

            DrawHotkey("Rotation 1", ref Settings.Rotation1Key);
            DrawHotkey("Rotation 2", ref Settings.Rotation2Key);
            DrawHotkey("Rotation 3", ref Settings.Rotation3Key);

            DrawHotkey("Snipe", ref Settings.SnipeKey);
            DrawHotkey("Barrage", ref Settings.BarrageKey);
            DrawHotkey("Frost Bomb", ref Settings.FrostBombKey);
            DrawHotkey("Ice Tipped Arrows", ref Settings.IceTipArrowKey);
            DrawHotkey("Freezing Mark", ref Settings.FreezingMarkKey);
            DrawHotkey("Freezing Salvo", ref Settings.FreezingSalvoKey);
            DrawHotkey("Lightning Rod", ref Settings.LightningRodKey);
            DrawHotkey("Lightning Arrow", ref Settings.LightningArrowKey);
            DrawHotkey("Iceshot", ref Settings.IceshotKey);

            ImGui.Unindent();
        }

        if (DXT.CollapsingHeader("Rotation Settings", ref Settings.RotationSettingsOpen)) {
            ImGui.Indent();
            ImGui.PushItemWidth(100);

            ImGui.Checkbox("Test Snipe", ref Settings.SnipeTest);            

            ImGui.SliderInt("Monster Distance From cursor search Radius", ref Settings.CursorTargetRadius, 10, 50);

            ImGui.SliderInt("Lightning Rod Distance from cursor search radius", ref Settings.RodCursorRadius, 10, 50);

            ImGui.SliderInt("Lightning Rods to deploy when rare/unique in search radius", ref Settings.RodRequiredCount, 1, 8);

            ImGui.SliderInt("Barraged Lightning Rods to deploy when rare/unique in search radius", ref Settings.BarrageRodRequiredCount, 1, 8);

            ImGui.Checkbox("Rotation 2: Forst Bomb", ref Settings.Rotation2_FrostBomb);

            ImGui.Checkbox("Rotation 2: Lightning Rods", ref Settings.Rotation2_Rods);

            ImGui.SliderInt("Rotation 2: Lightning Rods to deploy when rare/unique in search radius", ref Settings.Rotation2_RodRequiredCount, 1, 8);

            ImGui.Checkbox("Rotation 2: Freezing Salvo", ref Settings.Rotation2_Salvo);

            ImGui.PopItemWidth();

            ImGui.Unindent();
        }

        if (DXT.CollapsingHeader("Rotation Help", ref Settings.RotationDocsOpen)) {
            ImGui.Indent();

            ImGui.TextWrapped("This profile dynamically switches behavior based on which key you hold down:");
            ImGui.Separator();

            // --- ROTATION 1 ---
            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Rotation 1 (Boss & Rare Burst)");
            ImGui.TextWrapped("Best for Map Bosses and tough Rare monsters. Focuses on full debuff setup into a conditional Snipe payload.");
            ImGui.BulletText("Drops Frost Bomb for Elemental Exposure.");
            ImGui.BulletText("Maintains Ice Tipped Arrows and Freezing Mark.");
            ImGui.BulletText("Uses Freezing Salvo (>8 Seals) to force a freeze condition.");
            ImGui.BulletText("Fires Barrage to get the empower buff ONLY when the target is frozen.");
            ImGui.BulletText("Smart-channels and releases Snipe automatically when all buffs align.");
            ImGui.Spacing();

            // --- ROTATION 2 ---
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "Rotation 2 (Elite & Pack Clear)");
            ImGui.TextWrapped("Best for tanky Magic packs or weaker Rares where channeling Snipe is overkill.");
            ImGui.BulletText("Skips Snipe channeling entirely for faster clear pacing.");
            ImGui.BulletText("Maintains core debuffs (Frost Bomb, Ice Tipped Arrows, Freezing Mark).");
            ImGui.BulletText("Rapidly deploys Lightning Rods up to your setting limit.");
            ImGui.BulletText("Spams Lightning Arrow and Ice Shot to detonate overlapping AoE chains.");

            ImGui.Spacing();

            // --- ROTATION 3 ---
            ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), "Rotation 3 (Speed Mapping)");
            ImGui.TextWrapped("Best for pure, high-speed map clearing. Prioritizes mobility over setup animations.");
            ImGui.BulletText("Completely ignores Frost Bomb, Freezing Salvo, and Lightning Rods.");
            ImGui.BulletText("Only refreshes Marks/Buffs if you happen to hover cleanly over a target.");
            ImGui.BulletText("Delivers uninterrupted, maximum-attack-speed spam of LA and Ice Shot.");

            ImGui.Unindent();
        }

    }



}
