// BeltFlo scale handling.
//
// This replaces YieldFlo's ADS1115 moisture code but keeps the filename so the
// existing Arduino / Visual Micro project structure needs as little churn as
// possible. The Load Cell 2 Click uses an NAU7802 at I2C address 0x2A.

void ResetScaleFilter()
{
    ScaleFilterSum = 0;
    ScaleFilterIndex = 0;
    ScaleFilterCount = 0;
    for (uint8_t i = 0; i < ScaleFilterSize; i++) ScaleFilter[i] = 0;
    HaveIntegrationWeight = false;
}

bool StartScaleHardware(bool announce)
{
    if (announce) Serial.print("Starting NAU7802 scale ... ");

    // SparkFun begin() performs reset, power-up, 3.3 V LDO, gain 128,
    // 80 samples/s and AFE calibration. That is exactly what BeltFlo needs.
    bool ok = BeltScale.begin(Wire, true);
    ScaleFound = ok;
    ScaleOK = false;
    LastScaleReconnectMs = millis();

    if (ok)
    {
        ResetScaleFilter();
        LastScaleReadMs = 0;
        if (announce) Serial.println("OK (80 SPS, gain 128).");
    }
    else
    {
        if (announce) Serial.println("not found at I2C 0x2A.");
    }
    return ok;
}

void ReadScale()
{
    static uint32_t lastPollMs = 0;
    uint32_t now = millis();

    // If the scale is missing, probe at a slow rate so WiFi/portal operation is
    // still smooth. begin() only does the expensive calibration after the chip
    // has acknowledged on I2C.
    if (!ScaleFound)
    {
        ScaleOK = false;
        DiscardPendingBeltTravel();
        if (now - LastScaleReconnectMs >= ScaleReconnectMs)
        {
            LastScaleReconnectMs = now;
            if (BeltScale.isConnected())
            {
                Serial.println("NAU7802 detected again; reinitializing.");
                StartScaleHardware(false);
            }
        }
        return;
    }

    // 5 ms polling is fast enough to catch the NAU7802's 80 SPS data-ready bit
    // without hammering I2C on every pass through loop().
    if (now - lastPollMs < 5) return;
    lastPollMs = now;

    if (BeltScale.available())
    {
        int32_t raw = BeltScale.getReading();
        LastScaleReadMs = now;

        if (ScaleFilterCount < ScaleFilterSize)
        {
            ScaleFilter[ScaleFilterIndex] = raw;
            ScaleFilterSum += raw;
            ScaleFilterCount++;
            ScaleFilterIndex = (ScaleFilterIndex + 1) % ScaleFilterSize;
        }
        else
        {
            ScaleFilterSum -= ScaleFilter[ScaleFilterIndex];
            ScaleFilter[ScaleFilterIndex] = raw;
            ScaleFilterSum += raw;
            ScaleFilterIndex = (ScaleFilterIndex + 1) % ScaleFilterSize;
        }

        if (ScaleFilterCount > 0)
            ScaleRaw = (int32_t)(ScaleFilterSum / ScaleFilterCount);
        else
            ScaleRaw = raw;

        ScaleOverload = (llabs((long long)ScaleRaw) >= ScaleOverloadCounts);

        if (ScaleCalibrated())
        {
            ScaleLb = ((float)ScaleRaw - (float)Conveyor.ZeroCounts) * Conveyor.SpanLbPerCount;
            if (!isfinite(ScaleLb)) ScaleLb = 0.0f;
        }
        else
        {
            ScaleLb = 0.0f;
        }

        ScaleOK = true;
        UpdateBeltIntegration(ScaleLb);
        return;
    }

    // A cable unplug can leave the object "found" but produce no conversions.
    // After a stale interval, verify the I2C device is still present. If not,
    // drop to the slow reconnect path above. If it still ACKs but conversions
    // stopped, force a full re-init after two seconds.
    if (LastScaleReadMs == 0 || now - LastScaleReadMs > ScaleStaleMs)
    {
        ScaleOK = false;
        DiscardPendingBeltTravel();
    }

    if (LastScaleReadMs != 0 && now - LastScaleReadMs > 2000
        && now - LastScaleReconnectMs >= ScaleReconnectMs)
    {
        LastScaleReconnectMs = now;
        if (!BeltScale.isConnected())
        {
            Serial.println("NAU7802 connection lost.");
            ScaleFound = false;
            ResetScaleFilter();
        }
        else
        {
            Serial.println("NAU7802 stale; reinitializing.");
            StartScaleHardware(false);
        }
    }
}
