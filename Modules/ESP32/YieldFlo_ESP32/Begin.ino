// BeltFlo startup. Network / OTA / EEPROM handling is intentionally kept close
// to the proven YieldFlo ESP32 firmware; only the grain sensors are replaced by
// the NAU7802 scale and belt proximity input.

static uint8_t MapResetReason()
{
    switch (esp_reset_reason())
    {
    case ESP_RST_POWERON:  return 1;
    case ESP_RST_EXT:      return 2;
    case ESP_RST_SW:       return 3;
    case ESP_RST_INT_WDT:
    case ESP_RST_TASK_WDT:
    case ESP_RST_WDT:      return 4;
    case ESP_RST_BROWNOUT: return 5;
    case ESP_RST_PANIC:    return 6;
    case ESP_RST_UNKNOWN:  return 0;
    default:               return 7;
    }
}

void DoSetup()
{
    ResetReasonCode = MapResetReason();

    Serial.begin(38400);
    delay(1000);
    Serial.println();
    Serial.println();
    Serial.println(InoDescription);
    Serial.println();

    EEPROM.begin(EEPROM_SIZE);
    LoadData();

    // Version from DDMMY InoID.
    uint16_t yr = InoID % 10 + 2020;
    uint16_t rest = InoID / 10;
    uint8_t mn = rest % 100;
    uint16_t dy = rest / 100;

    String fwVer;
    if (mn <= 12 && dy <= 31)
    {
        fwVer = "Firmware Version: v";
        fwVer += String(yr);
        fwVer += ".";
        if (mn < 10) fwVer += "0";
        fwVer += String(mn);
        fwVer += ".";
        if (dy < 10) fwVer += "0";
        fwVer += String(dy);
    }
    else fwVer = "Firmware Version: invalid";

    Serial.println(fwVer);
    Serial.print("Module ID: ");
    Serial.println(MDL.ID);
    Serial.println();

    // I2C: YF1 / ESP32 defaults SDA 21, SCL 22. The Load Cell 2 Click NAU7802
    // is address 0x2A and the SparkFun library configures it for 80 SPS.
    Wire.begin();
    Wire.setClock(400000);
    StartScaleHardware(true);

    // Belt proximity input. This is the physical input formerly called RPM in
    // YieldFlo, so the YF1 wiring can be reused without a board change.
    Serial.print("Starting belt proximity input on GPIO ");
    Serial.print(MDL.RPMpin);
    Serial.print(" ... ");
    if (MDL.RPMpin < NC)
    {
        pinMode(MDL.RPMpin, INPUT); // GPIO35 has no internal pull-up; YF1 conditions the signal
        attachInterrupt(digitalPinToInterrupt(MDL.RPMpin), onRPMedge, RISING);
        Serial.println("OK.");
    }
    else Serial.println("not configured.");

    // WiFi access point — unchanged architecture from YieldFlo.
    StartWifiAP();

    Serial.println();
    Serial.println("Starting Web Server");

    server.on("/", HandleRoot);
    server.on("/wifi", HandleWifiPage);
    server.onNotFound(HandleRoot);

    server.on("/generate_204", []() { server.send(204, "text/plain", ""); });
    server.on("/fwlink", []() { server.send(200, "text/plain", "OK"); });
    server.on("/hotspot-detect.html", HTTP_GET, []() { server.send(200, "text/html", "<html><body>Portal</body></html>"); });
    server.on("/ncsi.txt", HTTP_GET, []() { server.send(200, "text/plain", "Microsoft NCSI"); });
    server.on("/connecttest.txt", HTTP_GET, []() { server.send(200, "text/plain", "Microsoft Connect Test"); });

    server.on("/update", HTTP_GET, []() {
        NotePortalRequest();
        server.sendHeader("Connection", "close");
        server.send(200, "text/html", GetPageUpdate());
    });

    server.begin();
    MDNS.begin("beltflo");

    ESP2SOTA.begin(&server);
    Serial.println("OTA started.");

    // CAN bus (TWAI), 250 kbps. WiFi AP remains active for portal / OTA.
    if (MDL.CommMode == CommModeCan)
    {
        Serial.print("Starting TWAI CAN (TX GPIO ");
        Serial.print(MDL.CanTxPin);
        Serial.print(", RX GPIO ");
        Serial.print(MDL.CanRxPin);
        Serial.println(") ...");

        twai_general_config_t g = TWAI_GENERAL_CONFIG_DEFAULT(
            (gpio_num_t)MDL.CanTxPin, (gpio_num_t)MDL.CanRxPin, TWAI_MODE_NORMAL);
        twai_timing_config_t t = TWAI_TIMING_CONFIG_250KBITS();
        twai_filter_config_t f = TWAI_FILTER_CONFIG_ACCEPT_ALL();

        if (twai_driver_install(&g, &t, &f) == ESP_OK && twai_start() == ESP_OK)
            Serial.println("TWAI CAN started at 250 kbps.");
        else
            Serial.println("TWAI CAN failed to start.");
    }

    // Ethernet (W5500), unchanged from YieldFlo except BeltFlo UDP ports.
    if (MDL.CommMode == CommModeEth)
    {
        Serial.println("Starting Ethernet ...");
        uint8_t ip3 = 50 + MDL.ID;
        IPAddress LocalIP(MDL.EthIP0, MDL.EthIP1, MDL.EthIP2, ip3);
        static uint8_t LocalMac[] = { 0x0A, 0x0B, 0x59, 0x46, 0x0D, 0x00 };
        LocalMac[5] = ip3;

        Ethernet.init(W5500_SS);
        IPAddress Gateway(MDL.EthIP0, MDL.EthIP1, MDL.EthIP2, 1);
        IPAddress Mask(255, 255, 255, 0);
        Ethernet.begin(LocalMac, LocalIP, Gateway, Gateway, Mask);

        delay(1500);
        EthChipFound = (Ethernet.hardwareStatus() != EthernetNoHardware);
        if (EthChipFound)
        {
            if (Ethernet.linkStatus() == LinkON)
                Serial.println("Ethernet connected.");
            else
                Serial.println("Ethernet cable not connected.");

            Serial.print("Ethernet IP: ");
            Serial.println(Ethernet.localIP());
            UDP_Ethernet.begin(ListeningPort);
        }
        else Serial.println("Ethernet hardware (W5500) not found.");

        Ethernet_DestinationIP = IPAddress(MDL.EthIP0, MDL.EthIP1, MDL.EthIP2, 255);
    }

    StartWifiStation();

    // Do not backlog belt pulses that happened during startup against the first
    // scale value. Integration starts from the current pulse total.
    noInterrupts();
    LastIntegratedPulseTotal = BeltPulseTotal;
    interrupts();

    Serial.println();
    Serial.println("Finished setup.");
    Serial.println();
}

