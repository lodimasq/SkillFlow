using ExileCore2.Shared.Nodes;
using System;
using System.Collections.Generic;
using System.Text;
using static ExileCore2.Shared.Nodes.HotkeyNodeV2;

namespace SkillFlow;

public class BowsAncientsSettings {


    public int TargetCursorRadius = 20;
    public bool TargetCursorRadiusTest = false;
    public int RodCursorRadius = 20;
    public int TornadoCursorRadius = 20;
    public bool RodCursorRadiusTest = false;
    public bool BindMouseButtons = false;
    public bool SnipeTest = false;

    public Keys Rotation1_Key = Keys.F13;
    public Keys Rotation2_Key = Keys.F14;
    public Keys Rotation3_Key = Keys.F15;

    public Keys Barrage_Key = Keys.Oemcomma;
    public Keys Snipe_Key = Keys.B;
    public Keys Iceshot_Key = Keys.V;

    public Keys FrostBomb_Key = Keys.N;
    public Keys LightningRod_Key = Keys.E;
    public Keys LightningArrow_Key = Keys.V;
    public Keys IceTipArrow_Key = Keys.M;
    public Keys FreezingSalvo_Key = Keys.T;
    public Keys FreezingMark_Key = Keys.F;
    public Keys TornadoShot_Key = Keys.R;

    // ROTATION 1
    public bool Rot1_TornadoShot = true;
    public bool Rot1_BarrageSnipe = true;
    public bool Rot1_FrostBomb = true;
    public bool Rot1_IceTipArrow = true;
    public bool Rot1_FreezingMark = true;
    public bool Rot1_FreezingMarkBuff = true;
    public bool Rot1_FreezingSalvo = true;
    public bool Rot1_LightningRod = true;
    public int Rot1_RodRequiredCount = 2;
    public int Rot1_BarragedRodRequiredCount = 3;
    public bool Rot1_use_IceTipArrow = true;
    public bool Rot1_use_FreezingMark = true;

    // ROTATION 2
    public bool Rot2_TornadoShot = false;
    public bool Rot2_BarrageSnipe = false;
    public bool Rot2_FrostBomb = true;
    public bool Rot2_IceTipArrow = true;
    public bool Rot2_FreezingMark = true;
    public bool Rot2_FreezingMarkBuff = true;
    public bool Rot2_FreezingSalvo = true;
    public bool Rot2_LightningRod = true;
    public int Rot2_RodRequiredCount = 1;
    public int Rot2_BarragedRodRequiredCount = 2;
    public bool Rot2_use_IceTipArrow = true;
    public bool Rot2_use_FreezingMark = true;

    // ROTATION 3
    public bool Rot3_TornadoShot = false;
    public bool Rot3_BarrageSnipe = false;
    public bool Rot3_FrostBomb = true;
    public bool Rot3_IceTipArrow = true;
    public bool Rot3_FreezingMark = true;
    public bool Rot3_FreezingMarkBuff = true;
    public bool Rot3_FreezingSalvo = false;
    public bool Rot3_LightningRod = false;
    public int Rot3_RodRequiredCount = 2;
    public int Rot3_BarragedRodRequiredCount = 3;
    public bool Rot3_use_IceTipArrow = false;
    public bool Rot3_use_FreezingMark = true;

    // ui
    public bool RotationSettingsOpen = true;
    public bool Rotation1SettingsOpen = true;
    public bool Rotation2SettingsOpen = true;
    public bool Rotation3SettingsOpen = true;

    public bool HotkeySettingsOpen = true;
}
