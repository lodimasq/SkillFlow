using ImGuiNET;

namespace DieselExileTools.ExileCore2;

public static partial class DXT {
    public static bool Checkbox(string label, string tooltip, ref bool value) {
        var b = ImGui.Checkbox(label, ref value);
        if (ImGui.IsItemHovered()) {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }
        return b;
    }
}
