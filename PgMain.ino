// BeltFlo module portal. Styling is retained from YieldFlo; the grain-sensor
// controls are replaced by read-only conveyor diagnostics because zero/span and
// geometry are owned by the BeltFlo PC app (PGN 40011).

String GetPageStyle()
{
    String st = "<style>";
    st += "html { font-family:Helvetica,Arial,sans-serif; display:inline-block; margin:0 auto; text-align:center; }";
    st += "body { margin-top:40px; background-color:wheat; }";
    st += "h1 { color:#444; margin:40px auto 12px; text-decoration:underline; }";
    st += "h1.subhead { margin:20px auto 12px; }";
    st += "table.center { margin-left:auto; margin-right:auto; border-collapse:collapse; table-layout:fixed; width:100%; max-width:568px; }";
    st += "td.label-col { width:40%; text-align:left; padding:8px 12px; vertical-align:middle; }";
    st += "td.input-col { width:60%; padding:8px 12px; vertical-align:middle; }";
    st += ".control-width { width:320px; max-width:90%; margin:0 auto; box-sizing:border-box; }";
    st += ".InputCell { display:block; width:100%; height:36px; box-sizing:border-box; text-align:center; font-size:18px; font-weight:700; padding:4px 6px; }";
    st += ".button-72 { align-items:center; background-color:initial; background-image:linear-gradient(rgba(179,132,201,.84),rgba(57,31,91,.84) 50%); border-radius:42px; border-width:0; box-shadow:rgba(57,31,91,0.24) 0 2px 2px, rgba(179,132,201,0.4) 0 8px 12px; color:#FFF; cursor:pointer; display:inline-flex; font-size:18px; font-weight:700; justify-content:center; letter-spacing:.04em; line-height:16px; margin:12px auto; padding:12px 18px; text-align:center; text-decoration:none; user-select:none; touch-action:manipulation; width:320px; max-width:90%; }";
    st += ".radio-row { display:flex; flex-wrap:wrap; align-items:center; gap:16px; min-height:44px; }";
    st += ".radio-row label { font-size:18px; font-weight:700; display:flex; align-items:center; gap:6px; cursor:pointer; }";
    st += "input[type=radio].styled { -webkit-appearance:none; appearance:none; width:44px; height:44px; display:inline-block; position:relative; margin:0; padding:0; box-sizing:border-box; border-radius:10px; background-image:linear-gradient(rgba(179,132,201,.84),rgba(57,31,91,.84) 50%); box-shadow:rgba(57,31,91,0.24) 0 2px 2px, rgba(179,132,201,0.4) 0 8px 12px; cursor:pointer; outline:none; border:1px solid rgba(57,31,91,0.25); }";
    st += "input[type=radio].styled::after { content:''; position:absolute; left:50%; top:50%; width:12px; height:22px; border-right:4px solid white; border-bottom:4px solid white; transform:translate(-50%,-60%) rotate(45deg) scale(0); transform-origin:center; transition:transform .12s ease-in-out; border-radius:2px; }";
    st += "input[type=radio].styled:checked::after { transform:translate(-50%,-60%) rotate(45deg) scale(1); }";
    st += ".hint { font-size:12px; color:#333; margin-top:4px; }";
    st += ".status { margin:2px auto 16px; font-size:16px; }";
    st += ".good { color:#176b2c; font-weight:700; } .warn { color:#a05a00; font-weight:700; } .bad { color:#a00000; font-weight:700; }";
    st += "a:link { font-size:150%; }";
    st += "table.nets { margin:0 auto; border-collapse:collapse; width:320px; max-width:90%; }";
    st += "table.nets td { padding:10px 8px; border-bottom:1px solid rgba(57,31,91,0.2); text-align:left; font-size:16px; }";
    st += "table.nets td.sig { text-align:right; white-space:nowrap; color:#333; font-size:14px; }";
    st += "table.nets a { font-size:100%; font-weight:700; text-decoration:none; color:#391f5b; }";
    st += "</style>";
    return st;
}

static String YesNo(bool v)
{
    return v ? "Yes" : "No";
}

