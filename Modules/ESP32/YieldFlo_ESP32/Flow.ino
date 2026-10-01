// BeltFlo belt-distance pulse handling and mass integration.
//
// YieldFlo used this file for optical grain flow. BeltFlo instead counts belt
// travel and integrates the weight sitting on a known conveyor section:
//
//   delivered_lb += section_lb / section_length_in * belt_travel_in
//
// The ISR only counts pulses. Floating-point work happens after NAU7802 samples
// in loop context, keeping the interrupt short and deterministic.

void IRAM_ATTR onRPMedge()
{
    uint32_t now = micros();
    if (now - LastBeltPulseUs < BeltDebounceUs) return;

    LastBeltPulseUs = now;
    BeltPulseTotal++;
}

void DiscardPendingBeltTravel()
{
    uint32_t pulseTotal;
    noInterrupts();
    pulseTotal = BeltPulseTotal;
    interrupts();
    LastIntegratedPulseTotal = pulseTotal;
    HaveIntegrationWeight = false;
}

void UpdateBeltIntegration(float currentScaleLb)
{
    uint32_t pulseTotal;
    noInterrupts();
    pulseTotal = BeltPulseTotal;
    interrupts();

    uint32_t deltaPulses = pulseTotal - LastIntegratedPulseTotal; // uint32 wrap-safe
    LastIntegratedPulseTotal = pulseTotal;

    // Never let pulse backlog accumulate while the scale or calibration is bad.
    // There is no trustworthy mass to apply to that distance later.
    if (!ScaleOK || ScaleOverload || !ScaleCalibrated())
    {
        PreviousIntegrationLb = currentScaleLb;
        HaveIntegrationWeight = false;
        return;
    }

    if (!HaveIntegrationWeight)
    {
        PreviousIntegrationLb = currentScaleLb;
        HaveIntegrationWeight = true;
        return;
    }

    if (deltaPulses == 0)
    {
        PreviousIntegrationLb = currentScaleLb;
        return;
    }

    // Negative values are normal around zero because of converter noise, but
    // delivered mass must never run backward. The PC app differences uint32
    // counters and correctly treats a backward jump as a module reset, so a
    // negative integration step here would be actively harmful.
    double w0 = PreviousIntegrationLb > 0.0f ? PreviousIntegrationLb : 0.0;
    double w1 = currentScaleLb > 0.0f ? currentScaleLb : 0.0;
    double avgSectionLb = (w0 + w1) * 0.5;

    double beltTravelIn = (double)deltaPulses * (double)Conveyor.InchesPerPulse;
    double pounds = avgSectionLb / (double)Conveyor.SectionLenIn * beltTravelIn;

    if (isfinite(pounds) && pounds > 0.0)
        CumulativePounds += pounds;

    PreviousIntegrationLb = currentScaleLb;
}
