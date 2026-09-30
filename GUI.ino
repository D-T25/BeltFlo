void HandleRoot()
{
    if (server.uri() == "/") NotePortalRequest();

    if (server.hasArg("commmode")) handleSettings();
    else server.send(200, "text/html", GetPageMain());
}

void handleSettings()
{
    // Conveyor zero/span/geometry deliberately do not live on this page. The
    // BeltFlo PC app owns them and sends PGN 40011 every two seconds.
    uint8_t oldCommMode = MDL.CommMode;
    uint8_t oldE0 = MDL.EthIP0, oldE1 = MDL.EthIP1, oldE2 = MDL.EthIP2;

    String commmode = server.arg("commmode");
    commmode.trim();
    MDL.CommMode = CommModeWifi;
    if (commmode == "can") MDL.CommMode = CommModeCan;
    else if (commmode == "eth") MDL.CommMode = CommModeEth;

    if (server.hasArg("ethsubnet"))
    {
        String sn = server.arg("ethsubnet");
        sn.trim();
        int a, b, c;
        if (sscanf(sn.c_str(), "%d.%d.%d", &a, &b, &c) == 3
            && a >= 0 && a <= 255 && b >= 0 && b <= 255 && c >= 0 && c <= 255)
        {
            MDL.EthIP0 = (uint8_t)a;
            MDL.EthIP1 = (uint8_t)b;
            MDL.EthIP2 = (uint8_t)c;
        }
    }

    server.send(200, "text/html", GetPageMain());

    bool changed =
        (MDL.CommMode != oldCommMode) ||
        (MDL.EthIP0 != oldE0) || (MDL.EthIP1 != oldE1) || (MDL.EthIP2 != oldE2);

    if (changed) SaveData();
}
