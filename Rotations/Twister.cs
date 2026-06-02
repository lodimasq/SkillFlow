using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using GameOffsets2;
using ImGuiNET;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SkillFlow;

public class TwisterRotation : IRotation {
    public string Name => "Twister";

    public TwisterSettings Settings { get; private set; }
    public Settings PluginSettings { get; set; }

    public TwisterRotation(Settings settings) {
        PluginSettings = settings;
        Settings = settings.Rotation_Twister;
    }

    //--| Snapshot model |----------------------------------------------------------
    private class Skill {
        public string Name; public string Id; public int PreferredWeaponSet;
        public bool CanBeUsed; public int CastTime; public int Stage;
        public Skill(string name, string id, int set = 0) { Name = name; Id = id; PreferredWeaponSet = set; }
        public void Reset() { CanBeUsed = false; CastTime = 0; Stage = 0; }
        public void Set(ActorSkill s) {
            CanBeUsed = s.CanBeUsed;
            CastTime = (int)s.CastTime.TotalMilliseconds;
            Stage = s.SkillUseStage;
        }
    }
    private class Buff {
        public string Name; public string Id;
        public bool IsActive; public int Charges; public int Stacks; public int FlaskSlot; public float Duration;
        public Buff(string name, string id) { Name = name; Id = id; }
        public void Reset() { IsActive = false; Charges = 0; Stacks = 0; FlaskSlot = 0; Duration = 0; }
        public void Set(ExileCore2.PoEMemory.Components.Buff b) {
            IsActive = true; Charges = b.BuffCharges; Stacks = b.BuffStacks; FlaskSlot = b.FlaskSlot; Duration = b.Timer;
        }
    }

    private class RotationSnapshot {
        private readonly Dictionary<string, Skill> _skillById;
        private readonly Dictionary<string, Buff> _buffById;

        public Skill Skill_WhirlingSlash = new("Whirling Slash", "WhirlingSlashPlayer");
        public Skill Skill_Twister       = new("Twister", "TwisterPlayer");
        public Skill Skill_IceTipArrow   = new("Ice Tipped Arrows", "IceTippedArrowsPlayer");
        public Skill Skill_Barrage       = new("Barrage", "BarragePlayer");
        public Skill Skill_WarBanner     = new("War Banner", "WarBannerPlayer");
        public Skill Skill_FreezingMark  = new("Freezing Mark", "FreezingMarkPlayer", 1);

        public Buff Buff_IceTip       = new("Ice Tip", "shearing_bolts");
        public Buff Buff_Barrage      = new("Barrage Empower", "empower_barrage_visual");
        public Buff Buff_WarBanner    = new("War Banner", "bloodstained_banner_buff_aura");
        public Buff Buff_PrimalBounty = new("Primal Bounty", "primal_bounty_buff");
        public Buff Buff_OwlFeather   = new("Owl Feather", "owl_feather");
        public Buff Buff_WSStacks     = new("WS Stacks", "spear_sandstorm_allies");

        public Buff Debuff_FreezingMark = new("Freezing Mark", "freezing_mark");

        public int FrenzyCharges = 0;

        public int RareCount = 0;
        public bool UniquePresent = false;
        public Entity EliteTarget = null;
        public Entity CloseTarget = null;
        public Entity ClusterCenter = null;
        public int ClusterDensity = 0;
        public Entity FmTarget = null;
        public bool FmTargetHasMark = false;
        public Entity TwisterAimTarget = null;
        public Vector2? TwisterAimScreenPos = null;
        public Vector2? FmAimScreenPos = null;

        public RotationSnapshot() {
            var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            _skillById = fields.Where(f => f.FieldType == typeof(Skill))
                .Select(f => f.GetValue(this) as Skill).OfType<Skill>().ToDictionary(s => s.Id);
            _buffById = fields.Where(f => f.FieldType == typeof(Buff))
                .Select(f => f.GetValue(this) as Buff).OfType<Buff>().ToDictionary(b => b.Id);
        }

