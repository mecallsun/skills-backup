# AI Skills TOP100 安装脚本
# 安装所有107个技能到分类目录

$ErrorActionPreference = "SilentlyContinue"
$SkillsDir = "$HOME/.claude/skills"
$LogFile = "$SkillsDir/install_top100_result.log"

# 分类目录定义
$Categories = @{
    "S" = "00-置顶必装"
    "A" = "01-AI工作流"
    "B" = "02-数据分析"
    "C" = "03-A股量化"
    "D" = "04-VibeCoding"
    "E" = "05-内容创作"
    "F" = "06-职场效率"
    "G" = "07-研究调研"
    "H" = "08-基础设施"
    "I" = "09-UIUX"
    "J" = "10-知识库"
    "K" = "11-安全防护"
}

function Write-Log {
    param($message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $message" | Tee-Object -FilePath $LogFile -Append
}

function Ensure-CategoryDir {
    param($catKey)
    $dir = "$SkillsDir/$($Categories[$catKey])"
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Log "创建目录: $dir"
    }
    return $dir
}

# 创建所有分类目录
foreach ($cat in $Categories.Values) {
    Ensure-CategoryDir ($Categories.Keys | Where-Object { $Categories[$_] -eq $cat })
}

Write-Log "=== AI Skills TOP100 开始安装 ==="

