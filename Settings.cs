using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using SDColor = System.Drawing.Color;
using SVector4 = System.Numerics.Vector4;

namespace SkillFlow;

public sealed class Settings : ISettings {
    public ToggleNode Enable { get; set; } = new(true);

    public DXTSettings DXT { get; set; } = new();
    public bool Debug = false;
    public bool LogInBackground = false;
    public bool RunInHideout = true;

    public bool DrawSettingsOpen = true;

    public bool WeaponTooltip = true;
    public bool WeaponTooltipDebug = false;

    public string SelectedRotation { get; set; } = "Bows: Ancients";

    public BowsAncientsSettings Rotation_BowsAncients { get; set; } = new();
    public TwisterSettings Rotation_Twister { get; set; } = new();



}