        public void SnapshotSkills(Actor actor) {
            foreach (var skill in actor.ActorSkills) {
                if (!_skillById.TryGetValue(skill.Name, out var s)) continue;
                if (skill.WeaponSetBinding == s.PreferredWeaponSet && skill.CanBeUsed) s.Set(skill);
                else if (!s.CanBeUsed) s.Set(skill);
            }
        }
        public void SnapshotPlayerBuffs(Entity player) {
            foreach (var buff in player.Buffs)
                if (_buffById.TryGetValue(buff.Name, out var b)) b.Set(buff);
        }
        public void SnapshotCharges(Entity player) {
            if (player.TryGetComponent<Stats>(out var stats) && stats.StatDictionary != null)
                FrenzyCharges = stats.StatDictionary.GetValueOrDefault(GameStat.CurrentFrenzyCharges, 0);
        }

        public void SnapshotTarget(GameController gc, TwisterSettings settings) {
            var playerGrid = gc.Player.GridPos;

            var hostiles = new List<(Entity e, Vector2 grid, float dist, int rarity)>();
            foreach (var e in gc.Entities) {
                if (e == null || !e.IsValid || e.Type != EntityType.Monster) continue;
                if (e.IsDead || e.IsHidden || !e.IsHostile) continue;
                if (e.Rarity == MonsterRarity.Error) continue;
                var g = e.GridPos;
                hostiles.Add((e, g, Vector2.Distance(g, playerGrid), (int)e.Rarity));
            }

            // ELITE
            int eliteBestRarity = -1; float eliteBestDist = float.MaxValue;
            foreach (var h in hostiles) {
                if (h.dist > settings.Twister_EliteRange) continue;
                if (h.rarity < (int)MonsterRarity.Rare) continue;
                RareCount++;
                if (h.rarity == (int)MonsterRarity.Unique) UniquePresent = true;
                if (h.rarity > eliteBestRarity || (h.rarity == eliteBestRarity && h.dist < eliteBestDist)) {
                    eliteBestRarity = h.rarity; eliteBestDist = h.dist; EliteTarget = h.e;
                }
            }

            // PACK
            int bestScore = -1; Entity bestCenter = null;
            for (int i = 0; i < hostiles.Count; i++) {
                if (hostiles[i].dist > settings.Twister_EliteRange) continue;
                int score = 0;
                for (int j = 0; j < hostiles.Count; j++) {
                    if (i == j) continue;
                    if (Vector2.Distance(hostiles[i].grid, hostiles[j].grid) <= settings.Twister_ZoneRadius)
                        score++;
                }
                if (score > bestScore) { bestScore = score; bestCenter = hostiles[i].e; }
            }
            ClusterCenter = bestCenter;
            ClusterDensity = bestScore < 0 ? 0 : bestScore + 1;

            // FREEZING MARK
            int fmBestRarity = -1; float fmBestDist = float.MaxValue;
            foreach (var h in hostiles) {
                if (h.dist > settings.FreezingMark_Range) continue;
                if (h.rarity > fmBestRarity || (h.rarity == fmBestRarity && h.dist < fmBestDist)) {
                    fmBestRarity = h.rarity; fmBestDist = h.dist; FmTarget = h.e;
                }
            }
            if (FmTarget != null)
                foreach (var b in FmTarget.Buffs)
                    if (b.Name == Debuff_FreezingMark.Id) { FmTargetHasMark = true; break; }

            // CLOSE
            float closeBestDist = float.MaxValue;
            foreach (var h in hostiles) {
                if (h.dist > settings.Twister_CloseRange) continue;
                if (h.dist < closeBestDist) { closeBestDist = h.dist; CloseTarget = h.e; }
            }

            // aim: elite, else a dense on-screen cluster, else nearest close mob
            if (EliteTarget != null) TwisterAimTarget = EliteTarget;
            else if (ClusterDensity >= settings.Twister_MinDensity && SafeScreenPos(gc, ClusterCenter) != null) TwisterAimTarget = ClusterCenter;
            else if (CloseTarget != null) TwisterAimTarget = CloseTarget;

            TwisterAimScreenPos = SafeScreenPos(gc, TwisterAimTarget);
            FmAimScreenPos = SafeScreenPos(gc, FmTarget);
        }