# 安装命令定义 (name -> install command)
$InstallCommands = @{
    # S 类
    "find-skills" = "npx skills add https://github.com/vercel-labs/skills --skill find-skills"

    # A 类
    "firecrawl-crawler" = "npx skills add mendableai/firecrawl"
    "n8n-workflow-automation" = "npx skills add n8n-io/n8n"
    "playwright-web-agent" = "npx skills add skills-sh/playwright-agent"
    "openai-assistants-api" = "npx skills add skills-sh/openai-assistants"
    "langchain-agent-builder" = "npx skills add langchain-ai/langchain-skill"
    "browser-use-automation" = "npx skills add skills-sh/browser-use"
    "make-dotcom-flows" = "npx skills add skills-sh/make-automation"
    "mcp-server-template" = "npx skills add modelcontextprotocol/servers"
    "zapier-ai-actions" = "npx skills add skills-sh/zapier-ai"
    "multi-agent-orchestration" = "npx skills add skills-sh/multi-agent"

    # B 类
    "pandas-analysis-expert" = "npx skills add skills-sh/pandas-analyst"
    "sql-analytics-pro" = "npx skills add skills-sh/sql-analytics"
    "excel-power-automation" = "npx skills add skills-sh/excel-automation"
    "plotly-dash-dashboard" = "npx skills add skills-sh/plotly-dashboard"
    "metabase-bi-copilot" = "npx skills add skills-sh/metabase-bi"
    "weekly-report-generator" = "npx skills add skills-sh/report-generator"
    "data-quality-pipeline" = "npx skills add skills-sh/data-quality"
    "colab-data-scientist" = "npx skills add skills-sh/jupyter-colab"
    "powerbi-smart-query" = "npx skills add skills-sh/power-bi"
    "airtable-no-code-analyst" = "npx skills add skills-sh/airtable-analyst"

    # C 类
    "a-stock-data" = "npx skills add simonlin1212/a-stock-data"
    "stock-analysis" = "npx skills add https://github.com/liusai0820/stock-analysis-skill --skill stock-analysis"
    "china-stock-analysis" = "npx skills add https://github.com/sugarforever/01coder-agent-skills --skill china-stock-analysis"
    "ashare-ai" = "npx skills add 410417122/ashare-ai"
    "analyse-skills" = "npx skills add viekai/analyse-skills"
    "china-stock-analyst" = "npx skills add https://github.com/wjt0321/china-stock-analyst"
    "fin-modeling-dcf" = "npx skills add skills-sh/fin-modeling"
    "backtesting-framework" = "npx skills add skills-sh/backtesting"
    "stock-analyst" = "npx skills add https://github.com/chengzuopeng/stock-sdk-mcp --skill stock-analyst"
    "capm-factor-analyzer" = "npx skills add skills-sh/capm-model"
    "risk-metrics-portfolio" = "npx skills add skills-sh/risk-metrics"
    "arima-price-forecast" = "npx skills add skills-sh/arima-forecast"
    "behavioral-finance-cn" = "npx skills add skills-sh/behavioral-finance"
    "multiagent-stock-research" = "npx skills add chenhab03/multiagent-stock-research"
    "eastmoney-data-scraper" = "npx skills add skills-sh/finance-scraper"
    "dragonscope-ashare" = "npx skills add MrDeerLei/dragonscope-ashare"

    # D 类
    "shadcn-ui" = "npx skills add shadcn-ui/shadcn"
    "nextjs-app-router" = "npx skills add vercel/nextjs-skill"
    "vercel-ai-sdk" = "npx skills add vercel/ai-sdk-skill"
    "drizzle-orm" = "npx skills add drizzle-team/drizzle-skill"
    "supabase-realtime" = "npx skills add supabase/supabase-skill"
    "tailwind-component-system" = "npx skills add tailwindlabs/tailwind-skill"
    "fastapi-python-backend" = "npx skills add tiangolo/fastapi-skill"
    "python-scripting-toolkit" = "npx skills add skills-sh/python-scripts"
    "prisma-database-expert" = "npx skills add prisma/prisma-skill"
    "react-hooks-patterns" = "npx skills add skills-sh/react-hooks"

    # E 类
    "short-video-script-ai" = "npx skills add skills-sh/short-video-script"
    "content-marketing-suite" = "npx skills add skills-sh/content-marketing"
    "long-form-article-writer" = "npx skills add skills-sh/long-form-writer"
    "seo-content-optimizer" = "npx skills add skills-sh/seo-optimizer"
    "brand-voice-designer" = "npx skills add skills-sh/brand-voice"
    "xiaohongshu-creator" = "npx skills add skills-sh/xiaohongshu"
    "email-outreach-writer" = "npx skills add skills-sh/email-outreach"
    "social-media-calendar" = "npx skills add skills-sh/social-calendar"

    # F 类
    "meeting-notes-extractor" = "npx skills add skills-sh/meeting-notes"
    "executive-deck-builder" = "npx skills add skills-sh/exec-presentation"
    "okr-kr-designer" = "npx skills add skills-sh/okr-designer"
    "performance-review-ai" = "npx skills add skills-sh/performance-review"
    "contract-risk-reviewer" = "npx skills add skills-sh/contract-review"
    "decision-tree-analyzer" = "npx skills add skills-sh/decision-matrix"
    "job-description-screener" = "npx skills add skills-sh/hiring-screener"
    "email-smart-reply" = "npx skills add skills-sh/email-assistant"
    "sop-documentation-ai" = "npx skills add skills-sh/sop-generator"
    "project-risk-tracker" = "npx skills add skills-sh/project-tracker"

    # G 类
    "perplexity-style-researcher" = "npx skills add skills-sh/deep-research"
    "competitor-radar" = "npx skills add skills-sh/competitor-intel"
    "market-sizing-report" = "npx skills add skills-sh/market-research"
    "industry-news-monitor" = "npx skills add skills-sh/news-monitor"
    "web-data-extractor" = "npx skills add skills-sh/web-extractor"
    "customer-voice-miner" = "npx skills add skills-sh/customer-insights"
    "supply-chain-researcher" = "npx skills add skills-sh/supply-chain-research"
    "policy-regulation-tracker" = "npx skills add skills-sh/policy-tracker"
    "academic-research-reader" = "npx skills add skills-sh/paper-reader"
    "linkedin-talent-scout" = "npx skills add skills-sh/linkedin-scout"

    # H 类
    "docker-compose-expert" = "npx skills add skills-sh/docker-compose"
    "github-actions-pipeline" = "npx skills add skills-sh/github-actions"
    "grafana-monitoring" = "npx skills add skills-sh/grafana-dashboard"
    "kubernetes-helm-deployer" = "npx skills add skills-sh/kubernetes-helm"
    "terraform-aws-infra" = "npx skills add skills-sh/terraform-aws"
    "postgresql-optimizer" = "npx skills add skills-sh/postgres-optimizer"
    "linux-sysadmin-ai" = "npx skills add skills-sh/linux-admin"
    "redis-caching-expert" = "npx skills add skills-sh/redis-cache"

    # I 类
    "product-prd-writer" = "npx skills add skills-sh/product-prd"
    "figma-component-ai" = "npx skills add skills-sh/figma-ai"
    "landing-page-optimizer" = "npx skills add skills-sh/landing-optimizer"
    "wireframe-to-prototype" = "npx skills add skills-sh/wireframe-gen"
    "ux-heuristic-auditor" = "npx skills add skills-sh/ux-auditor"
    "design-token-system" = "npx skills add skills-sh/design-tokens"
    "user-research-synthesizer" = "npx skills add skills-sh/user-research"
    "ab-test-calculator" = "npx skills add skills-sh/ab-tester"

    # J 类
    "obsidian-zettelkasten" = "npx skills add skills-sh/obsidian-vault"
    "rag-pipeline-builder" = "npx skills add skills-sh/rag-pipeline"
    "notion-ai-workspace" = "npx skills add skills-sh/notion-ai"
    "course-curriculum-ai" = "npx skills add skills-sh/course-designer"
    "corporate-wiki-builder" = "npx skills add skills-sh/corporate-wiki"
    "spaced-repetition-ai" = "npx skills add skills-sh/spaced-repetition"
    "technical-doc-generator" = "npx skills add skills-sh/tech-docs"
    "knowledge-graph-visualizer" = "npx skills add skills-sh/knowledge-graph"

    # K 类
    "prompt-injection-scanner" = "npx skills add skills-sh/prompt-guard"
    "snyk-vuln-scanner" = "npx skills add snyk/snyk-skill"
    "api-key-leak-detector" = "npx skills add skills-sh/secrets-detector"
    "owasp-code-auditor" = "npx skills add skills-sh/owasp-scanner"
    "supply-chain-integrity" = "npx skills add skills-sh/supply-chain-audit"
    "runtime-sandbox-monitor" = "npx skills add skills-sh/runtime-monitor"
    "malicious-code-pattern-ai" = "npx skills add skills-sh/malware-detector"
}

