#include "src/ESP2SOTA_RC/index_html.h"
#include "src/ESP2SOTA_RC/ESP2SOTA_RC.h" // modified from https://github.com/pangodream/ESP2SOTA

#include <WiFi.h>
#include <ESPmDNS.h>
#include <WebServer.h>
#include <DNSServer.h>
#include <WiFiUdp.h>
#include <WiFiClient.h>
#include <EEPROM.h>
#include <Wire.h>
#include <SPI.h>
#include <Ethernet_Generic.h>
#include <EthernetUdp.h>
#include "driver/twai.h"
#include <SparkFun_Qwiic_Scale_NAU7802_Arduino_Library.h>

// BeltFlo conveyor module, based on the YieldFlo ESP32 firmware.
// Board: DOIT ESP32 DEVKIT V1 / YF1 ESP32 hardware.
#define InoDescription "BeltFlo_ESP32"
#define InoID 30096         // firmware version, DDMMY -> 2026-09-30
#define StructVersion 5     // EEPROM layout unchanged from current YieldFlo firmware

// Comm modes
const uint8_t CommModeWifi = 0;
const uint8_t CommModeCan  = 1;
const uint8_t CommModeEth  = 2;

const uint8_t NC = 0xFF;
const uint8_t ModStringLengths = 15;
const uint16_t EEPROM_SIZE = 512;

// -----------------------------------------------------------------------------
// Scale / NAU7802
// -----------------------------------------------------------------------------
NAU7802 BeltScale;
bool ScaleFound = false;
bool ScaleOK = false;
bool ScaleOverload = false;
int32_t ScaleRaw = 0;          // filtered raw converter counts
float ScaleLb = 0.0f;          // live weigh-section load after app zero/span
uint32_t LastScaleReadMs = 0;
uint32_t LastScaleReconnectMs = 0;
const uint32_t ScaleStaleMs = 500;
const uint32_t ScaleReconnectMs = 5000;
const int32_t ScaleOverloadCounts = 8000000; // near 24-bit converter rails

// Eight-sample boxcar at 80 SPS ~= 100 ms. This keeps the calibration/raw
// display steady without adding much conveyor delay.
const uint8_t ScaleFilterSize = 8;
int32_t ScaleFilter[ScaleFilterSize] = {0};
int64_t ScaleFilterSum = 0;
uint8_t ScaleFilterIndex = 0;
uint8_t ScaleFilterCount = 0;

// -----------------------------------------------------------------------------
// Belt proximity sensor and cumulative counters
// -----------------------------------------------------------------------------
// The old YieldFlo RPM input is reused as the BeltFlo proximity input. Keeping
// the EEPROM field name RPMpin lets the proven portal / EEPROM layout continue
// to build while its meaning changes to "belt pulse pin" for BeltFlo.
volatile uint32_t BeltPulseTotal = 0;      // wraps naturally at uint32
volatile uint32_t LastBeltPulseUs = 0;
const uint32_t BeltDebounceUs = 2000;      // 500 Hz max; far above expected belt rate

// Pounds are kept at full precision internally, then exposed as tenths of a
// pound in PGN 40010 / CAN 0x18FF02F8.
double CumulativePounds = 0.0;
uint32_t LastIntegratedPulseTotal = 0;
float PreviousIntegrationLb = 0.0f;
bool HaveIntegrationWeight = false;

// ISR forward declaration. Arduino does not reliably auto-prototype IRAM_ATTR.
void IRAM_ATTR onRPMedge();

// -----------------------------------------------------------------------------
// Settings received from BeltFlo PC app (PGN 40011)
// -----------------------------------------------------------------------------
struct ConveyorSettingsData
{
    int32_t ZeroCounts = 0;
    float SpanLbPerCount = 0.0f;
    float SectionLenIn = 36.0f;
    float InchesPerPulse = 1.0f;
    float BeltStopTimeoutS = 2.0f;
    uint32_t LastReceivedMs = 0;
    bool EverReceived = false;
};
ConveyorSettingsData Conveyor;

const uint32_t SettingsHeartbeatMs = 4000;

bool ReceivingFromPC()
{
    return Conveyor.EverReceived && (millis() - Conveyor.LastReceivedMs) < SettingsHeartbeatMs;
}