        private static Vector2? SafeScreenPos(GameController gc, Entity target) {
            if (target == null) return null;
            var client = gc.IngameState.Camera.WorldToScreen(target.Pos);
            if (client == Vector2.Zero) return null;
            var win = gc.Window.GetWindowRectangleReal();
            float mx = win.Width * 0.08f, my = win.Height * 0.08f;
            if (client.X < mx || client.X > win.Width - mx || client.Y < my || client.Y > win.Height - my)
                return null;
            return client + gc.Window.GetWindowRectangleTimeCache.TopLeft;
        }

        public void Reset() {
            foreach (var s in _skillById.Values) s.Reset();
            foreach (var b in _buffById.Values) b.Reset();
            FrenzyCharges = 0;
            RareCount = 0; UniquePresent = false; EliteTarget = null;
            CloseTarget = null;
            ClusterCenter = null; ClusterDensity = 0;
            FmTarget = null; FmTargetHasMark = false;
            TwisterAimTarget = null; TwisterAimScreenPos = null; FmAimScreenPos = null;
        }

        public void Monitor() {
            foreach (var s in _skillById.Values) {
                DBug.Monitor("Skills", s.Name, s.CanBeUsed);
                DBug.Monitor("Skills", s.Name + " stage", s.Stage);
                DBug.Monitor("Skills", s.Name + " cast(ms)", s.CastTime);
            }
            foreach (var b in _buffById.Values) {
                DBug.Monitor("Buffs", b.Name, b.IsActive);
                DBug.Monitor("Buffs", b.Name + " charges", b.Charges);
                DBug.Monitor("Buffs", b.Name + " stacks", b.Stacks);
                DBug.Monitor("Buffs", b.Name + " flaskslot", b.FlaskSlot);
            }
            DBug.Monitor("Player", "FrenzyCharges", FrenzyCharges);
            DBug.Monitor("Target", "EliteTarget", EliteTarget != null);
            DBug.Monitor("Target", "RareCount(elite rng)", RareCount);
            DBug.Monitor("Target", "UniquePresent", UniquePresent);
            DBug.Monitor("Target", "ClusterDensity", ClusterDensity);
            DBug.Monitor("Target", "CloseTarget", CloseTarget != null);
            DBug.Monitor("Target", "TwisterAimTarget", TwisterAimTarget != null);
            DBug.Monitor("Target", "FmTarget", FmTarget != null);
            DBug.Monitor("Target", "FmHasMark", FmTargetHasMark);
        }

        public void MonitorRaw(Actor actor, Entity player) {
            foreach (var skill in actor.ActorSkills)
                if (!string.IsNullOrEmpty(skill.Name))
                    DBug.Monitor("RAW Skills", skill.Name, $"stage={skill.SkillUseStage} usable={skill.CanBeUsed}");
            foreach (var buff in player.Buffs)
                DBug.Monitor("RAW Buffs", buff.Name, $"charges={buff.BuffCharges} stacks={buff.BuffStacks} flask={buff.FlaskSlot} t={buff.Timer:0.0}");
        }
    }
    private readonly RotationSnapshot SS = new();

    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly Dictionary<string, long> _lastFired = new();
    private bool _wsHeld = false;
    private long _lastCastMs = -100000;
    private const int BuffSuppressionMs = 500;      // anti-double-cast + FM backoff base
    private const int WhirlingSlashHoldMs = 30;

    private long NowMs => _clock.ElapsedMilliseconds;
    private bool RecentlyFired(string id, int withinMs) =>
        _lastFired.TryGetValue(id, out var t) && (NowMs - t) < withinMs;
    private void MarkFired(string id) => _lastFired[id] = NowMs;

    private RotationAction Tap(Skill skill, Keys key, string reason) {
        _wsHeld = false;
        _lastCastMs = NowMs;
        MarkFired(skill.Id);
        int lockout = skill.CastTime;
        return new RotationAction(skill.Name, reason, key, lockout);
    }
    private RotationAction TapRaw(string id, string name, Keys key, string reason, int lockout) {
        _wsHeld = false;
        _lastCastMs = NowMs;
        MarkFired(id);
        return new RotationAction(name, reason, key, lockout);
    }
    // stacks live in the buff FlaskSlot: flask >> 1, capped at 3
    private int WhirlingSlashStacks() => Math.Min(SS.Buff_WSStacks.FlaskSlot >> 1, 3);

