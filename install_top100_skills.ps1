# TOP 100 Skills Installation Script
# Save as: install_top100_skills.ps1
# Run with: powershell -ExecutionPolicy Bypass -File install_top100_skills.ps1

$skillsDir = "C:\Users\Mecall\.claude\skills"
$logFile = "$skillsDir\install_log.txt"

function Install-Skill {
    param($name, $package)
    Write-Host "Installing $name..." -NoNewline
    try {
        $result = npx skills add $package 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host " OK" -ForegroundColor Green
        } else {
            Write-Host " FAILED" -ForegroundColor Yellow
            Add-Content $logFile "$name : FAILED"
        }
    } catch {
        Write-Host " ERROR: $_" -ForegroundColor Red
        Add-Content $logFile "$name : ERROR $_"
    }
    Start-Sleep -Milliseconds 500
}

# S - 置顶必装
Install-Skill "find-skills" "https://github.com/vercel-labs/skills --skill find-skills"

# A - AI工作流 (10 skills)
Install-Skill "firecrawl-crawler" "mendableai/firecrawl"
Install-Skill "n8n-workflow-automation" "n8n-io/n8n"
Install-Skill "playwright-web-agent" "skills-sh/playwright-agent"
Install-Skill "openai-assistants-api" "skills-sh/openai-assistants"
Install-Skill "langchain-agent-builder" "langchain-ai/langchain-skill"
Install-Skill "browser-use-automation" "skills-sh/browser-use"
Install-Skill "make-dotcom-flows" "skills-sh/make-automation"
Install-Skill "mcp-server-template" "modelcontextprotocol/servers"
Install-Skill "zapier-ai-actions" "skills-sh/zapier-ai"
Install-Skill "multi-agent-orchestration" "skills-sh/multi-agent"

# B - 数据分析 (10 skills)
Install-Skill "pandas-analysis-expert" "skills-sh/pandas-analyst"
Install-Skill "sql-analytics-pro" "skills-sh/sql-analytics"
Install-Skill "excel-power-automation" "skills-sh/excel-automation"
Install-Skill "plotly-dash-dashboard" "skills-sh/plotly-dashboard"
Install-Skill "metabase-bi-copilot" "skills-sh/metabase-bi"
Install-Skill "weekly-report-generator" "skills-sh/report-generator"
Install-Skill "data-quality-pipeline" "skills-sh/data-quality"
Install-Skill "colab-data-scientist" "skills-sh/jupyter-colab"
Install-Skill "powerbi-smart-query" "skills-sh/power-bi"
Install-Skill "airtable-no-code-analyst" "skills-sh/airtable-analyst"

# C - A股量化 (16 skills)
Install-Skill "a-stock-data" "simonlin1212/a-stock-data"
Install-Skill "stock-analysis-skill" "https://github.com/liusai0820/stock-analysis-skill --skill stock-analysis"
Install-Skill "china-stock-analysis" "https://github.com/sugarforever/01coder-agent-skills --skill china-stock-analysis"
Install-Skill "ashare-ai" "410417122/ashare-ai"
Install-Skill "analyse-skills" "viekai/analyse-skills"
Install-Skill "china-stock-analyst" "https://github.com/wjt0321/china-stock-analyst"
Install-Skill "fin-modeling-dcf" "skills-sh/fin-modeling"
Install-Skill "backtesting-framework" "skills-sh/backtesting"
Install-Skill "stock-analyst" "https://github.com/chengzuopeng/stock-sdk-mcp --skill stock-analyst"
Install-Skill "capm-factor-analyzer" "skills-sh/capm-model"
Install-Skill "risk-metrics-portfolio" "skills-sh/risk-metrics"
Install-Skill "arima-price-forecast" "skills-sh/arima-forecast"
Install-Skill "behavioral-finance-cn" "skills-sh/behavioral-finance"
Install-Skill "multiagent-stock-research" "chenhab03/multiagent-stock-research"
Install-Skill "eastmoney-data-scraper" "skills-sh/finance-scraper"
Install-Skill "dragonscope-ashare" "MrDeerLei/dragonscope-ashare"

Write-Host "`nInstallation complete! Check $logFile for details."