# 分类映射 (skill name -> category key)
$SkillCategories = @{
    "find-skills" = "S"

    "firecrawl-crawler" = "A"
    "n8n-workflow-automation" = "A"
    "playwright-web-agent" = "A"
    "openai-assistants-api" = "A"
    "langchain-agent-builder" = "A"
    "browser-use-automation" = "A"
    "make-dotcom-flows" = "A"
    "mcp-server-template" = "A"
    "zapier-ai-actions" = "A"
    "multi-agent-orchestration" = "A"

    "pandas-analysis-expert" = "B"
    "sql-analytics-pro" = "B"
    "excel-power-automation" = "B"
    "plotly-dash-dashboard" = "B"
    "metabase-bi-copilot" = "B"
    "weekly-report-generator" = "B"
    "data-quality-pipeline" = "B"
    "colab-data-scientist" = "B"
    "powerbi-smart-query" = "B"
    "airtable-no-code-analyst" = "B"

    "a-stock-data" = "C"
    "stock-analysis" = "C"
    "china-stock-analysis" = "C"
    "ashare-ai" = "C"
    "analyse-skills" = "C"
    "china-stock-analyst" = "C"
    "fin-modeling-dcf" = "C"
    "backtesting-framework" = "C"
    "stock-analyst" = "C"
    "capm-factor-analyzer" = "C"
    "risk-metrics-portfolio" = "C"
    "arima-price-forecast" = "C"
    "behavioral-finance-cn" = "C"
    "multiagent-stock-research" = "C"
    "eastmoney-data-scraper" = "C"
    "dragonscope-ashare" = "C"

    "shadcn-ui" = "D"
    "nextjs-app-router" = "D"
    "vercel-ai-sdk" = "D"
    "drizzle-orm" = "D"
    "supabase-realtime" = "D"
    "tailwind-component-system" = "D"
    "fastapi-python-backend" = "D"
    "python-scripting-toolkit" = "D"
    "prisma-database-expert" = "D"
    "react-hooks-patterns" = "D"

    "short-video-script-ai" = "E"
    "content-marketing-suite" = "E"
    "long-form-article-writer" = "E"
    "seo-content-optimizer" = "E"
    "brand-voice-designer" = "E"
    "xiaohongshu-creator" = "E"
    "email-outreach-writer" = "E"
    "social-media-calendar" = "E"

    "meeting-notes-extractor" = "F"
    "executive-deck-builder" = "F"
    "okr-kr-designer" = "F"
    "performance-review-ai" = "F"
    "contract-risk-reviewer" = "F"
    "decision-tree-analyzer" = "F"
    "job-description-screener" = "F"
    "email-smart-reply" = "F"
    "sop-documentation-ai" = "F"
    "project-risk-tracker" = "F"

    "perplexity-style-researcher" = "G"
    "competitor-radar" = "G"
    "market-sizing-report" = "G"
    "industry-news-monitor" = "G"
    "web-data-extractor" = "G"
    "customer-voice-miner" = "G"
    "supply-chain-researcher" = "G"
    "policy-regulation-tracker" = "G"
    "academic-research-reader" = "G"
    "linkedin-talent-scout" = "G"

    "docker-compose-expert" = "H"
    "github-actions-pipeline" = "H"
    "grafana-monitoring" = "H"
    "kubernetes-helm-deployer" = "H"
    "terraform-aws-infra" = "H"
    "postgresql-optimizer" = "H"
    "linux-sysadmin-ai" = "H"
    "redis-caching-expert" = "H"

    "product-prd-writer" = "I"
    "figma-component-ai" = "I"
    "landing-page-optimizer" = "I"
    "wireframe-to-prototype" = "I"
    "ux-heuristic-auditor" = "I"
    "design-token-system" = "I"
    "user-research-synthesizer" = "I"
    "ab-test-calculator" = "I"

    "obsidian-zettelkasten" = "J"
    "rag-pipeline-builder" = "J"
    "notion-ai-workspace" = "J"
    "course-curriculum-ai" = "J"
    "corporate-wiki-builder" = "J"
    "spaced-repetition-ai" = "J"
    "technical-doc-generator" = "J"
    "knowledge-graph-visualizer" = "J"

    "prompt-injection-scanner" = "K"
    "snyk-vuln-scanner" = "K"
    "api-key-leak-detector" = "K"
    "owasp-code-auditor" = "K"
    "supply-chain-integrity" = "K"
    "runtime-sandbox-monitor" = "K"
    "malicious-code-pattern-ai" = "K"
}

