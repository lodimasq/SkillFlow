using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.Shared.Enums;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace SkillFlow;



public sealed class Tooltip : PluginModule {
    public Tooltip(Plugin plugin) : base(plugin) { }

    public void Render() {

        var hoverItem = GameController.Game.IngameState.UIHover ?.AsObject<HoverItemIcon>();
        if (hoverItem == null) return;

        var item = hoverItem.Item;
        if (item == null || !item.IsValid) return;

        // weapon component
        if (!item.TryGetComponent<Weapon>(out var weapon)) return;

        // mods component
        if (!item.TryGetComponent<LocalStats>(out var localStats)) return;

        // Quality comp 
        item.TryGetComponent<Quality>(out var qualityComp);
        var quality = 1;
        if (qualityComp != null) { quality = qualityComp.ItemQuality; }
        // APS
        float aps = 1000f / weapon.AttackTime;        
        // physical
        float physMin = weapon.DamageMin;
        float physMax = weapon.DamageMax;
        float physMultiplier = 1.0f;
        // elemental totals
        float fireMin = 0;
        float fireMax = 0;
        float coldMin = 0;
        float coldMax = 0;
        float lightningMin = 0;
        float lightningMax = 0;
        // chaos
        float chaosMin = 0;
        float chaosMax = 0;

        // scan mods
        foreach (var stat in localStats.StatDictionary) {
            switch (stat.Key) {
                // phys %
                case GameStat.LocalPhysicalDamagePct:
                    physMultiplier += stat.Value / 100f;
                    break;
                // flat phys 
                case GameStat.LocalMinimumAddedPhysicalDamage:
                    physMin += stat.Value;
                    break;                
                case GameStat.LocalMaximumAddedPhysicalDamage:
                    physMax += stat.Value;
                    break;
                // attack speed
                case GameStat.LocalAttackSpeedPct:
                    aps *= (100f + stat.Value) / 100f;
                    break;
                // fire
                case GameStat.LocalMinimumAddedFireDamage:
                    fireMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedFireDamage:
                    fireMax += stat.Value;
                    break;
                // cold 
                case GameStat.LocalMinimumAddedColdDamage:
                    coldMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedColdDamage:
                    coldMax += stat.Value;
                    break;
                // lightning
                case GameStat.LocalMinimumAddedLightningDamage:
                    lightningMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedLightningDamage:
                    lightningMax += stat.Value;
                    break;
                // chaos
                case GameStat.LocalMinimumAddedChaosDamage:
                    chaosMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedChaosDamage:
                    chaosMax += stat.Value;
                    break;
            }               
        }


        float basePhysMin = physMin;
        float basePhysMax = physMax;
        // apply phys scaling
        physMin *= physMultiplier;
        physMax *= physMultiplier;
        // apply quality
        float qualityMult = (1f + quality / 100f);
        physMin *= qualityMult;
        physMax *= qualityMult;
        // DPS
        float pdps = ((physMin + physMax) / 2f) * aps;
        // fire DPS
        float fdps = ((fireMin + fireMax) / 2f) * aps;
        // cold DPS
        float cdps = ((coldMin + coldMax) / 2f) * aps;
        // lightning DPS
        float ldps = ((lightningMin + lightningMax) / 2f) * aps;
        // chaos DPS
        float chaosdps = ((chaosMin + chaosMax) / 2f) * aps;

        float edps = fdps + cdps + ldps;

        float totalDPS = pdps + edps + chaosdps;

        if (Settings.DXT.DBug.ShowMonitor) {
            DBug.Monitor("DPS", "quality", quality);
            DBug.Monitor("DPS", "physMultiplier", physMultiplier);
            DBug.Monitor("DPS", "physMin", physMin);
            DBug.Monitor("DPS", "physMax", physMax);
            DBug.Monitor("DPS", "aps", aps);
            DBug.Monitor("DPS", "phys dps", pdps);
            DBug.Monitor("DPS", "fire dps", fdps);
            DBug.Monitor("DPS", "cold dps", cdps);
            DBug.Monitor("DPS", "lightning dps", ldps);
            DBug.Monitor("DPS", "chaosdps", chaosdps);
            DBug.Monitor("DPS", "elemental dps", edps);
            DBug.Monitor("DPS", "total dps", totalDPS);
        }

        var tooltipRect = hoverItem.Tooltip?.GetClientRect();
        if (tooltipRect == null) return;

        var textSize  = Graphics.MeasureText("X");
        var offset = 10;
        var lines = 9;
        var padding = 10;

        var myTooltipRect = new ExileCore2.Shared.RectangleF(tooltipRect.Value.Left, tooltipRect.Value.Bottom + offset, tooltipRect.Value.Width, textSize.Y * lines + (padding * 2));
        Graphics.DrawBox(myTooltipRect, SColor.FromArgb(200, 0, 0, 0));

        var textPos = new SVector2(myTooltipRect.Left + padding, myTooltipRect.Top + padding);

        DrawStat("Total DPS: ", totalDPS, textPos.X, textPos.Y);
        DrawStat("Physical DPS: ", pdps, textPos.X, textPos.Y + textSize.Y);
        DrawStat("Elemental DPS: ", edps, textPos.X, textPos.Y + textSize.Y * 2);
        DrawStat("Chaos DPS: ", chaosdps, textPos.X, textPos.Y + textSize.Y * 3);


        // IRON RUNE gains
        float ironL = (((basePhysMin * physMultiplier * qualityMult) * (1f + 0.14f)) + ((basePhysMax * physMultiplier * qualityMult) * (1f + 0.14f))) / 2f * aps - pdps;
        float ironN = (((basePhysMin * physMultiplier * qualityMult) * (1f + 0.16f)) + ((basePhysMax * physMultiplier * qualityMult) * (1f + 0.16f))) / 2f * aps - pdps;
        float ironG = (((basePhysMin * physMultiplier * qualityMult) * (1f + 0.18f)) + ((basePhysMax * physMultiplier * qualityMult) * (1f + 0.18f))) / 2f * aps - pdps;


        float desertL = ((5f + 8f) / 2f) * aps;
        float desertN = ((7f + 11f) / 2f) * aps;
        float desertG = ((13f + 16f) / 2f) * aps;

        float glacialL = ((4f + 7f) / 2f) * aps;
        float glacialN = ((6f + 10f) / 2f) * aps;
        float glacialG = ((9f + 15f) / 2f) * aps;

        float stormL = ((1f + 14f) / 2f) * aps;
        float stormN = ((1f + 20f) / 2f) * aps;
        float stormG = ((1f + 29f) / 2f) * aps;

        DrawRuneLine("+Iron Rune", ironG, ironN, ironL, textPos.X, textPos.Y + textSize.Y * 5);
        DrawRuneLine("+Desert Rune", desertG, desertN, desertL, textPos.X, textPos.Y + textSize.Y * 6);
        DrawRuneLine("+Glacial Rune", glacialG, glacialN, glacialL, textPos.X, textPos.Y + textSize.Y * 7);
        DrawRuneLine("+Storm Rune", stormG, stormN, stormL, textPos.X, textPos.Y + textSize.Y * 8);
    }