void SaveData()
{
    EEPROM.put(0, (int16_t)StructVersion);
    EEPROM.put(10, MDL);
    EEPROM.commit();
    delay(250);
    ESP.restart();
}

void LoadData()
{
    bool IsValid = false;
    int16_t StoredStructVersion;
    EEPROM.get(0, StoredStructVersion);

    if (StoredStructVersion == StructVersion)
    {
        Serial.println("Loading stored settings.");
        EEPROM.get(10, MDL);
        IsValid = ValidData();

        // Preserve every existing communication setting. Only migrate the old
        // stock hotspot name; a custom AP name is left exactly as the user set it.
        if (IsValid && strcmp(MDL.APname, "YieldFlo_ESP32") == 0)
        {
            strncpy(MDL.APname, "BeltFlo_ESP32", ModStringLengths);
            MDL.APname[ModStringLengths - 1] = 0;
            EEPROM.put(10, MDL);
            EEPROM.commit();
            Serial.println("Migrated default hotspot name to BeltFlo_ESP32.");
        }
    }

    if (!IsValid)
    {
        Serial.println("Stored settings not valid.");
        LoadDefaults();
        EEPROM.put(0, (int16_t)StructVersion);
        EEPROM.put(10, MDL);
        EEPROM.commit();
        delay(100);
    }
}

uint8_t ValidPins0[] = { 0,2,4,5,13,14,15,16,17,18,19,21,22,23,25,26,27,32,33,34,35,36,39 };

bool PinValid(uint8_t pin)
{
    for (int i = 0; i < (int)sizeof(ValidPins0); i++)
        if (pin == ValidPins0[i]) return true;
    return false;
}

bool ValidData()
{
    if (MDL.RPMpin != NC && !PinValid(MDL.RPMpin)) return false;
    if (!PinValid(MDL.CanTxPin)) return false;
    if (!PinValid(MDL.CanRxPin)) return false;
    if (MDL.CommMode > CommModeEth) return false;
    if (MDL.StaChannelCache > 13) return false;
    return true;
}

void LoadDefaults()
{
    Serial.println("Loading BeltFlo default settings.");

    strncpy(MDL.APname, "BeltFlo_ESP32", ModStringLengths);
    MDL.APname[ModStringLengths - 1] = 0;
    strncpy(MDL.APpassword, "", ModStringLengths);
    MDL.APpassword[ModStringLengths - 1] = 0;

    MDL.WifiModeUseStation = false;
    strncpy(MDL.SSID, "Tractor", ModStringLengths);
    MDL.SSID[ModStringLengths - 1] = 0;
    strncpy(MDL.Password, "111222333", ModStringLengths);
    MDL.Password[ModStringLengths - 1] = 0;

    // Legacy fields retained in EEPROM layout; unused by BeltFlo.
    MDL.ADS1115Enabled = false;
    MDL.RPMpin = 35;      // Belt proximity input
    MDL.CompPin = 32;
    MDL.MainPin = 33;
    MDL.UseCompSignal = false;
    MDL.InvertSensor = false;
    MDL.AlertPin = 16;
    MDL.AnalogPin = NC;

    MDL.CommMode = CommModeWifi;
    MDL.CanTxPin = 14;
    MDL.CanRxPin = 27;
    MDL.EthIP0 = 192;
    MDL.EthIP1 = 168;
    MDL.EthIP2 = 1;
    MDL.StaChannelCache = 0;
}