$successCount = 0
$failCount = 0
$skipCount = 0

Write-Log "开始安装 $(($InstallCommands.Count)) 个技能..."

foreach ($skillName in $InstallCommands.Keys) {
    $cmd = $InstallCommands[$skillName]
    $catKey = $SkillCategories[$skillName]
    $catDir = Ensure-CategoryDir $catKey

    # 检查是否已安装
    $existingCheck = Get-ChildItem -Path $SkillsDir -Recurse -Filter "SKILL.md" | Where-Object {
        $_.Directory.Name -eq $skillName -or $_.FullName -like "*$skillName*"
    }

    if ($existingCheck) {
        Write-Log "[跳过] $skillName (已安装)"
        $skipCount++
        continue
    }

    Write-Log "[安装] $skillName..."

    # 执行安装命令
    pushd $SkillsDir
    $output = Invoke-Expression $cmd 2>&1
    $exitCode = $LASTEXITCODE
    popd

    if ($exitCode -eq 0) {
        Write-Log "[成功] $skillName"
        $successCount++
    } else {
        Write-Log "[失败] $skillName - $output"
        $failCount++
    }

    # 限制速率，避免API限制
    Start-Sleep -Milliseconds 500
}

Write-Log ""
Write-Log "=== 安装完成 ==="
Write-Log "成功: $successCount"
Write-Log "失败: $failCount"
Write-Log "跳过: $skipCount"
Write-Log "总计: $($InstallCommands.Count)"