bool ScaleCalibrated()
{
    return Conveyor.EverReceived
        && isfinite(Conveyor.SpanLbPerCount)
        && fabsf(Conveyor.SpanLbPerCount) > 0.000000001f
        && Conveyor.SectionLenIn > 0.0f
        && Conveyor.InchesPerPulse > 0.0f;
}

bool BeltRunningNow()
{
    uint32_t lastUs;
    noInterrupts();
    lastUs = LastBeltPulseUs;
    interrupts();

    if (lastUs == 0) return false;
    float timeoutS = Conveyor.BeltStopTimeoutS;
    if (!isfinite(timeoutS) || timeoutS <= 0.0f) timeoutS = 2.0f;
    uint32_t timeoutUs = (uint32_t)(timeoutS * 1000000.0f);
    if (timeoutUs < 100000) timeoutUs = 100000;
    return (micros() - lastUs) <= timeoutUs;
}

// -----------------------------------------------------------------------------
// Module configuration retained from YieldFlo for WiFi / Ethernet / CAN portal.
// Grain-sensor fields remain reserved so the existing portal files can still be
// used while the BeltFlo UI is being cleaned up. RPMpin is the belt pulse input.
// -----------------------------------------------------------------------------
struct ModuleConfig
{
    uint8_t ID = 0;
    char APname[ModStringLengths] = "BeltFlo_ESP32";
    char APpassword[ModStringLengths] = "";
    bool WifiModeUseStation = false;
    char SSID[ModStringLengths] = "Tractor";
    char Password[ModStringLengths] = "111222333";
    bool ADS1115Enabled = true; // reserved / unused in BeltFlo
    uint8_t RPMpin = 35;        // BELT PROXIMITY INPUT in BeltFlo
    uint8_t CompPin = 32;       // reserved / unused
    uint8_t MainPin = 33;       // reserved / unused
    bool UseCompSignal = false; // reserved / unused
    bool InvertSensor = false;  // reserved / unused
    uint8_t AlertPin = 16;      // reserved / unused
    uint8_t AnalogPin = NC;     // reserved / unused
    uint8_t CommMode = CommModeWifi;
    uint8_t CanTxPin = 14;
    uint8_t CanRxPin = 27;
    uint8_t EthIP0 = 192;
    uint8_t EthIP1 = 168;
    uint8_t EthIP2 = 1;
    uint8_t StaChannelCache = 0;
};
ModuleConfig MDL;

// Ethernet (W5500 on VSPI: SCK 18, MISO 19, MOSI 23, SS 5)
const uint8_t W5500_SS = 5;
EthernetUDP UDP_Ethernet;
bool EthChipFound = false;
IPAddress Ethernet_DestinationIP;

// WiFi
WiFiUDP UDP_Wifi;
IPAddress Wifi_DestinationIP(192, 168, 100, 255);
WiFiClient client;
WebServer server(80);
DNSServer dnsServer;
const byte AP_DNS_PORT = 53;

// BeltFlo ports from BeltFlo/Communication/UDPcomm.cs
const uint16_t ListeningPort = 30400;   // PC -> module settings
const uint16_t ModuleSendPort = 30300;  // module -> PC conveyor data

// Main conveyor packet is 5 Hz, matching the PC app / simulator.
const uint16_t SendTimePK1 = 200;
uint32_t SendLastPK1 = SendTimePK1;

// Reset reason codes retained for diagnostics / future health packet.
uint8_t ResetReasonCode = 0;

void setup()
{
    DoSetup();
}

void loop()
{
    dnsServer.processNextRequest();
    server.handleClient();
    ReceiveComm();
    ServiceWifiStation();
    ReadScale();

    if (MDL.CommMode == CommModeCan)
    {
        CheckCanBus();
        SendCAN();
    }
    else
    {
        SendUdp();
    }
}

bool GoodCRC(byte Data[], byte Length)
{
    byte ck = CRC(Data, Length - 1, 0);
    return (ck == Data[Length - 1]);
}

byte CRC(byte Chk[], byte Length, byte Start)
{
    byte Result = 0;
    for (int i = Start; i < Length; i++) Result += Chk[i];
    return Result;
}
