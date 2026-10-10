"""Compare emitted modern candidates without changing parameters or global alignment."""

import hashlib
import json
import pathlib

import numpy as np

import analyze_differences as d

ROOT = pathlib.Path(__file__).resolve().parents[1]
DIRECTORY = ROOT / "results/phase2"
PROFILES = ["source-reverb", "source-envelope", "source-bandlimited", "source-filtered"]


def diagnostics(x, song):
    result = {"levels": d.channels(x)}
    if song == "pallet":
        result["spectral_proxies"] = d.spectrum(x)
        section = d.crop(x, .100, .300)
        square1 = d.peak_frequency(section, 3905, 3945)
        square2 = d.peak_frequency(section, 103, 117)
        result["pitch_regions"] = {
            "window_seconds": [.100, .300],
            "square1_third_harmonic": square1,
            "square1_inferred_fundamental_hz": square1["peak_hz"] / 3,
            "square2_fundamental_region": square2,
            "warning": "Mixed spectra, not source separation. Square1's fundamental is obscured by stronger PCM energy; its resolved third harmonic is reported instead."}
        result["release_limit"] = "The 95-second Pallet file ends during playing; its final sample cannot measure a natural release. Individual six-track note envelopes are not isolated here."
    else:
        result["spectral_proxies"] = d.spectrum(d.crop(x, .112, .145))
        result["spectral_window_seconds"] = [.112, .145]
        result["notes"] = []
        for index, window in enumerate(d.SELECT_WINDOWS):
            section = d.crop(x, *window)
            result["notes"].append({"tick": [0, 3, 6, 9, 12][index],
                                    "window_seconds": list(window),
                                    "ac_rms": d.rms(section - section.mean(axis=0)),
                                    "dc_left_right": section.mean(axis=0).tolist(),
                                    "frequency": d.peak_frequency(section, *( (1750, 2000) if index == 0 else (2900, 3350)))})
        result["release_steps"] = d.release_steps(d.ac_envelope(x))
        result["transient_samples"] = d.select_transients(x)
        tail = d.crop(x, .240, .280)
        result["late_tail"] = {"window_seconds": [.240, .280], "rms": d.rms(tail),
                               "mean_left_right": tail.mean(axis=0).tolist(),
                               "max_absolute": float(np.max(np.abs(tail)))}
    return result


def main():
    baseline_path = ROOT / "results/phase2-baseline-analysis.json"
    baseline_before = hashlib.sha256(baseline_path.read_bytes()).hexdigest()
    report = {
        "schema": 1,
        "scope": "Measured files only: modern alternatives, not hardware equivalence or bit-perfect claims.",
        "method": {
            "alignment": "Frozen phase-1 delays: discard 43 reference samples for SELECT and 852 for Pallet. No delay re-estimation, note phase fitting, gain fitting, time warping, synthesis changes or parameter tuning.",
            "window_policy": "Identical to baseline: SELECT spectrum 112-145 ms; five fixed note windows; Pallet stereo power spectrum over the full common 95 seconds and pitch regions over 100-300 ms.",
            "release": "Centered 2 ms local AC-RMS thresholds at 90/70/50/30/10 percent of the 120-145 ms plateau; same absolute extrema/sample-difference checks as baseline.",
            "limits": "High-frequency energy is a spectral proxy, not listening quality. Short-window frequency estimates do not imply sub-Hz independent resolution. Digital tail and oscillation-end are distinct measurements. No new reference render."},
        "profile_context_from_assignment": {
            "source-bandlimited": "Source-reverb candidate plus modern polyBLEP square-wave antialiasing.",
            "source-filtered": "Source-bandlimited plus the fixed modern DC blocker with pole 511/512, approximately 15 Hz at 48 kHz.",
            "source-envelope": "Separate experimental frame-envelope variant, included only to distinguish release-clock changes from filtering."},
        "baseline_report_sha256": baseline_before,
        "songs": {},
    }
    for song, refname in [("select", "se-stripped.wav"), ("pallet", "bgm-stripped.wav")]:
        refpath = ROOT.parent / "runtime-audio-study" / refname
        rawref = d.read_wav(refpath)
        duration = 3 if song == "select" else 95
        reference = rawref[-d.FROZEN_LAGS[song]:][:duration * d.RATE]
        refmetrics = diagnostics(reference, song)
        entry = {"fixed_lag_samples": d.FROZEN_LAGS[song], "common_seconds": duration,
                 "reference": {"file": d.metadata(refpath, rawref), **refmetrics}, "profiles": {}}
        candidates = [("phase1-frozen-baseline", ROOT / "results" / f"{song}.wav")]
        candidates += [(profile, DIRECTORY / f"{song}-{profile}.wav") for profile in PROFILES]
        for profile, path in candidates:
            before = hashlib.sha256(path.read_bytes()).hexdigest()
            x = d.read_wav(path)
            assert len(x) >= len(reference), f"Incomplete candidate: {path}"
            metrics = diagnostics(x[:len(reference)], song)
            metadata = d.metadata(path, x)
            assert metadata["sha256"] == before, f"Candidate changed during analysis: {path}"
            metrics["rms_minus_reference_db"] = d.db(metrics["levels"]["rms"] / refmetrics["levels"]["rms"])
            metrics["power_above_16khz_minus_reference_percentage_points"] = 100 * (
                metrics["spectral_proxies"]["power_fraction_above_hz"]["16000"] -
                refmetrics["spectral_proxies"]["power_fraction_above_hz"]["16000"])
            if song == "select":
                offsets = [p["time_ms"] - r["time_ms"] for p, r in
                           zip(metrics["release_steps"]["steps"], refmetrics["release_steps"]["steps"])]
                metrics["release_step_offsets_from_reference_ms"] = offsets
                metrics["mean_release_step_offset_from_reference_ms"] = float(np.mean(offsets))
            entry["profiles"][profile] = {"file": metadata, **metrics}
        report["songs"][song] = entry
    path = DIRECTORY / "modern-analysis.json"
    path.write_text(json.dumps(report, indent=2, allow_nan=False) + "\n")
    assert hashlib.sha256(baseline_path.read_bytes()).hexdigest() == baseline_before
    print(path)
    for song, entry in report["songs"].items():
        for profile, result in [("reference", entry["reference"]), *entry["profiles"].items()]:
            high = 100 * result["spectral_proxies"]["power_fraction_above_hz"]["16000"]
            dc = result["levels"]["dc_left_right"]
            print(f"{song:6s} {profile:23s} >16k {high:.6f}% DC [{dc[0]:+.9f}, {dc[1]:+.9f}]")


if __name__ == "__main__":
    main()
