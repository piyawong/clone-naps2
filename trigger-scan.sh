#!/bin/bash
# Trigger scan by simulating keyboard shortcut in NAPS2

# เปิด NAPS2 ถ้ายังไม่เปิด
if ! pgrep -f "NAPS2.app" > /dev/null; then
    echo "🚀 Opening NAPS2..."
    open /Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app
    sleep 3
fi

echo "📄 Triggering scan..."

# ส่ง keyboard shortcut ไป NAPS2 (Cmd+B = Scan with default profile)
osascript <<EOF
tell application "System Events"
    tell process "NAPS2"
        keystroke "b" using command down
    end tell
end tell
EOF

echo "✅ Scan triggered!"