    private RotationAction TryIceTipArrow() {
        if (!Settings.UseIceTipArrow) return null;
        if (SS.Buff_IceTip.IsActive) return null;
        if (RecentlyFired(SS.Skill_IceTipArrow.Id, BuffSuppressionMs)) return null;
        bool castable = SS.Skill_IceTipArrow.CanBeUsed || SS.FrenzyCharges >= 1;
        if (!castable) return null;
        return Tap(SS.Skill_IceTipArrow, Settings.IceTipArrowKey, "ITA buff down");
    }

    private const string PrimalBountyId = "primal_bounty_roll";
    private const int RollLockoutMs = 250;
    private RotationAction TryPrimalBounty() {
        if (!Settings.UsePrimalBounty) return null;
        if (Settings.UseIceTipArrow && !SS.Buff_IceTip.IsActive) return null; // don't roll while ITA down
        if (SS.Buff_PrimalBounty.IsActive) return null;
        if (SS.Buff_OwlFeather.Charges < Settings.PrimalBounty_FeatherThreshold) return null;
        return TapRaw(PrimalBountyId, "Primal Bounty", Settings.DodgeRollKey,
            "feathers full -> roll", RollLockoutMs);
    }

    private RotationAction TryWarBanner() {
        if (!Settings.UseWarBanner) return null;
        if (!(SS.UniquePresent || (Settings.WarBanner_IncludeRares && SS.RareCount > 0))) return null;
        if (SS.Buff_WarBanner.IsActive) return null;
        if (!SS.Skill_WarBanner.CanBeUsed) return null;
        if (RecentlyFired(SS.Skill_WarBanner.Id, Settings.WarBanner_RecastDelayMs)) return null;
        return Tap(SS.Skill_WarBanner, Settings.WarBannerKey, "banner down, unique near");
    }

    private int _fmFails = 0;
    private long _fmNextAllowed = 0;
    private uint _fmTargetId = 0;
    private RotationAction TryFreezingMark() {
        if (!Settings.UseFreezingMark) return null;
        var t = SS.FmTarget;
        if (t == null) return null;
        bool eligible = t.Rarity == MonsterRarity.Unique
                        || (Settings.FreezingMark_IncludeRares && t.Rarity >= MonsterRarity.Rare);
        if (!eligible) return null;

        if (t.Id != _fmTargetId) { _fmTargetId = t.Id; _fmFails = 0; _fmNextAllowed = 0; } // new target -> reset backoff

        if (SS.FmTargetHasMark) { _fmFails = 0; _fmNextAllowed = 0; return null; }
        if (!SS.Skill_FreezingMark.CanBeUsed) return null;
        if (NowMs < _fmNextAllowed) return null;                 // backing off

        int delay = Math.Min(BuffSuppressionMs << Math.Min(_fmFails, 5), Settings.FreezingMark_MaxBackoffMs);
        _fmNextAllowed = NowMs + delay;
        _fmFails++;

        var action = Tap(SS.Skill_FreezingMark, Settings.FreezingMarkKey, $"mark missing (try {_fmFails})");
        if (Settings.AutoAim) { action.AimScreenPos = SS.FmAimScreenPos; action.AimSettleMs = Settings.AimSettleMs; }
        return action;
    }

    private RotationAction TryBarrage(bool burst) {
        if (!Settings.UseBarrage) return null;
        if (Settings.UseIceTipArrow && !SS.Buff_IceTip.IsActive) return null; // don't empower while ITA down
        if (SS.Buff_Barrage.IsActive) return null;
        bool condition = burst || SS.UniquePresent || SS.RareCount >= Settings.Barrage_MinRares;
        if (!condition) return null;
        if (!SS.Skill_Barrage.CanBeUsed) return null;
        if (RecentlyFired(SS.Skill_Barrage.Id, BuffSuppressionMs)) return null;
        return Tap(SS.Skill_Barrage, Settings.BarrageKey, "empower down, pack condition");
    }