String GetPageMain()
{
    uint16_t yr = InoID % 10 + 2020;
    uint16_t rest = InoID / 10;
    uint8_t mn = rest % 100;
    uint16_t dy = rest / 100;
    String fwVer = "v" + String(yr) + ".";
    if (mn < 10) fwVer += "0";
    fwVer += String(mn) + ".";
    if (dy < 10) fwVer += "0";
    fwVer += String(dy);

    uint32_t pulses;
    noInterrupts();
    pulses = BeltPulseTotal;
    interrupts();

    String st = "<HTML><head>";
    st += "<META content='text/html; charset=utf-8' http-equiv=Content-Type>";
    st += "<meta name='viewport' content='width=device-width, initial-scale=1.0'>";
    st += "<title>BeltFlo Module</title>";
    st += GetPageStyle();
    st += "</head><BODY>";
    st += "<h1>BeltFlo Module</h1>";
    st += "<p class='status'>" + fwVer + "</p>";

    // Live conveyor status.
    st += "<table class='center'>";
    st += "<tr><td colspan='2' style='text-align:center; padding:0;'><h1 class='subhead'>Conveyor</h1></td></tr>";
    st += "<tr><td class='label-col'>Scale</td><td class='input-col'><b>" + String(ScaleOK ? "OK" : "NO SCALE") + "</b></td></tr>";
    st += "<tr><td class='label-col'>Section weight</td><td class='input-col'>" + String(ScaleLb, 1) + " lb</td></tr>";
    st += "<tr><td class='label-col'>Raw counts</td><td class='input-col'>" + String(ScaleRaw) + "</td></tr>";
    st += "<tr><td class='label-col'>Belt pulses</td><td class='input-col'>" + String(pulses) + "</td></tr>";
    st += "<tr><td class='label-col'>Belt running</td><td class='input-col'>" + YesNo(BeltRunningNow()) + "</td></tr>";
    st += "<tr><td class='label-col'>PC settings</td><td class='input-col'>" + String(ReceivingFromPC() ? "Receiving" : "Not receiving") + "</td></tr>";
    st += "<tr><td class='label-col'>Calibrated</td><td class='input-col'>" + YesNo(ScaleCalibrated()) + "</td></tr>";
    st += "<tr><td class='label-col'>Overload</td><td class='input-col'>" + YesNo(ScaleOverload) + "</td></tr>";
    st += "<tr><td class='label-col'>Total since boot</td><td class='input-col'>" + String(CumulativePounds, 1) + " lb</td></tr>";

    st += "<tr><td colspan='2'><hr></td></tr>";
    st += "<tr><td colspan='2' style='text-align:center; padding:0;'><h1 class='subhead'>App Settings</h1></td></tr>";
    st += "<tr><td class='label-col'>Zero counts</td><td class='input-col'>" + String(Conveyor.ZeroCounts) + "</td></tr>";
    st += "<tr><td class='label-col'>Span</td><td class='input-col'>" + String(Conveyor.SpanLbPerCount, 8) + " lb/count</td></tr>";
    st += "<tr><td class='label-col'>Scale section</td><td class='input-col'>" + String(Conveyor.SectionLenIn, 1) + " in</td></tr>";
    st += "<tr><td class='label-col'>Belt travel</td><td class='input-col'>" + String(Conveyor.InchesPerPulse, 3) + " in/pulse</td></tr>";
    st += "<tr><td class='label-col'>Stop timeout</td><td class='input-col'>" + String(Conveyor.BeltStopTimeoutS, 1) + " s</td></tr>";
    st += "<tr><td class='label-col'>Belt input</td><td class='input-col'>GPIO " + String(MDL.RPMpin) + "</td></tr>";
    st += "</table>";

    st += "<form id=FORM1 method=post action='/'>";
    st += "<table class='center'>";
    st += "<tr><td colspan='2'><hr></td></tr>";
    st += "<tr><td colspan='2' style='text-align:center; padding:0;'><h1 class='subhead'>Communication</h1></td></tr>";
    st += "<tr><td class='label-col'>Mode</td><td class='input-col'><div class='control-width'><div class='radio-row'>";
    st += "<label><input class='styled' type='radio' name='commmode' value='wifi'" + String(MDL.CommMode == CommModeWifi ? " checked" : "") + "> WiFi</label>";
    st += "<label><input class='styled' type='radio' name='commmode' value='can'" + String(MDL.CommMode == CommModeCan ? " checked" : "") + "> CAN</label>";
    st += "<label><input class='styled' type='radio' name='commmode' value='eth'" + String(MDL.CommMode == CommModeEth ? " checked" : "") + "> Ethernet</label>";
    st += "</div></div></td></tr>";

    String ethSubnet = String(MDL.EthIP0) + "." + String(MDL.EthIP1) + "." + String(MDL.EthIP2);
    st += "<tr><td class='label-col'>Ethernet subnet</td><td class='input-col'><div class='control-width'><input class='InputCell' name='ethsubnet' value='" + ethSubnet + "'></div></td></tr>";
    st += "<tr><td colspan='2'><div class='control-width'><div class='hint'>Module IP: " + ethSubnet + "." + String(50 + MDL.ID) + "</div></div></td></tr>";

    if (MDL.CommMode == CommModeEth)
    {
        st += "<tr><td colspan='2' style='text-align:center;'>";
        if (!EthChipFound) st += "<div class='status'>Ethernet hardware (W5500) not found</div>";
        else if (Ethernet.linkStatus() == LinkON) st += "<div class='status'>Ethernet connected (" + Ethernet.localIP().toString() + ")</div>";
        else st += "<div class='status'>Ethernet cable not connected</div>";
        st += "</td></tr>";
    }

    st += "</table>";
    st += "<p><div class='control-width'><input class='button-72' type='submit' value='Save / Restart'></div></p>";
    st += "</form>";

    st += "<p><a href='/wifi'>WiFi Network</a></p>";
    if (WiFi.isConnected()) st += "<p class='status'>Connected to " + String(MDL.SSID) + " (" + WiFi.localIP().toString() + ")</p>";
    else if (MDL.WifiModeUseStation) st += "<p class='status'>Network not connected</p>";
    st += "<p><a href='/update'>Update Firmware</a></p>";
    st += "</BODY></HTML>";
    return st;
}
