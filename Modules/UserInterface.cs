using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ImGuiNET;
using SDColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace SkillFlow;


public sealed class UserInterface : PluginModule {
    public UserInterface(Plugin plugin) : base(plugin) { }

    public void Draw() {
        DrawBuildSelector();

        DXT.Button.Draw("ShowDBug", ref Settings.DXT.DBug.ShowToolbar, new DXT.Button.Options {
            Label = "DBug",
            Width = 100,
            Height = 22,
        });
        ImGui.Checkbox("Enable Rotation in hideout", ref Settings.RunInHideout);

        ImGui.Checkbox("Weapon DPS Tooltip", ref Settings.WeaponTooltip);
    }

    private void DrawBuildSelector() {
        var builds = Plugin.RotationMenu.Keys.ToArray();
        if (builds.Length == 0) return;

        int current = Array.IndexOf(builds, Settings.SelectedRotation);
        if (current < 0) current = 0;

        ImGui.PushItemWidth(200);
        if (ImGui.Combo("Build", ref current, builds, builds.Length)) {
            Settings.SelectedRotation = builds[current];
            Plugin.LoadRotation();
        }
        ImGui.PopItemWidth();
    }
}
