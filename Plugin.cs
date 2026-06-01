using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2;
using ImGuiNET;
using System.Numerics;
using static DieselExileTools.ExileCore2.DXT;

namespace SkillFlow;

public class Plugin : BaseSettingsPlugin<Settings> {

    //--| Properties |-------------------------------------------------------------------------------------------------
    private UserInterface _userInterface;
    private UserInterface UserInterface => _userInterface ??= new UserInterface(this);

    private Engine _engine;
    private Engine Engine => _engine ??= new (this);

    private Tooltip _tooltip;
    private Tooltip Tooltip => _tooltip ??= new (this);

    DXT.FloatingToolbar.Button _levelButton;

    private int _currentLevel = 0;
    public void SetButtonLevel(int level) {
        if (level == _currentLevel) return;
        _currentLevel = level;
        if (_levelButton == null || _currentLevel <= 0) return;
        _levelButton.Label = _currentLevel.ToString();
    }

    //--| Initialise |--------------------------------------------------------------------------------------------------
    public override bool Initialise() {
        CanUseMultiThreading = true;
        Initialise_DXT();
        Engine.Initialise();

        LoadRotation();
        return base.Initialise();
    }
    private void Initialise_DXT() {
        DBug.AdditionalTools.Add(new DXT.FloatingToolbar.Button {
            Label = "BGLog",
            Tooltip = DXT.Tooltip.BasicOptions("Toggle Background Action Logging"),
            SetChecked = (bool state) => { Settings.LogInBackground = state; },
            GetChecked = () => Settings.LogInBackground,
        });

        _levelButton = new DXT.FloatingToolbar.Button {
            Label = "0",
            Tooltip = DXT.Tooltip.BasicOptions("Player Level"),
        };
        DBug.AdditionalTools.Add(_levelButton);

        DXT.Initialise(new DXT.Config
        {
            PluginName = Name,
            PluginDirectory = DirectoryFullName,
            GameController = GameController,
            Graphics = Graphics,
            Settings = Settings.DXT,
        });

        DBug.LogHeader = (width, height) => {
            //DXT.Button.Draw($"{Name}Friendly", ref Settings.DebugFriendlyIcon, new DXT.Button.Options { Label = "Friendly", Width = 80, Height = 22 }); ImGui.SameLine();
            //DXT.Button.Draw($"{Name}Monster", ref Settings.DebugMonsterIcon, new DXT.Button.Options { Label = "Monster", Width = 80, Height = 22 }); ImGui.SameLine();
            //DXT.Button.Draw($"{Name}Chest", ref Settings.DebugChestIcon, new DXT.Button.Options { Label = "Chest", Width = 80, Height = 22 }); ImGui.SameLine();
            //DXT.Button.Draw($"{Name}Ingame", ref Settings.DebugMinimapIcon, new DXT.Button.Options { Label = "Ingame", Width = 80, Height = 22 }); ImGui.SameLine();
            //DXT.Button.Draw($"{Name}Misc", ref Settings.DebugMiscIcon, new DXT.Button.Options { Label = "Misc", Width = 80, Height = 22 }); ImGui.SameLine();
            //DXT.Button.Draw($"{Name}User", ref Settings.DebugUser, new DXT.Button.Options { Label = "User", Width = 80, Height = 22 }); ImGui.SameLine();


        };
    }
    //--| Draw Settings |-----------------------------------------------------------------------------------------------
    public override void DrawSettings() {
        UserInterface.Draw();
        if (ActiveRotation != null) ActiveRotation.DrawSettings();
    }
    //--| Tick |-------------------------------------------------------------------------------------------------------
    public override void Tick() {
        Engine.Tick();
    }
    //--| Render |-----------------------------------------------------------------------------------------------------
    public override void Render() {
        Engine.Render();
        DBug.Render();
        Tooltip.Render();
        if (ActiveRotation != null) ActiveRotation.Render(GameController, Graphics);
    }

    //--| Rotations |--------------------------------------------------------------------------------------------------------
    private BowsAncientsRotation _bowsAncientsRotation;
    private BowsAncientsRotation BowsAncientsRotation => _bowsAncientsRotation ??= new(this.Settings);

    private Dictionary<string, IRotation> _rotationMenu;
    private Dictionary<string, IRotation> RotationMenu => _rotationMenu ??= new() {
        { "Bows: Ancients", BowsAncientsRotation },
        //{ "Volcanic Fissure", VolcanicRotation }
    };

    public IRotation ActiveRotation;

    public void LoadRotation() {
        ActiveRotation = BowsAncientsRotation;
    }


}
