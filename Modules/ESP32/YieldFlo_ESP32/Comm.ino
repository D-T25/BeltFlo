// BeltFlo conveyor communication.
//
// UDP module -> PC, PGN 40010, 19 bytes, 5 Hz, destination port 30300.
// UDP PC -> module, PGN 40011, 18 bytes, listen port 30400.
//
// CAN module -> PC:
//   0x18FF02F8 counters [cum_lb_x10 uint32, cum_pulses uint32]
//   0x18FF03F8 status   [flags, scale_lb_x10 int16, scale_raw int32, reserved]
// CAN PC -> module:
//   0x18FF04F9 settings bytes 0..7
//   0x18FF05F9 settings bytes 8..12 + CRC16

static const uint16_t SETTINGS_PGN = 40011;
static const uint32_t CAN_COUNTERS_ID = 0x18FF02F8u;
static const uint32_t CAN_STATUS_ID   = 0x18FF03F8u;
static const uint32_t CAN_SETTINGS_A_ID = 0x18FF04F9u;
static const uint32_t CAN_SETTINGS_B_ID = 0x18FF05F9u;

// -----------------------------------------------------------------------------
// Little-endian helpers
// -----------------------------------------------------------------------------
static uint16_t ReadU16LE(const byte* p)
{
    return (uint16_t)p[0] | ((uint16_t)p[1] << 8);
}

static int32_t ReadI32LE(const byte* p)
{
    uint32_t v = (uint32_t)p[0]
               | ((uint32_t)p[1] << 8)
               | ((uint32_t)p[2] << 16)
               | ((uint32_t)p[3] << 24);
    return (int32_t)v;
}

static void WriteU16LE(byte* p, uint16_t v)
{
    p[0] = (byte)(v & 0xFF);
    p[1] = (byte)((v >> 8) & 0xFF);
}

static void WriteU32LE(byte* p, uint32_t v)
{
    p[0] = (byte)(v & 0xFF);
    p[1] = (byte)((v >> 8) & 0xFF);
    p[2] = (byte)((v >> 16) & 0xFF);
    p[3] = (byte)((v >> 24) & 0xFF);
}

static void WriteI16LE(byte* p, int16_t v)
{
    WriteU16LE(p, (uint16_t)v);
}

static void WriteI32LE(byte* p, int32_t v)
{
    WriteU32LE(p, (uint32_t)v);
}

static uint16_t Crc16CcittFalse(const byte* data, uint8_t len)
{
    uint16_t crc = 0xFFFF;
    for (uint8_t n = 0; n < len; n++)
    {
        crc ^= (uint16_t)data[n] << 8;
        for (uint8_t i = 0; i < 8; i++)
            crc = (crc & 0x8000) ? (uint16_t)((crc << 1) ^ 0x1021) : (uint16_t)(crc << 1);
    }
    return crc;
}

// -----------------------------------------------------------------------------
// Settings receive / apply
// -----------------------------------------------------------------------------
static void ResetIntegrationForSettingChange()
{
    noInterrupts();
    LastIntegratedPulseTotal = BeltPulseTotal;
    interrupts();
    HaveIntegrationWeight = false;
}

static void ApplySettingsBlock(const byte block[13])
{
    int32_t zero = ReadI32LE(block + 0);

    float span;
    memcpy(&span, block + 4, sizeof(float)); // ESP32 is little-endian, same as .NET BitConverter

    float section = ReadU16LE(block + 8) / 10.0f;
    float ipp = ReadU16LE(block + 10) / 1000.0f;
    float beltStop = block[12] / 10.0f;

    if (!isfinite(span)) return;
    if (!isfinite(section) || section <= 0.0f) return;
    if (!isfinite(ipp) || ipp <= 0.0f) return;
    if (!isfinite(beltStop) || beltStop <= 0.0f) beltStop = 2.0f;

    bool integrationChanged =
        (zero != Conveyor.ZeroCounts) ||
        (span != Conveyor.SpanLbPerCount) ||
        (section != Conveyor.SectionLenIn) ||
        (ipp != Conveyor.InchesPerPulse);

    Conveyor.ZeroCounts = zero;
    Conveyor.SpanLbPerCount = span;
    Conveyor.SectionLenIn = section;
    Conveyor.InchesPerPulse = ipp;
    Conveyor.BeltStopTimeoutS = beltStop;
    Conveyor.LastReceivedMs = millis();
    Conveyor.EverReceived = true;

    if (integrationChanged) ResetIntegrationForSettingChange();
}

