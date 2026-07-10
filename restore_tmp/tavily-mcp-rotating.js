#!/usr/bin/env node
/**
 * Tavily MCP Server with API Key Rotation
 * Rotates through multiple API keys when one is exhausted
 */

const { spawn } = require('child_process');

const API_KEYS = [
  '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',
  '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',
  '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'
];

let currentKeyIndex = 0;
let consecutiveErrors = 0;
const MAX_ERRORS_BEFORE_ROTATION = 3;

function getNextKey() {
  currentKeyIndex = (currentKeyIndex + 1) % API_KEYS.length;
  return API_KEYS[currentKeyIndex];
}

function startMcpServer(apiKey) {
  console.error(`[tavily-mcp-rotating] Starting with API key index: ${currentKeyIndex}`);

  const child = spawn('npx', ['-y', 'tavily-mcp'], {
    env: {
      ...process.env,
      TAVILY_API_KEY: apiKey
    },
    stdio: ['pipe', 'pipe', 'pipe']
  });

  child.stdout.on('data', (data) => {
    process.stdout.write(data);
  });

  child.stderr.on('data', (data) => {
    const message = data.toString();

    // Check for rate limit or auth errors
    if (message.includes('429') || message.includes('rate limit') ||
        message.includes('401') || message.includes('unauthorized') ||
        message.includes(' quota ') || message.includes('exhausted')) {
      consecutiveErrors++;

      if (consecutiveErrors >= MAX_ERRORS_BEFORE_ROTATION) {
        console.error(`[tavily-mcp-rotating] Detected key exhaustion, rotating to next key...`);
        consecutiveErrors = 0;
        currentKeyIndex = (currentKeyIndex + 1) % API_KEYS.length;
        child.kill();
      }
    } else {
      consecutiveErrors = 0;
      console.error(`[tavily-mcp] ${message}`);
    }
  });

  child.on('close', (code) => {
    if (code !== null && code !== 0) {
      // Restart with next key
      const nextKey = API_KEYS[currentKeyIndex];
      startMcpServer(nextKey);
    }
  });

  return child;
}

// Start with first key
startMcpServer(API_KEYS[currentKeyIndex]);

// Handle stdin
process.stdin.on('data', (data) => {
  // Forward to child process if needed
});
