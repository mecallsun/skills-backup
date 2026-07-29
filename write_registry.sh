#!/bin/bash
# write_registry.sh - Write license info to HKLM registry for TrayApp
# Usage: ./write_registry.sh <CDKEY> <LTDName> [RegDate]

CDKEY="$1"
LTDNAME="$2"
REGDATE="${3:-2027-12-31}"  # Default to far future date for testing

if [ -z "$CDKEY" ] || [ -z "$LTDNAME" ]; then
    echo "Usage: $0 <CDKEY> <LTDName> [RegDate]"
    echo "Example: $0 ABCDE-FGHIJ-KLMNO-PQRST-UVWXY 'Test Company' 2027-12-31"
    exit 1
fi

echo "=== Writing registry ==="
echo "HKLM\\Software\\JINGE\\DormManage\\License"
echo "  CDKEY = $CDKEY"
echo "  LTDName = $LTDNAME"
echo "  RegDate = $REGDATE"
echo "  UseTimes = 0"

# Use reg.exe (more reliable in Git Bash)
/c/Windows/System32/reg.exe ADD "HKLM\Software\JINGE\DormManage" //f 2>&1
/c/Windows/System32/reg.exe ADD "HKLM\Software\JINGE\DormManage\License" //f 2>&1

# Write CDKEY (REG_SZ)
/c/Windows/System32/reg.exe ADD "HKLM\Software\JINGE\DormManage\License" //v CDKEY //t REG_SZ //d "$CDKEY" //f 2>&1

# Write LTDName (REG_SZ)
/c/Windows/System32/reg.exe ADD "HKLM\Software\JINGE\DormManage\License" //v LTDName //t REG_SZ //d "$LTDNAME" //f 2>&1

# Write RegDate (REG_SZ)
/c/Windows/System32/reg.exe ADD "HKLM\Software\JINGE\DormManage\License" //v RegDate //t REG_SZ //d "$REGDATE" //f 2>&1

# Write UseTimes (REG_DWORD)
/c/Windows/System32/reg.exe ADD "HKLM\Software\JINGE\DormManage\License" //v UseTimes //t REG_DWORD //d 0 //f 2>&1

echo ""
echo "=== Verify ==="
/c/Windows/System32/reg.exe QUERY "HKLM\Software\JINGE\DormManage\License" 2>&1