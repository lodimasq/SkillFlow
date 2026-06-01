using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using Newtonsoft.Json.Bson;
using System.Windows.Forms;



namespace SkillFlow;

public interface IRotation {

    string Name { get; }

    // This runs every single frame so the profile can update its own internal debug variables
    void UpdateTelemetry(GameController gameController);

    // Evaluates conditions and returns the next key to press (returns Keys.None if nothing is ready)
    RotationAction GetNextAction(GameController gameController);

    // Optional: Draw some rotation info
    void Render(GameController gameController, ExileCore2.Graphics graphics);

    // Optional: Draw some ui settings
    void DrawSettings(); 


}


