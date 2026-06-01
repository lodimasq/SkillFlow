using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Interfaces;
using ImGuiNET;
using System.Diagnostics;
using System.Xml.Linq;
using SDColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace SkillFlow;

public class RotationAction {
    public string Name { get; set; } = "";
    public string Reason { get; set; } = "";
    public Keys Key { get; set; }
    public ActionBehavior Behavior { get; set; } = ActionBehavior.KeyPress;
    /// <summary>
    /// The duration (in milliseconds) to stall the engine loop after firing.
    /// </summary>
    public int LockoutDuration { get; set; } = 0;

    /// <summary>
    /// Triggers a quick key press and release event (for instant cast skills).
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="reason">The reason for the action.</param>
    /// <param name="key">The keyboard key to press.</param>
    /// <param name="lockout">The duration (in milliseconds) to stall the engine loop after firing.</param>
    public RotationAction(String name, String reason, Keys key, int lockout = 100) {
        Name = name;
        Reason = reason;
        Key = key;
        Behavior = ActionBehavior.KeyPress;
        LockoutDuration = lockout;
    }

    /// <summary>
    /// Set a new rotation action
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="reason">The reason for the action.</param>
    /// <param name="key">The keyboard key that triggers the rotation action.</param>
    /// <param name="Behavior">The action behavior to apply when the key is used. </param>
    /// <param name="lockout">The duration (in milliseconds) to stall the engine loop after firing.</param>
    public RotationAction(String name, String reason, Keys key, ActionBehavior behavior, int lockout = 0) {
        Name = name;
        Reason = reason;
        Key = key;
        Behavior = behavior;
        LockoutDuration = lockout;
    }
}
public enum ActionBehavior {
    KeyPress,     // Normal KeyDown + KeyUp instant combo
    KeyHold,      // KeyDown only (holds it across frames)
    KeyRelease    // KeyUp only (stops channeling)
}

public sealed class Engine : PluginModule {
    public Engine(Plugin plugin) : base(plugin) { }

    private float _inputLockoutTimer = 0f;
    private readonly Stopwatch _frameStopwatch = new Stopwatch();
    private RotationAction _latestActionData = null;
    private Keys _currentlyHeldKey = Keys.None;

    public void Initialise() {
        _frameStopwatch.Start();
        return;
    }

    public void Tick() {
        if (!Settings.RunInHideout && GameController.Area.CurrentArea.IsHideout) return;

        if (GameController == null) return;
        if (Plugin.ActiveRotation == null) return;

        var player = GameController.Player;
        if (player == null || !player.IsValid) return;

        if(player.TryGetComponent<Player>(out var playerComp)) Plugin.SetButtonLevel(playerComp.Level);

        if (!player.TryGetComponent<Actor>(out var actor)) return;

        int elapsedMs = (int)_frameStopwatch.ElapsedMilliseconds;
        _frameStopwatch.Restart();
        
        //Plugin.ActiveRotation.UpdateTelemetry(player, entities);

        if (_inputLockoutTimer > 0) {
            _inputLockoutTimer -= elapsedMs;
            // reset lockout if player is dodge rolling
            if ( actor.Animation == AnimationE.DodgeRoll || actor.Animation == AnimationE.DodgeRollBack) _inputLockoutTimer = 50;
        }
        else {
            _latestActionData = Plugin.ActiveRotation.GetNextAction(GameController);

            if (_latestActionData != null && _latestActionData.Key != Keys.None) {
                ExecuteInputBehavior(_latestActionData);
                _inputLockoutTimer = _latestActionData.LockoutDuration;
            }
        }
    }

    private void ExecuteInputBehavior(RotationAction action) {
        switch (action.Behavior) {
            case ActionBehavior.KeyPress:
                Log($"[{action.Name}]: {action.Reason}");
                // Force-release any legacy keys stuck in the buffer before executing a standard blip
                SafeReleaseCurrentKey();
                Input.KeyDown(action.Key);
                Input.KeyUp(action.Key);
                break;
            case ActionBehavior.KeyHold:
                Log($"[{action.Name}]: {action.Reason}");
                // Only send KeyDown if we aren't already holding this exact key down
                if (_currentlyHeldKey != action.Key) {
                    SafeReleaseCurrentKey(); // Drop old key if switching channels
                    Input.KeyDown(action.Key);
                    _currentlyHeldKey = action.Key;
                }
                break;
            case ActionBehavior.KeyRelease:
                Log($"[{action.Name}]: {action.Reason}");
                if (_currentlyHeldKey == action.Key || action.Key == Keys.None) {
                    Input.KeyUp(_currentlyHeldKey);
                    _currentlyHeldKey = Keys.None;
                }
                break;
        }
    }
    private void SafeReleaseCurrentKey() {
        //Log($"Releasing current key: {_currentlyHeldKey}");
        if (_currentlyHeldKey != Keys.None) {
            Input.KeyUp(_currentlyHeldKey);
            _currentlyHeldKey = Keys.None;
        }
    }


    private void Log(string msg) {
        DBug.Log(msg, !Settings.LogInBackground); 
    }

    public void Render() {
        DBug.Monitor("Engine", "Lock Active:",  _inputLockoutTimer > 0);
        DBug.Monitor("Engine", "Lock Duration:", $"{_inputLockoutTimer}ms");
    }



}