    private SColor TitleColor = DXTC.Colors.Text; 
    private SColor ValueColor = DXTC.Colors.TextYellow;
    void DrawStat(string label, float value, float x, float y) {
        var labelPos = new SVector2(x, y);

        Graphics.DrawText(label, labelPos, TitleColor);

        var labelWidth = Graphics.MeasureText(label).X;

        Graphics.DrawText( value.ToString("0.0"), new SVector2(x + labelWidth, y), ValueColor);
    }

    void DrawRuneLine( string runeName, float greater, float normal, float lesser, float x, float y) {
        var pos = new SVector2(x, y);

        // segment 1: rune name
        Graphics.DrawText($"{runeName}: ", pos, TitleColor);
        var width = Graphics.MeasureText($"{runeName}: ").X;

        // segment 2: Greater
        var gText = "G: ";
        Graphics.DrawText(gText, new SVector2(x + width, y), TitleColor);
        width += Graphics.MeasureText(gText).X;

        Graphics.DrawText($"{greater:0.0}", new SVector2(x + width, y), ValueColor);
        width += Graphics.MeasureText($"{greater:0.0}").X + 10;

        // segment 3: Normal
        var nText = "N: ";
        Graphics.DrawText(nText, new SVector2(x + width, y), TitleColor);
        width += Graphics.MeasureText(nText).X;

        Graphics.DrawText($"{normal:0.0}", new SVector2(x + width, y), ValueColor);
        width += Graphics.MeasureText($"{normal:0.0}").X + 10;

        // segment 4: Lesser
        var lText = "L: ";
        Graphics.DrawText(lText, new SVector2(x + width, y), TitleColor);
        width += Graphics.MeasureText(lText).X;

        Graphics.DrawText($"{lesser:0.0}", new SVector2(x + width, y), ValueColor);
    }



}