static void HandleSettingsUdp(const byte* pkt, int len)
{
    if (len < 18) return;
    if (ReadU16LE(pkt) != SETTINGS_PGN) return;

    // Final byte is the same byte-sum CRC8 used by BeltFlo conveyor packets.
    byte ck = 0;
    for (int i = 0; i < 17; i++) ck += pkt[i];
    if (ck != pkt[17]) return;

    const byte* block = pkt + 2;
    uint16_t sentCrc = ReadU16LE(pkt + 15);
    if (Crc16CcittFalse(block, 13) != sentCrc) return;

    ApplySettingsBlock(block);
}

static byte CanSettingsA[8];
static bool CanSettingsAValid = false;
static uint32_t CanSettingsAMs = 0;

static void ReceiveCanFrames()
{
    if (MDL.CommMode != CommModeCan) return;

    twai_message_t msg;
    while (twai_receive(&msg, 0) == ESP_OK)
    {
        if (!msg.extd || msg.data_length_code != 8) continue;

        if (msg.identifier == CAN_SETTINGS_A_ID)
        {
            memcpy(CanSettingsA, msg.data, 8);
            CanSettingsAValid = true;
            CanSettingsAMs = millis();
        }
        else if (msg.identifier == CAN_SETTINGS_B_ID)
        {
            if (!CanSettingsAValid || (millis() - CanSettingsAMs) > 1000) continue;

            byte block[13];
            memcpy(block, CanSettingsA, 8);
            memcpy(block + 8, msg.data, 5);
            uint16_t sentCrc = ReadU16LE(msg.data + 5);

            if (Crc16CcittFalse(block, 13) == sentCrc)
                ApplySettingsBlock(block);

            CanSettingsAValid = false;
        }
    }
}

// -----------------------------------------------------------------------------
// Status / counter snapshots
// -----------------------------------------------------------------------------
static byte BuildStatusFlags()
{
    byte flags = 0;
    if (ScaleOK)          flags |= 0x01; // ScaleOK
    if (BeltRunningNow()) flags |= 0x02; // BeltRunning
    if (ScaleTared())       flags |= 0x04; // Tared / zero set
    if (ReceivingFromPC())flags |= 0x08; // settings heartbeat
    if (ScaleOverload)    flags |= 0x10; // converter near rail
    return flags;
}

static uint32_t SnapshotPulses()
{
    uint32_t p;
    noInterrupts();
    p = BeltPulseTotal;
    interrupts();
    return p;
}

static uint32_t SnapshotPoundsX10()
{
    double v = CumulativePounds * 10.0;
    if (!isfinite(v) || v <= 0.0) return 0;
    uint64_t q = (uint64_t)llround(v);
    return (uint32_t)q; // deliberate wrap; PC differences uint32 counters
}

static int16_t SnapshotScaleX10()
{
    double v = (double)ScaleLb * 10.0;
    if (!isfinite(v)) v = 0;
    if (v > 32767.0) v = 32767.0;
    if (v < -32768.0) v = -32768.0;
    return (int16_t)lround(v);
}

// -----------------------------------------------------------------------------
// CAN health monitoring — retained from YieldFlo
// -----------------------------------------------------------------------------
static uint32_t LastBusOffMs = 0;
static uint8_t BusOffCount = 0;

void CheckCanBus()
{
    twai_status_info_t st;
    if (twai_get_status_info(&st) != ESP_OK) return;

    if (st.state == TWAI_STATE_BUS_OFF)
    {
        if (millis() - LastBusOffMs > 3000)
        {
            LastBusOffMs = millis();
            BusOffCount++;
            Serial.print("CAN Bus Off. Recovery attempt ");
            Serial.println(BusOffCount);
            twai_initiate_recovery();
        }

        if (BusOffCount > 5)
        {
            Serial.println("CAN Bus Off: persistent. Falling back to WiFi for this session.");
            MDL.CommMode = CommModeWifi;
            BusOffCount = 0;
            SendLastPK1 = 0;
        }
    }
    else if (st.state == TWAI_STATE_STOPPED)
    {
        twai_start();
    }

    static uint32_t LastStatusMs = 0;
    static twai_status_info_t PrevSt;
    static bool StatusPrinted = false;

    if (millis() - LastStatusMs > 5000)
    {
        LastStatusMs = millis();

        if (StatusPrinted
            && st.state == PrevSt.state
            && st.msgs_to_tx == PrevSt.msgs_to_tx
            && st.tx_error_counter == PrevSt.tx_error_counter
            && st.rx_error_counter == PrevSt.rx_error_counter
            && st.tx_failed_count == PrevSt.tx_failed_count
            && st.bus_error_count == PrevSt.bus_error_count
            && st.arb_lost_count == PrevSt.arb_lost_count) return;

        PrevSt = st;
        StatusPrinted = true;

        Serial.print("CAN state="); Serial.print((int)st.state);
        Serial.print(" txq="); Serial.print(st.msgs_to_tx);
        Serial.print(" TEC="); Serial.print(st.tx_error_counter);
        Serial.print(" REC="); Serial.print(st.rx_error_counter);
        Serial.print(" txFailed="); Serial.print(st.tx_failed_count);
        Serial.print(" busErr="); Serial.print(st.bus_error_count);
        Serial.print(" arbLost="); Serial.println(st.arb_lost_count);
    }
}

