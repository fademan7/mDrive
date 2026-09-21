# Wendy English conversation reference — 0.5.3

Use **Options → Wendy → AUTO: system (recommended)** on this Fold5. Its on-device service exists but an English on-device model was not installed during device verification. AUTO hides PTT; select **Push to talk** to restore it. No wake word is needed. System recognition may use its provider's internet service; on-device mode never silently falls back to cloud. The microphone pauses while Wendy speaks and when the app loses foreground focus.

Try **“Radio check.”** Wendy answering “Loud and clear” verifies a spoken round trip. A race greeting only verifies telemetry/downlink/TTS, not microphone recognition. IDLE briefly between automatic listening sessions is normal. Settings show link status, microphone starts/ready/errors, model availability and last fault.

## Read-only questions

| Topic | English examples | Meaning / limits |
|---|---|---|
| Radio/help | Radio check. Can you hear me? What can I ask? | No race telemetry required. |
| Wear | How are my tyres? Front left tyre wear. | Worst wheel by default; named wheel supported. |
| Temperature | Tyre temperature. Front right tyre temperature. Brake temperatures. Engine temperature. | Celsius; named tyre reports inner and surface. |
| Pressure/age/compound | Tyre pressures. Front left tyre pressure. How old are my tyres? What tyres am I on? | Actual PSI, age in laps; F1 soft/medium/hard/intermediate/wet where known. |
| Gaps | What's the gap ahead? How close is the car behind? | Seconds to adjacent race position on the same lap; not fabricated when missing. |
| Leader | What's the gap to the leader? | Same-lap leader gap, or confirmation that you are leading. |
| Fuel | How much fuel do I have? | Kilograms and the game's MFD lap estimate, not an invented strategy range. |
| ERS | What's my ERS? ERS mode. | Stored megajoules; none/medium/hotlap/overtake. No universal battery-percent assumption. |
| Damage | Any damage? Front wing damage. Left front wing damage. Rear wing damage. Floor damage. Diffuser damage. Sidepod damage. Gearbox damage. Engine damage. | Measured component percentages; generic answer summarizes monitored components. |
| Driving values | How fast am I going? What gear am I in? Engine RPM. | km/h, gear, RPM; not an optimal target. |
| DRS | Is DRS open? Can I use DRS? | Current open/available state or activation distance. |
| Race order/laps | What's my position? What lap am I on? How many laps left? | Remaining laps include the current lap. |
| Lap data | Last lap time. Sector times. Is my lap valid? | Actual last-lap seconds, current sector/completed current-lap sectors, validity. |
| Coaching | How was my last lap? How can I improve? Coach me. | Existing first-clean-lap reference, later clean-lap comparisons. Not a physics-optimal racing coach. |
| Conditions | What's the weather? Track temperature. Air temperature. | Current weather/temperature. |
| Forecast | Is it going to rain? Weather forecast. When will the rain start? | The game's current-session forecast, time offset in minutes and rain probability. Approximate forecasts are identified. Not an independent prediction. |
| Session | Time left. | Game session timer, not an inferred finish time. |
| Flags | What flag is out? Are we under yellow? | GREEN/YELLOW/RED/BLUE/SC/VSC/CHECKERED where available. |
| Penalties | Any penalties? Track limits warnings? | Time penalties/warnings/unserved drive-through/stop-go. |
| Pit information | Do I need pit in? Should I box this lap? Pit window. Pit status. How many pit stops have I made? Pit limiter. Pit speed limit. | Conservative damage/wear/game-pit-window advice; limiter state and speed limit. |
| Setup readback | What's my brake bias? What's the diff set to? Wing settings. | Current bias/on/off-throttle diff/front and rear wing values; no write. |

Freshness is checked per packet group. Missing/stale data is explicitly unavailable. Unsupported formulas/compounds are not guessed. Ask one topic at a time; arbitrary English recognition/classification is not guaranteed.

## Plans and driver feedback

- **Box box / Box this lap / I want to pit**: current-lap reminder only. **Stay out / Cancel pit stop** cancels it. No in-game pit request is sent. Clear, sufficiently confident recognition is required.
- Handling feedback such as **“The rear feels loose on exit”**, **“The car doesn't want to turn in”**, **“I keep locking the fronts”**, **“My tyres are overheating”** uses the existing conservative evidence checks. Wendy does not invent a setup recommendation without supporting telemetry.
- **Sounds good / Go ahead** saves a pending garage-review recommendation, not a live game setup change. **No, leave it** rejects it. Approvals expire and are never replayed after reconnect.
- **Set brake bias to 54 / Set differential to 55 / Soft tyres next stop / Increase front wing by one** remain unsupported game mutations. UDP provides observations, not a reliable general menu-command channel. They are not falsely acknowledged as completed.

## Proactive speech

Flags, SC/VSC, significant wear/damage, low fuel, changing weather, pit-related events, penalties, sustained tyre heat, following traffic, lap reports and conservative corner comparisons retain cooldowns. Twenty race greeting variants are selected without consecutive repeats, once per session during the receiver lifetime. Safety alerts take priority. Reconnection/flashback does not repeat a greeting for the same session. Engineer OFF has no optional telemetry/model service.

Not implemented: all fields in the UDP specification, driver-name/teammate strategy conversations, independent weather prediction, complete strategy optimization, automatic game menu changes, always-on background microphone, or a guarantee against Wi-Fi/provider/driver outages.
