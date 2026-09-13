@echo off
echo === Verify Junction ===
dir /AL "C:\Users\Mecall\.claude\skills"
echo.
echo === Test Read ===
dir "C:\Users\Mecall\.claude\skills\07-研究调研\r2r\SKILL.md"
echo.
echo === Count Categories ===
dir /B "C:\Users\Mecall\.claude\skills"
echo.
echo === Total Skills (sample first 10) ===
dir /B "C:\Users\Mecall\.claude\skills\07-研究调研"