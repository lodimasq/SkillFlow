using static ExileCore2.Shared.Nodes.HotkeyNodeV2;

namespace SkillFlow;

public class TwisterSettings {

    // activation
    public Keys HoldKey = Keys.XButton1;
    public Keys BurstKey = Keys.XButton2;

    // skill keys
    public Keys WhirlingSlashKey = Keys.MButton;
    public Keys TwisterKey = Keys.Q;
    public Keys IceTipArrowKey = Keys.E;
    public Keys BarrageKey = Keys.R;
    public Keys WarBannerKey = Keys.T;
    public Keys FreezingMarkKey = Keys.F;
    public Keys DodgeRollKey = Keys.Space;

    // step toggles
    public bool UseTwister = true;
    public bool UseIceTipArrow = true;
    public bool UsePrimalBounty = true;
    public bool UseWarBanner = true;
    public bool UseFreezingMark = true;
    public bool UseBarrage = true;

    // behaviour tunables
    public bool FreezingMark_IncludeRares = false;
    public int FreezingMark_Range = 60;
    public int FreezingMark_MaxBackoffMs = 4000;
    public int Barrage_MinRares = 1;
    public bool BarragePreserveRoll = false;
    public int PreserveRollDelayMs = 100;
    public int WarBanner_RecastDelayMs = 4000;
    public bool WarBanner_IncludeRares = false;
    public int Twister_StackThreshold = 3;

    // twister targeting
    public int Twister_EliteRange = 90;
    public int Twister_CloseRange = 42;
    public int Twister_ZoneRadius = 22;
    public int Twister_MinDensity = 3;
    public int CastThrottleMs = 2000;
    public bool Twister_FireWithoutTarget = false;

    // auto-aim
    public bool AutoAim = true;
    public int AimSettleMs = 15;
    public int PrimalBounty_FeatherThreshold = 3;

    // binding helper
    public bool BindMouseButtons = true;

    // range overlay
    public bool DrawRangeOverlays = false;
}