// -----------------------------------------------------------------------------
// CAN send, 5 Hz
// -----------------------------------------------------------------------------
void SendCAN()
{
    if (millis() - SendLastPK1 < SendTimePK1) return;

    twai_status_info_t st;
    if (twai_get_status_info(&st) != ESP_OK) return;
    if (st.state != TWAI_STATE_RUNNING) return;

    SendLastPK1 = millis();

    uint32_t lbX10 = SnapshotPoundsX10();
    uint32_t pulses = SnapshotPulses();
    int16_t scaleX10 = SnapshotScaleX10();

    // Send status first. Core.ApplyConveyorStatus() is the PC side's reconnect
    // hook; it immediately pushes PGN 40011 settings when it sees a new module.
    // If counters go first they mark ModuleConnected before status arrives and
    // that immediate settings push is missed (the periodic 2 s heartbeat would
    // still recover, but the link comes up needlessly slowly).
    twai_message_t status;
    memset(&status, 0, sizeof(status));
    status.extd = 1;
    status.identifier = CAN_STATUS_ID;
    status.data_length_code = 8;
    status.data[0] = BuildStatusFlags();
    WriteI16LE(status.data + 1, scaleX10);
    WriteI32LE(status.data + 3, ScaleRaw);
    status.data[7] = 0;
    twai_transmit(&status, pdMS_TO_TICKS(10));

    twai_message_t counters;
    memset(&counters, 0, sizeof(counters));
    counters.extd = 1;
    counters.identifier = CAN_COUNTERS_ID;
    counters.data_length_code = 8;
    WriteU32LE(counters.data + 0, lbX10);
    WriteU32LE(counters.data + 4, pulses);
    twai_transmit(&counters, pdMS_TO_TICKS(10));
}

// -----------------------------------------------------------------------------
// UDP send, 5 Hz
// -----------------------------------------------------------------------------
void UdpSend(byte pkt[], int len)
{
    if (MDL.CommMode == CommModeEth)
    {
        if (!EthChipFound || Ethernet.linkStatus() != LinkON) return;
        UDP_Ethernet.beginPacket(Ethernet_DestinationIP, ModuleSendPort);
        UDP_Ethernet.write(pkt, len);
        UDP_Ethernet.endPacket();
    }
    else
    {
        UDP_Wifi.beginPacket(Wifi_DestinationIP, ModuleSendPort);
        UDP_Wifi.write(pkt, len);
        UDP_Wifi.endPacket();
    }
}

void SendUdp()
{
    if (millis() - SendLastPK1 < SendTimePK1) return;
    SendLastPK1 = millis();

    byte pkt[19];
    memset(pkt, 0, sizeof(pkt));

    pkt[0] = 0x4A; // PGN 40010 little-endian
    pkt[1] = 0x9C;
    pkt[2] = BuildStatusFlags();
    WriteU32LE(pkt + 3, SnapshotPoundsX10());
    WriteU32LE(pkt + 7, SnapshotPulses());
    WriteI16LE(pkt + 11, SnapshotScaleX10());
    WriteI32LE(pkt + 13, ScaleRaw);
    pkt[17] = 0; // reserved
    pkt[18] = CRC(pkt, 18, 0);

    UdpSend(pkt, sizeof(pkt));
}

// -----------------------------------------------------------------------------
// Receive settings on UDP and CAN
// -----------------------------------------------------------------------------
static void DrainWifiSettings()
{
    int sz = UDP_Wifi.parsePacket();
    while (sz > 0)
    {
        byte pkt[64];
        int n = UDP_Wifi.read(pkt, sizeof(pkt));
        if (n > 0) HandleSettingsUdp(pkt, n);
        UDP_Wifi.flush();
        sz = UDP_Wifi.parsePacket();
    }
}

static void DrainEthernetSettings()
{
    if (!EthChipFound) return;

    int sz = UDP_Ethernet.parsePacket();
    while (sz > 0)
    {
        byte pkt[64];
        int n = UDP_Ethernet.read(pkt, sizeof(pkt));
        if (n > 0) HandleSettingsUdp(pkt, n);
        UDP_Ethernet.flush();
        sz = UDP_Ethernet.parsePacket();
    }
}

void ReceiveComm()
{
    // The WiFi socket exists in every comm mode because the BeltFlo access point
    // stays up for the portal. Accepting settings there as well is useful for
    // bench testing and does not interfere with CAN or Ethernet operation.
    DrainWifiSettings();
    DrainEthernetSettings();
    ReceiveCanFrames();
}