    private RotationAction TryTwister(bool burst) {
        if (!Settings.UseTwister) return null;
        if (Settings.UseIceTipArrow && !SS.Buff_IceTip.IsActive) return null;       // hold until ITA up
        int stacks = WhirlingSlashStacks();
        if (stacks < Settings.Twister_StackThreshold) return null;

        Vector2? aim = null;
        if (SS.TwisterAimTarget != null) aim = SS.TwisterAimScreenPos;
        else if (!burst && !Settings.Twister_FireWithoutTarget) return null;        // no target: burst/debug only

        if (!SS.Skill_Twister.CanBeUsed) return null;
        var action = Tap(SS.Skill_Twister, Settings.TwisterKey, $"twister ({stacks}/{Settings.Twister_StackThreshold})");
        if (Settings.AutoAim) { action.AimScreenPos = aim; action.AimSettleMs = Settings.AimSettleMs; }
        return action;
    }

    private RotationAction IdleWhirlingSlash() {
        if (!SS.Skill_WhirlingSlash.CanBeUsed) return null;
        _wsHeld = true;
        return new RotationAction(SS.Skill_WhirlingSlash.Name, "idle build",
            Settings.WhirlingSlashKey, ActionBehavior.KeyHold, WhirlingSlashHoldMs);
    }

    // Engine won't release a held key on a null action, so release it explicitly or WS channels forever
    private RotationAction ReleaseIfHeld() {
        if (!_wsHeld) return null;
        _wsHeld = false;
        return new RotationAction(SS.Skill_WhirlingSlash.Name, "release hold",
            Settings.WhirlingSlashKey, ActionBehavior.KeyRelease, 0);
    }

    public void UpdateTelemetry(GameController gameController) { }

    public RotationAction GetNextAction(GameController gameController) {
        if (gameController == null) return null;
        var player = gameController.Player;
        if (player == null || !player.TryGetComponent<Actor>(out var actor)) return null;

        if (actor.Animation == AnimationE.DodgeRoll || actor.Animation == AnimationE.DodgeRollBack) return ReleaseIfHeld();

        bool burst = Input.IsKeyDown(Settings.BurstKey);
        if (!Input.IsKeyDown(Settings.HoldKey) && !burst) return ReleaseIfHeld();

        SS.Reset();
        SS.SnapshotSkills(actor);
        SS.SnapshotPlayerBuffs(player);
        SS.SnapshotCharges(player);
        SS.SnapshotTarget(gameController, Settings);

        if (PluginSettings.DXT.DBug.ShowMonitor) {
            SS.Monitor();
            SS.MonitorRaw(actor, player);
        }

        // global throttle (burst ignores it)
        if (!burst && NowMs - _lastCastMs < Settings.CastThrottleMs)
            return IdleWhirlingSlash() ?? ReleaseIfHeld();

        return TryIceTipArrow()
            ?? TryPrimalBounty()
            ?? TryWarBanner()
            ?? TryFreezingMark()
            ?? TryBarrage(burst)
            ?? TryTwister(burst)
            ?? IdleWhirlingSlash()
            ?? ReleaseIfHeld();
    }

    public void Render(GameController gameController, ExileCore2.Graphics graphics) {
        var player = gameController?.Player;
        if (player == null || !Settings.DrawRangeOverlays) return;

        graphics.DrawCircleInWorld(player.Pos, Settings.Twister_EliteRange * 11f, Color.Yellow);
        graphics.DrawCircleInWorld(player.Pos, Settings.Twister_CloseRange * 11f, Color.Green);

        var aim = SS.TwisterAimTarget;
        if (aim != null && aim.IsValid)
            graphics.DrawCircleInWorld(aim.Pos, 20f, Color.Green);
    }

