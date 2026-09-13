$root = "C:\Users\Mecall\.claude\skills"
$summary = @()
$details = @{}

if (Test-Path $root) {
    Get-ChildItem -Path $root -Directory | Sort-Object Name | ForEach-Object {
        $catName = $_.Name
        $skills = Get-ChildItem -Path $_.FullName -Directory -ErrorAction SilentlyContinue | Sort-Object Name
        $count = if ($skills) { $skills.Count } else { 0 }
        $summary += [PSCustomObject]@{
            Category = $catName
            Count = $count
        }
        if ($skills) {
            $details[$catName] = @()
            foreach ($s in $skills) {
                $details[$catName] += $s.Name
            }
        }
    }
}

$agentsRoot = "C:\Users\Mecall\.claude\.agents\skills"
if (Test-Path $agentsRoot) {
    $agentsSkills = Get-ChildItem -Path $agentsRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name
    $summary += [PSCustomObject]@{
        Category = ".agents/skills"
        Count = if ($agentsSkills) { $agentsSkills.Count } else { 0 }
    }
    if ($agentsSkills) {
        $details[".agents/skills"] = @()
        foreach ($s in $agentsSkills) {
            $details[".agents/skills"] += $s.Name
        }
    }
}

# 保存为 JSON 供后续步骤使用
$totalCount = ($summary | Measure-Object -Property Count -Sum).Sum
$output = @{
    summary = $summary
    details = $details
    total = $totalCount
}
$output | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 "C:\Users\Mecall\.skills-pool\scan_step1_result.json"

Write-Host "=== Step 1 Result ==="
Write-Host ("Total skill dirs: " + $totalCount)
Write-Host ""
Write-Host "=== Categories ==="
$summary | Format-Table -AutoSize
Write-Host ""
Write-Host "Result saved to: C:\Users\Mecall\.skills-pool\scan_step1_result.json"