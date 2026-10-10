"""Phase-2 waveform diagnostics, using only Python/NumPy/SciPy and existing WAVs.

This is analysis, not a synthesizer or a listening-quality score. Delays are frozen
from the phase-1 comparison; no per-note phase, gain or timing optimization is used.
Run from any directory. --results-dir allows analysis of separately rendered work.
"""

import argparse
import hashlib
import json
import math
import pathlib
import platform
import re
import wave

import numpy as np
import scipy
from scipy.ndimage import uniform_filter1d
from scipy.signal import find_peaks, welch

ROOT = pathlib.Path(__file__).resolve().parents[1]
RATE = 48000
FRAME_SECONDS = 280896 / 16777216
# Phase-1 first-second correlation estimates, copied unchanged from comparison.json.
# Positive means discard samples from output; negative means discard from reference.
FROZEN_LAGS = {"select": -43, "pallet": -852}
SELECT_WINDOWS = [(0.005, 0.025), (0.036, 0.047), (0.060, 0.078),
                  (0.086, 0.098), (0.112, 0.145)]
TINY = np.finfo(float).tiny


def db(ratio):
    return float(20 * np.log10(max(float(ratio), TINY)))


def rms(x):
    return float(np.sqrt(np.mean(np.square(x))))


def read_wav(path):
    with wave.open(str(path)) as w:
        assert (w.getframerate(), w.getnchannels(), w.getsampwidth()) == (RATE, 2, 2)
        data = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2")
    return data.reshape(-1, 2).astype(np.float64) / 32768


def metadata(path, x):
    activity = {}
    maximum = np.max(np.abs(x), axis=1)
    for threshold in [1 / 32768, 0.001, 0.01]:
        indices = np.flatnonzero(maximum >= threshold)
        activity[str(threshold)] = {
            "first_sample": int(indices[0]) if len(indices) else None,
            "last_sample": int(indices[-1]) if len(indices) else None,
            "first_ms": float(indices[0] / 48) if len(indices) else None,
            "last_ms": float(indices[-1] / 48) if len(indices) else None,
        }
    return {"path": str(path.resolve()), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "samples": len(x), "seconds": len(x) / RATE, "peak": float(maximum.max()),
            "raw_threshold_activity": activity}


def channels(x):
    channel_rms = np.sqrt(np.mean(x * x, axis=0))
    mid, side = (x[:, 0] + x[:, 1]) / 2, (x[:, 0] - x[:, 1]) / 2
    return {"rms": rms(x), "rms_left": float(channel_rms[0]),
            "rms_right": float(channel_rms[1]), "left_over_right_db": db(channel_rms[0] / channel_rms[1]),
            "left_right_correlation": float(np.corrcoef(x.T)[0, 1]),
            "side_over_mid_rms": rms(side) / rms(mid), "dc_left_right": x.mean(axis=0).tolist(),
            "peak": float(np.max(np.abs(x)))}


def level_comparison(a, b):
    output, reference = channels(a), channels(b)
    ratio = output["rms"] / reference["rms"]
    return {"output": output, "reference": reference, "output_over_reference_rms": ratio,
            "output_minus_reference_db": db(ratio), "rms_match_gain_for_output": 1 / ratio,
            "left_right_balance_difference_db": output["left_over_right_db"] - reference["left_over_right_db"]}


def spectrum(x, nperseg=4096):
    """Stereo power average: avoids cancelling opposite-panned components in mono."""
    if x.ndim == 1:
        x = x[:, None]
    size = min(nperseg, len(x))
    f, p = welch(x, RATE, nperseg=size, axis=0, detrend="constant")
    p = p.mean(axis=1)
    total = p.sum()
    ac = x - x.mean(axis=0)
    return {"spectral_centroid_hz": float(np.sum(f * p) / total),
            "power_fraction_above_hz": {str(c): float(p[f >= c].sum() / total)
                                        for c in [4000, 8000, 12000, 16000]},
            "normalized_first_difference_rms": rms(np.diff(x, axis=0)) / rms(ac),
            "welch_segment_samples": size,
            "warning": "High-band energy and sample-to-sample variation are roughness proxies, not a psychoacoustic roughness score."}


