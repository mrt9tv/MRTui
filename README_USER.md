# 🏁 iRacing Telemetry Overlay - Ready to Use!

## Quick Start (3 Easy Steps)

### 1️⃣ Launch the Telemetry Overlay
**Double-click:** `START_TELEMETRY.bat`

A green console window will appear with:
```
===========================================
iRacing Telemetry - Live Data
===========================================

Status: 🔄 Connecting...
Waiting for iRacing...
```

### 2️⃣ Start iRacing
- Open iRacing normally
- Enter **any session** (Test Drive, Practice, Race, etc.)
- You'll see the status change to: ✅ Connected

### 3️⃣ Drive and Watch!
The overlay will show live data:
- **Speed** in km/h and mph
- **RPM** and **Gear**
- **Throttle, Brake, Clutch** (visual bars)
- **Steering angle**
- **Lap number and Position**

**That's it!** The data updates automatically while you race.

---

## 📺 Where is the Display?

Right now, the telemetry shows in a **console window** (black/green text window).

### Positioning Tips:
- **Dual Monitor:** Drag the console to your second screen
- **Single Monitor:** Minimize it or place it at screen edge
- **Alt+Tab:** Switch between iRacing and the overlay window

### Coming in MVP 2:
We'll add a **transparent WPF overlay** that sits on top of your game screen!

---

## 🎮 What You Can See Right Now

**Telemetry Data:**
- Speed (m/s, km/h, mph)
- Engine RPM
- Current Gear (R, N, 1-6)
- Lap Number
- Position in Class

**Input Display:**
- Throttle Position (0-100%)
- Brake Pressure (0-100%)
- Clutch Position (0-100%)
- Steering Wheel Angle (radians)

**Connection Info:**
- Connection Status
- Uptime
- Update Rate (Hz)
- Total Updates Received

---

## 🛠️ Troubleshooting

### "Not Connecting" Issue
✅ **Solution:** Make sure iRacing is **in a session**, not just at the main menu
- Go to Test Drive or start a Practice session
- The overlay will auto-connect once you're on track

### "Can't See the Window" Issue  
✅ **Solution:** Check your taskbar - the window might be behind iRacing
- Alt+Tab to find "iRacing Telemetry Overlay"
- Drag it to a visible location

### "Application Won't Start" Issue
✅ **Solution:** Ensure you have the .NET 8 runtime (or use the self-contained .exe)
- The `build/iRacingOverlay.Core.exe` is self-contained (no .NET needed)
- Just double-click START_TELEMETRY.bat

---

## 📂 Files You Need

### To Run:
- `START_TELEMETRY.bat` ← **USE THIS**
- `build/iRacingOverlay.Core.exe` ← The actual program

### Optional:
- `QUICKSTART.md` ← Detailed guide
- `src/` ← Source code (if you want to modify)

---

## ⚡ Performance

**Resource Usage:**
- Memory: ~45 MB
- CPU: < 2%  
- No network traffic (uses iRacing shared memory)

**Update Rate:**
- Receives data at ~25-60 Hz from iRacing
- Display refreshes at 1 Hz (once per second)

---

## 🎯 Current Status

### ✅ MVP 1: COMPLETE AND WORKING
- Real-time connection to iRacing
- Live telemetry data
- All core vehicle metrics
- Input visualization
- Stable and performant

### 🔜 Next: MVP 2 (Coming Soon)
- Transparent WPF overlay window
- Positioned on top of game
- Customizable layout
- Better visual design

---

## ❓ Need Help?

**Check these first:**
1. Is iRacing running?
2. Are you in a session (not main menu)?
3. Did you start the overlay (START_TELEMETRY.bat)?

**Still not working?**
- Close iRacing
- Close the overlay (Ctrl+C)
- Restart both: overlay first, then iRacing

---

**Enjoy your real-time iRacing telemetry!** 🏁
