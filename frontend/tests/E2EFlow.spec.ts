import { test, expect } from '@playwright/test';

test.describe('OpenWiki End-to-End Flow', () => {
  test('Mock offline ingestion and streaming fallback', async ({ page }) => {
    // Mock check
    await page.route('**/api/repo/check*', async route => {
      await route.fulfill({ json: { indexed: false } });
    });

    // Mock ingest
    await page.route('**/api/ingest*', async route => {
      const json = {
        status: 'completed',
        owner: 'microsoft',
        repo: 'playwright',
        document: '# Overview\n\nPlaywright is a testing framework.',
        metadata: { stars: 68000, language: 'TypeScript', description: 'Playwright testing' },
        sections: [],
        relations: [],
        diagrams: []
      };
      await route.fulfill({ json });
    });

    await page.goto('/');
    
    // Click microsoft/playwright repo from popular repos list
    const repoCards = page.getByTestId('repo-card');
    await repoCards.filter({ hasText: 'microsoft/playwright' }).click();
    
    // Expect documentation
    await expect(page.getByTestId('doc-viewer')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('Overview')).toBeVisible();
  });
});