def peak_frequency(x, low, high):
    """Hann periodogram, 16x zero padding and log-parabolic peak interpolation.

    Zero padding aids localization, not independent spectral resolution. No phase fit.
    """
    if x.ndim == 2:
        x = x.mean(axis=1)
    nfft = 1 << int(math.ceil(math.log2(max(len(x) * 16, 4096))))
    p = np.abs(np.fft.rfft((x - x.mean()) * np.hanning(len(x)), nfft)) ** 2
    f = np.fft.rfftfreq(nfft, 1 / RATE)
    maxima, _ = find_peaks(p)
    candidates = maxima[(f[maxima] >= low) & (f[maxima] <= high)]
    if not len(candidates):
        raise ValueError(f"No resolved spectral peak in {low}..{high} Hz")
    i = int(candidates[np.argmax(p[candidates])])
    y = np.log(np.maximum(p[i - 1:i + 2], TINY))
    delta = np.clip(0.5 * (y[0] - y[2]) / (y[0] - 2 * y[1] + y[2]), -.5, .5)
    return {"peak_hz": float((i + delta) * RATE / nfft),
            "search_band_hz": [low, high], "window_seconds": len(x) / RATE,
            "unpadded_bin_hz": RATE / len(x),
            "warning": "Peak location is descriptive; sub-bin output digits do not imply equivalent independent frequency resolution."}


def crop(x, start, end):
    return x[round(start * RATE):round(end * RATE)]


def ac_envelope(x):
    """Centered 2 ms local standard deviation, excluding slow DC/filter transients."""
    mono = x.mean(axis=1)
    mean = uniform_filter1d(mono, size=96, mode="nearest")
    variance = uniform_filter1d(mono * mono, size=96, mode="nearest") - mean * mean
    return np.sqrt(np.maximum(variance, 0))


def edge_time(envelope, expected, rising):
    left = np.median(crop(envelope, expected - .006, expected - .003))
    right = np.median(crop(envelope, expected + .003, expected + .006))
    threshold = (left + right) / 2
    start, end = round((expected - .003) * RATE), round((expected + .003) * RATE)
    y = envelope[start:end] - threshold
    indices = np.flatnonzero((y[:-1] < 0) & (y[1:] >= 0) if rising else
                            (y[:-1] > 0) & (y[1:] <= 0))
    if not len(indices):
        return None
    i = int(min(indices, key=lambda q: abs(start + q - expected * RATE)))
    fraction = -y[i] / (y[i + 1] - y[i])
    return float((start + i + fraction) / RATE)


def release_steps(envelope):
    plateau = float(np.median(crop(envelope, .120, .145)))
    start, end = round(.145 * RATE), round(.245 * RATE)
    steps = []
    for relative in [.9, .7, .5, .3, .1]:
        below = np.flatnonzero(envelope[start:end] < plateau * relative)
        i = start + int(below[0])
        previous, current = envelope[i - 1], envelope[i]
        time = (i - 1 + (plateau * relative - previous) / (current - previous)) / RATE
        steps.append({"relative_crossing": relative, "time_ms": float(time * 1000)})
    return {"plateau_ac_rms": plateau, "steps": steps,
            "successive_step_intervals_ms": np.diff([s["time_ms"] for s in steps]).tolist()}


