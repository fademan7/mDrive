> Historical 0.5.7 design. In 0.5.14 the Keys UI and production BB/Diff keyboard output were removed. Read-only queries and manual recommendations remain. See README for current support.

# Wendy 0.5.7

## UI

Android 0.5.8 supersedes the Race/Menu layout: Race mode, shortcut options,
the bottom switch and standalone PTT are removed at the user's request.
Brake/Throttle retain their saved widths and full-height surfaces.
ABXY/D-pad/LB/RB/Back/Start and right analog stick are always present.
Wendy manual PTT observes the existing LB edges without consuming or changing
its gamepad bit. Wendy OFF/AUTO does not start manual recognition from LB.
No new gamepad mapping is required. Layout editing and QR scanning remain
separate operations that safely release controls.
Header: Wendy state, flag square, optional summary, mDrive menu.
Two-column options; QR immediately scans, no manual pairing/test-vibration UI.
Wendy ON uses English AUTO system recognition; Use PTT/AUTO selects mode.
No on-device selector. Explanations/creator links are in About.

## BB/Diff direct shortcuts: opt-in, actual F1 testing pending

1. In F1 25 keyboard **MFD Shortcuts**, assign four DISTINCT keys to Brake Bias
   Increase/Decrease and Differential Increase/Decrease. Defaults are unassigned.
2. Verify a short press changes BB by **1 point**, on-throttle Diff by **5 points**.
   Remove conflicting bindings. These must be DIRECT shortcuts, not MFD arrows.
3. Receiver **Keys**: choose the same four A–Z/F1–F12 keys, confirm they were
   tested in F1, and Enable. Default OFF after each Receiver restart.
4. F1_25 must be foreground, resumed on track, with fresh session/lap/setting
   telemetry. No focus stealing. Test in practice before racing.

Supported exact requests:

- “Set brake bias to 54.” / “Move brake bias forward one.”
- “Set the diff to 55.” / “Reduce differential by five.”
- English tens such as “fifty four” are accepted. Mixed/negative/low-confidence
  numbers, unsupported steps, or changes over 20 points are rejected, not rounded.

One 35ms keyboard pulse, key-up in finally, then NEW matching telemetry before
any further pulse. Native errors, focus loss, disable, stale data, or conflicting
manual changes stop it. No virtual-pad button stealing or blind MFD macro.
1.5s step verification / 8s total ACTION limits do not change controller timing.
Requested → Executing → Confirmed / Unconfirmed / Rejected. No ACK means no
success claim and no retry. Already-matching fresh targets are described as such.
Session/flashback/controller reconnect/Wendy reconnect discard pending actions
and approvals. No replay queue. Mixed keyboard/pad behavior needs real F1 testing.

## Unsupported actual actions

Box box / OK box box / Box this lap: reminder + PIT THIS LAP HUD only; actual
action state Rejected. No verified direct binding/request acknowledgement has
been established. Entering pit lane is NOT proof we sent a request.
Next-stop tyres/front wing: rejected; fitted tyres/current setup do not establish
the next-stop selection. No blind macro, OCR, screenshot or guessed success.
Full optimal strategy and automatic garage manipulation remain unsupported.

## Advisor

Collect repeated DRIVER-REPORTED rear exit instability/oversteer/traction/spin,
entry understeer and front locking across three completed clean laps. Require
matching throttle/steering or braking phase exposure, >=85% track coverage,
stable setup/compound/weather, fresh data, no damage, wear <55%, inner tyre
temperature 75–110 C. These are conservative heuristics, not universal F1 limits.
Ordinary control inputs alone are NOT interpreted as measured instability.

Rear-exit reports can propose a single Diff -5 trial. Agree/Do it/Go ahead uses
the SAME optional verified executor; keys disabled means Rejected, not applied.
Entry/front locking produces garage/driving review, not invented numeric fixes.
Explicitly approved garage reviews are saved. Existing overheating/uneven-wear
reviews remain. Approval expires at 30s or context/setup/condition/reconnect
change. A trial consumes evidence and imposes BOTH 3 laps and 180s before another.
Proactive proposals require repeated reported evidence and a straight/quiet
opportunity. Autonomous slip detection/vehicle-dynamics diagnosis is unsupported.

## Tyre range / pit guidance

Use 3–5 recent clean completed-lap per-wheel wear deltas. Reset/suppress on new
generation, compound/set-age rollback, setup/weather change, >2s data gap,
invalid lap, flags/pit or incomplete coverage. Require positive wear and stable
spread <=65% of median. Estimate distance to a **70% wear planning threshold**,
NOT puncture probability or optimal strategy. Use the most limiting wheel;
widen rate bounds 25%, or 45% outside the conservative temperature band.
Floor/ceil a range at least two laps wide; suppress low-confidence/very long values.
PIT THIS LAP priority: wear >=75% or supported near-limit range, more race distance
remaining, or explicit pit reminder. Other summaries: FW damage >=10%, nearby
game rain forecast, current advisor proposal and tyre range. Stale data clears HUD.
Pit guidance never implies a game action executed or an optimal rejoin strategy.

## Verification / sources

See `artifacts/validation/UI_WENDY_057.md` for synthetic/build/phone/F1 distinctions.
SafetyGate, sender deadlines and gamepad worker unchanged; only a rare atomic
controller reconnect notification invalidates optional Wendy actions.
No new AI inference, OCR or screen capture is added by this change.

[EA UDP v3](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf),
[EA MFD keyboard shortcuts](https://www.ea.com/en/games/f1/f1-25/f1-25-controls-hub/mfd-shortcuts-controls-keyboard-pc).
