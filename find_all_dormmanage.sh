#!/bin/bash
# find_all_dormmanage.sh - Search for ALL DormManage.Admin.exe on this machine and connected drives
echo "=== Searching local drives for DormManage.Admin.exe ==="
echo ""

# Common locations
for path in \
    "C:/DormManage" \
    "D:/DormManage" \
    "E:/DormManage" \
    "C:/Program Files/DormManage" \
    "C:/inetpub/wwwroot/DormManage" \
    "D:/publish" \
    "D:/publish-final" \
    "D:/release"; do
    if [ -d "$path" ]; then
        echo "[FOUND DIR] $path"
        ls "$path"/*.exe 2>/dev/null | head -3
        echo ""
    fi
done

echo ""
echo "=== Searching all mounted drives (top 3 levels only) ==="
for drive in C D E F G; do
    mount="/$drive"
    if [ -d "$mount" ]; then
        echo "Scanning drive $drive..."
        find "$mount" -maxdepth 3 -name "DormManage.Admin.exe" 2>/dev/null | head -5
    fi
done

echo ""
echo "=== Checking network/UNC paths ==="
ls //localhost/ 2>/dev/null
ls //127.0.0.1/ 2>/dev/null

echo ""
echo "=== Current process list (filter DormManage) ==="
tasklist 2>/dev/null | grep -i "DormManage" || echo "No DormManage processes found"

echo ""
echo "=== Listening ports (5001/5100/5099) ==="
netstat -ano 2>/dev/null | grep ":5001\|:5100\|:5099" | head -10 || echo "No relevant ports listening"