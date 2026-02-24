const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

async function run() {
  console.log('Starting browser...');
  const browser = await chromium.launch();
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 }
  });
  const page = await context.newPage();

  try {
    console.log('Navigating to homepage...');
    await page.goto('http://localhost:5173');
    await page.waitForLoadState('networkidle');
    
    // Screenshot 1: Homepage
    await page.screenshot({ path: '/root/.openclaw/workspace/deepwiki-app/homepage.png' });
    console.log('Saved homepage.png');

    // Screenshot 2: Ingesting state
    console.log('Testing ingestion...');
    await page.fill('#repo-input', 'microsoft/playwright');
    
    // Intercept the API call to provide mock data since cliproxy might be slow or offline
    await page.route('http://localhost:5000/api/ingest?owner=microsoft&repo=playwright', async route => {
      const mockDoc = `
# Playwright Architecture

## Overview
Playwright is a framework for Web Testing and Automation. It allows testing Chromium, Firefox and WebKit with a single API. Playwright is built to enable cross-browser web automation that is ever-green, capable, reliable and fast.

## Core Components
*   **Test Runner**: High-performance test executor
*   **Browser Drivers**: Direct CDP integration for Chrome, Juggler for Firefox
*   **Selectors Engine**: Advanced locator resolution

## Example
\`\`\`typescript
const { chromium } = require('playwright');
const browser = await chromium.launch();
\`\`\`
      `;
      await route.fulfill({ 
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ 
          status: 'completed', 
          owner: 'microsoft', 
          repo: 'playwright',
          document: mockDoc
        })
      });
    });

    await page.click('#ingest-button');
    
    // Wait for the UI transition (chat unlocking and document showing)
    await page.waitForTimeout(2000);
    
    // Screenshot 2: Dual-pane interface
    await page.screenshot({ path: '/root/.openclaw/workspace/deepwiki-app/dual_pane.png' });
    console.log('Saved dual_pane.png');

    // Screenshot 3: Chat Interaction
    console.log('Testing Chat...');
    // Click Deep Mode
    const deepButton = page.locator('button', { hasText: 'Deep' });
    await deepButton.click({ force: true });
    
    await page.fill('#chat-input', 'How does the architecture work?');
    await page.click('#chat-send', { force: true });
    
    // Wait for mock stream to complete
    await page.waitForTimeout(3000);
    
    // Screenshot 3: Deep Mode Chat
    await page.screenshot({ path: '/root/.openclaw/workspace/deepwiki-app/chat_interaction.png' });
    console.log('Saved chat_interaction.png');

  } catch (error) {
    console.error('Error during automation:', error);
  } finally {
    await browser.close();
  }
}

run();