def select_transients(x):
    """Report actual samples/extrema; do not equate a filtered edge with a register write."""
    mono = x.mean(axis=1)
    peaks, _ = find_peaks(mono, prominence=.004, distance=10)
    valleys, _ = find_peaks(-mono, prominence=.004, distance=10)
    cycles = []
    for peak in peaks:
        following = valleys[valleys > peak]
        if not len(following):
            continue
        valley = int(following[0])
        cycles.append({"peak_sample": int(peak), "peak_ms": float(peak / 48),
                       "valley_sample": valley, "valley_ms": valley / 48,
                       "peak_to_valley": float(mono[peak] - mono[valley])})
    plateau = np.median([c["peak_to_valley"] for c in cycles if 140 <= c["peak_ms"] < 150])
    first_drop = next(i for i, c in enumerate(cycles)
                      if c["peak_ms"] > 150 and c["peak_to_valley"] < .9 * plateau)
    difference = np.abs(np.diff(mono))
    last_edges = {}
    for threshold in [.001, .0001, 1 / 32768]:
        i = int(np.flatnonzero(difference > threshold)[-1])
        last_edges[str(threshold)] = {"from_sample": i, "to_sample": i + 1,
                                      "from_ms": i / 48, "to_ms": (i + 1) / 48}
    last_nonzero = int(np.flatnonzero(mono != 0)[-1])
    return {"previous_cycle_before_first_threshold_drop": cycles[first_drop - 1],
            "first_cycle_below_90_percent_plateau": cycles[first_drop],
            "last_prominent_peak_ms": float(peaks[-1] / 48),
            "last_prominent_valley_ms": float(valleys[-1] / 48),
            "last_sample_difference_above_threshold": last_edges,
            "last_nonzero_sample": last_nonzero, "last_nonzero_ms": last_nonzero / 48,
            "silence_from_sample": last_nonzero + 1, "silence_from_ms": (last_nonzero + 1) / 48,
            "method": "Extrema prominence >=0.004 full scale, separation >=10 samples, and paired next trough. Times already include the frozen global alignment. A filtered waveform edge only bounds the audible transition; it does not establish the exact hardware register-write time."}