    //--| Settings UI |-------------------------------------------------------------
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private void DrawHotkey(string name, ref Keys key) {
        DXT.Button.Draw("##" + name, new DXT.Button.Options {
            Label = $"{key} ", Width = 100, Height = 22,
        });
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Hover and press ANY key to bind it instantly!");
            for (int i = 1; i < 255; i++) {
                if ((GetAsyncKeyState(i) & 0x8000) != 0) {
                    if ((i == 1 || i == 2) && !Settings.BindMouseButtons) continue;
                    key = (Keys)i;
                    break;
                }
            }
        }
        ImGui.SameLine();
        ImGui.Text(name);
    }

    public void DrawSettings() {
        ImGui.PushItemWidth(160);

        float thr = Settings.CastThrottleMs / 1000f;
        if (ImGui.SliderFloat("Cast throttle (s)", ref thr, 0f, 3.5f, "%.2f s")) {
            thr = (float)Math.Round(thr * 4) / 4f;
            Settings.CastThrottleMs = (int)Math.Round(thr * 1000);
        }
        ImGui.Checkbox("Draw range overlays", ref Settings.DrawRangeOverlays);
        ImGui.Checkbox("Fire Twister without target", ref Settings.Twister_FireWithoutTarget);

        if (ImGui.CollapsingHeader("Keybinds", ImGuiTreeNodeFlags.DefaultOpen)) {
            DrawHotkey("Hold (activate)", ref Settings.HoldKey);
            DrawHotkey("Burst", ref Settings.BurstKey);
            DrawHotkey("Whirling Slash", ref Settings.WhirlingSlashKey);
            DrawHotkey("Twister", ref Settings.TwisterKey);
            DrawHotkey("Ice Tipped Arrows", ref Settings.IceTipArrowKey);
            DrawHotkey("Dodge Roll", ref Settings.DodgeRollKey);
            DrawHotkey("War Banner", ref Settings.WarBannerKey);
            DrawHotkey("Freezing Mark", ref Settings.FreezingMarkKey);
            DrawHotkey("Barrage", ref Settings.BarrageKey);
            ImGui.Checkbox("Allow mouse-button binds", ref Settings.BindMouseButtons);
        }

        ImGui.SeparatorText("Twister");
        ImGui.Checkbox("Enable##Twister", ref Settings.UseTwister);
        ImGui.SliderInt("Stack threshold", ref Settings.Twister_StackThreshold, 1, 3);
        ImGui.Checkbox("Auto-aim", ref Settings.AutoAim);
        ImGui.SliderInt("Aim settle (ms)", ref Settings.AimSettleMs, 0, 100);
        ImGui.SliderInt("Outer range (elites + clusters)", ref Settings.Twister_EliteRange, 10, 400);
        ImGui.SliderInt("Close range (always fire)", ref Settings.Twister_CloseRange, 5, 100);
        ImGui.SliderInt("Zone radius", ref Settings.Twister_ZoneRadius, 5, 100);
        ImGui.SliderInt("Min density", ref Settings.Twister_MinDensity, 1, 20);

        ImGui.SeparatorText("Support skills");
        ImGui.Checkbox("Ice-Tipped Arrows", ref Settings.UseIceTipArrow);
        ImGui.Checkbox("Primal Bounty", ref Settings.UsePrimalBounty);
        ImGui.SliderInt("Feather threshold", ref Settings.PrimalBounty_FeatherThreshold, 1, 3);
        ImGui.Checkbox("War Banner", ref Settings.UseWarBanner);
        ImGui.SameLine(); ImGui.Checkbox("incl. rares##WB", ref Settings.WarBanner_IncludeRares);
        ImGui.SliderInt("Banner recast (ms)", ref Settings.WarBanner_RecastDelayMs, 500, 10000);
        ImGui.Checkbox("Freezing Mark", ref Settings.UseFreezingMark);
        ImGui.SameLine(); ImGui.Checkbox("incl. rares##FM", ref Settings.FreezingMark_IncludeRares);
        ImGui.SliderInt("Mark range", ref Settings.FreezingMark_Range, 10, 200);
        ImGui.SliderInt("Mark max backoff (ms)", ref Settings.FreezingMark_MaxBackoffMs, 500, 10000);
        ImGui.Checkbox("Barrage", ref Settings.UseBarrage);
        ImGui.SliderInt("Barrage min rares", ref Settings.Barrage_MinRares, 1, 10);

        ImGui.PopItemWidth();
    }
}
