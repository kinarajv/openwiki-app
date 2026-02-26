import { test, expect } from '@playwright/test';

test.describe('OpenWiki End-to-End Flow', () => {
  test('Mock ingestion flow', async ({ page }) => {
    await page.route('**/api/Repository/check*', async route => {
      await route.fulfill({ json: { indexed: false } });
    });

    await page.route('**/api/Repository/ingest*', async route => {
      const json = {
        status: 'completed',
        owner: 'microsoft',
        repo: 'playwright',
        document: '# Overview\n\nPlaywright is a testing framework.',
        metadata: { stars: 68000, language: 'TypeScript', description: 'Playwright testing' },
        sections: 1,
        relations: 0,
        diagrams: 0
      };
      await route.fulfill({ json });
    });

    await page.goto('/');
    
    // Test card
    await page.getByTestId('repo-card-microsoft/playwright').click();
    
    // Expect documentation
    await expect(page.getByTestId('doc-viewer')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('Overview')).toBeVisible();
  });
});