def select_detail(a, b, event_path):
    events = []
    for line in event_path.read_text().splitlines():
        values = dict(re.findall(r"(\w+)=(\d+)", line))
        if "note key=" in line:
            events.append({k: int(values[k]) for k in ["frame", "sample", "tick", "key", "velocity", "gate", "program"]})
    assert [e["tick"] for e in events] == [0, 3, 6, 9, 12]
    env_a, env_b = ac_envelope(a), ac_envelope(b)
    notes = []
    edges_a, edges_b = [0.0], [0.0]
    for i, (event, window) in enumerate(zip(events, SELECT_WINDOWS)):
        aa, bb = crop(a, *window), crop(b, *window)
        ac_a = rms(aa - aa.mean(axis=0))
        ac_b = rms(bb - bb.mean(axis=0))
        lo, hi = (1750, 2000) if i == 0 else (2900, 3350)
        note = {**event, "stable_window_seconds": list(window),
                "output_ac_rms": ac_a, "reference_ac_rms": ac_b,
                "output_over_reference_ac_rms": ac_a / ac_b,
                "output_minus_reference_ac_db": db(ac_a / ac_b),
                "ac_rms_match_gain": ac_b / ac_a,
                "output_frequency": peak_frequency(aa, lo, hi),
                "reference_frequency": peak_frequency(bb, lo, hi)}
        note["output_minus_reference_frequency_hz"] = note["output_frequency"]["peak_hz"] - note["reference_frequency"]["peak_hz"]
        if i:
            expected = event["sample"] / RATE
            ta = edge_time(env_a, expected, event["velocity"] > events[i - 1]["velocity"])
            tb = edge_time(env_b, expected, event["velocity"] > events[i - 1]["velocity"])
            edges_a.append(ta)
            edges_b.append(tb)
            note["amplitude_edge"] = {"output_ms": ta * 1000, "reference_ms": tb * 1000,
                                      "output_minus_reference_ms": (ta - tb) * 1000}
        notes.append(note)
    fine_sample = math.ceil(9 * FRAME_SECONDS * RATE)
    ra, rb = release_steps(env_a), release_steps(env_b)
    magnitude = np.abs(a[:, 0])
    changes = np.flatnonzero(np.diff(magnitude) != 0) + 1
    changes = changes[(changes > .145 * RATE) & (changes < .240 * RATE)]
    # Present this exact step detector only for an unfiltered, piecewise-constant square.
    exact_steps = [{"sample": int(i), "time_ms": float(i / 48),
                    "new_magnitude": float(magnitude[i])} for i in changes] if len(changes) <= 8 else None
    offsets = [x["time_ms"] - y["time_ms"] for x, y in zip(ra["steps"], rb["steps"])]
    tail = crop(b, .240, .280).mean(axis=1)
    time = np.arange(len(tail)) / RATE
    slope, intercept = np.polyfit(time, np.log(np.maximum(np.abs(tail), 1 / 32768)), 1)
    predicted = intercept + slope * time
    actual = np.log(np.maximum(np.abs(tail), 1 / 32768))
    return {"notes": notes,
            "note_edge_method": "Centered 2 ms local AC RMS, local before/after midpoint. Approximately 1 ms envelope resolution; no waveform phase fit. First onset anchored by frozen global delay, not re-estimated per note.",
            "first_four_audible_note_lengths_ms": {"output": (np.diff(edges_a) * 1000).tolist(),
                                                    "reference": (np.diff(edges_b) * 1000).tolist()},
            "fine_source_schedule": {"tick": 18, "frame": 9, "sample": fine_sample,
                                      "time_ms": fine_sample / 48,
                                      "warning": "Derived from the documented sequencer clock. Fine is not directly observable in a WAV; first amplitude drop is later."},
            "release": {"output": ra, "reference": rb,
                        "output_exact_absolute_amplitude_steps_if_piecewise_constant": exact_steps,
                        "sample_and_extremum_evidence": {"output": select_transients(a), "reference": select_transients(b)},
                        "output_minus_reference_step_ms": offsets,
                        "mean_output_minus_reference_step_ms": float(np.mean(offsets)),
                        "last_note_onset_to_last_ac_step_ms": {"output": ra["steps"][-1]["time_ms"] - edges_a[-1] * 1000,
                                                               "reference": rb["steps"][-1]["time_ms"] - edges_b[-1] * 1000},
                        "reference_late_tail": {"window_seconds": [.240, .280], "dc_mean": float(tail.mean()),
                                                "ac_rms_about_window_mean": float(tail.std()),
                                                "exponential_decay_time_constant_ms": float(-1000 / slope),
                                                "log_decay_r_squared": float(1 - np.sum((actual - predicted) ** 2) / np.sum((actual - actual.mean()) ** 2)),
                                                "warning": "The late tail is predominantly a decaying DC/filter transient, not proof of a held PSG note."}},
            "stable_last_note_spectral_proxies": {"window_seconds": [.112, .145],
                                                  "output": spectrum(crop(a, .112, .145)),
                                                  "reference": spectrum(crop(b, .112, .145))}}


def pallet_detail(a, b):
    windows = []
    for start, end in [(0, 1), (0, 43.83544921875), (43.83544921875, 87.6708984375), (0, len(a) / RATE)]:
        windows.append({"window_seconds": [start, end], **level_comparison(crop(a, start, end), crop(b, start, end))})
    aa, bb = crop(a, .100, .300), crop(b, .100, .300)
    # The same pitch region may contain PCM harmonics. No blind source separation claim.
    psg_regions = []
    for name, band, harmonic in [("square1 initial key88 third harmonic", (3905, 3945), 3),
                                 ("square2 initial key45 fundamental", (103, 117), 1)]:
        pa, pb = peak_frequency(aa, *band), peak_frequency(bb, *band)
        psg_regions.append({"description": name, "output": pa, "reference": pb,
                            "harmonic_number": harmonic,
                            "inferred_output_fundamental_hz": pa["peak_hz"] / harmonic,
                            "inferred_reference_fundamental_hz": pb["peak_hz"] / harmonic,
                            "inferred_fundamental_difference_hz": (pa["peak_hz"] - pb["peak_hz"]) / harmonic})
    envelope_bin = 480
    n = len(a) // envelope_bin * envelope_bin
    ea = np.sqrt(np.mean(a[:n].reshape(-1, envelope_bin, 2) ** 2, axis=(1, 2)))
    eb = np.sqrt(np.mean(b[:n].reshape(-1, envelope_bin, 2) ** 2, axis=(1, 2)))
    return {"level_windows": windows,
            "ten_ms_rms_envelope_correlation_fixed_alignment": float(np.corrcoef(ea, eb)[0, 1]),
            "psg_frequency_regions": {"window_seconds": [.100, .300], "regions": psg_regions,
                                      "warning": "Mixed PCM/PSG spectrum; regional peaks alone cannot prove source separation or isolated voice gain. The square1 fundamental near 1310.7 Hz is obscured by a stronger ~1321.7 Hz PCM component; report its resolved ~3932.2 Hz third-harmonic region instead."},
            "whole_output_spectral_proxies": {"output": spectrum(a), "reference": spectrum(b)},
            "note_length_release_limit": "Pallet is a six-track looping mix. Individual PCM and PSG gates/releases cannot be uniquely measured from these two mixed WAVs. SELECT supplies the isolated PSG timing check; Pallet ends while playing, so its file ending is not a natural release measurement."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=pathlib.Path, default=ROOT / "results")
    parser.add_argument("--reference-dir", type=pathlib.Path, default=ROOT.parent / "runtime-audio-study")
    parser.add_argument("--json-out", type=pathlib.Path, default=ROOT / "results/phase2-baseline-analysis.json")
    args = parser.parse_args()
    result = {"schema": 1, "profile": "frozen phase1 modern-dry experimental baseline",
              "sample_rate": RATE, "units": "floating full scale, PCM16 divided by 32768",
              "software": {"python": platform.python_version(), "numpy": np.__version__, "scipy": scipy.__version__},
              "method": {"alignment": "One frozen phase1 first-second correlation lag per pair. Applied unchanged to the entire stereo file and all diagnostic windows; no new delay search, per-note phase alignment or time warping.",
                         "amplitude_gain": "RMS or local DC-removed RMS ratio; not fitted waveform least squares. No gain is applied to the waveforms.",
                         "spectra": "Hann-window Welch power, arithmetic mean across stereo-channel powers, constant detrending per segment.",
                         "limits": "No listening test, new reference rendering, psychoacoustic score, reference-purity claim or bit-perfect claim. Original-driver stripped bootstrap is the named comparison target."},
              "pairs": {}}
    for name, reference in [("select", "se-stripped.wav"), ("pallet", "bgm-stripped.wav")]:
        path_a, path_b = args.results_dir / (name + ".wav"), args.reference_dir / reference
        a, b = read_wav(path_a), read_wav(path_b)
        pair = {"output_file": metadata(path_a, a), "reference_file": metadata(path_b, b)}
        lag = FROZEN_LAGS[name]
        a, b = a[max(lag, 0):], b[max(-lag, 0):]
        n = min(len(a), len(b))
        a, b = a[:n], b[:n]
        pair.update({"fixed_lag_samples": lag, "fixed_lag_ms": lag / 48,
                     "aligned_samples": n, "aligned_seconds": n / RATE,
                     "levels": level_comparison(a, b),
                     "waveform": {"rmse_pcm16": rms(a - b) * 32768,
                                  "correlation": float(np.corrcoef(a.ravel(), b.ravel())[0, 1])}})
        pair["detail"] = select_detail(a, b, args.results_dir / "select.wav.events.txt") if name == "select" else pallet_detail(a, b)
        result["pairs"][name] = pair
    args.json_out.parent.mkdir(parents=True, exist_ok=True)
    args.json_out.write_text(json.dumps(result, indent=2, allow_nan=False) + "\n")
    print(args.json_out)
    for name, pair in result["pairs"].items():
        levels = pair["levels"]
        print(f"{name}: level {levels['output_minus_reference_db']:.3f} dB; RMS-match gain {levels['rms_match_gain_for_output']:.4f}; L/R difference {levels['left_right_balance_difference_db']:.3f} dB")
    release = result["pairs"]["select"]["detail"]["release"]
    print(f"SELECT release staircase offset {release['mean_output_minus_reference_step_ms']:.3f} ms (output minus reference)")


if __name__ == "__main__":
    main()
