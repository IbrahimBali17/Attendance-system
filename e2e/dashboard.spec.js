import { test, expect } from '@playwright/test';

const EMAIL = process.env.PROF_EMAIL || 'prof@hu.edu.jo';
const PASSWORD = process.env.PROF_PASSWORD || 'Prof@12345';

async function login(page) {
  await page.goto('/login');
  await page.getByLabel('Email or university ID').fill(EMAIL);
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'My Courses' })).toBeVisible();
}

test('rejects wrong credentials', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Email or university ID').fill(EMAIL);
  await page.getByLabel('Password').fill('wrong-password');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('alert')).toContainText('Invalid credentials');
});

test('professor logs in, starts a session, sees the QR code, then ends it', async ({ page }) => {
  await login(page);
  await page.getByRole('link', { name: /Live session|Resume live session/ }).first().click();

  if (await page.getByRole('button', { name: 'Start session' }).isVisible()) {
    await page.getByRole('button', { name: 'Start session' }).click();
  }
  await expect(page.getByTestId('qr-code')).toBeVisible();
  await expect(page.getByTestId('qr-code').locator('svg').first()).toBeVisible();
  await expect(page.getByText('Live feed connected')).toBeVisible({ timeout: 10_000 });

  page.once('dialog', (d) => d.accept());
  await page.getByRole('button', { name: 'End session' }).click();
  await expect(page.getByRole('heading', { name: 'Session ended' })).toBeVisible();
});

test('analytics page shows charts and export buttons', async ({ page }) => {
  await login(page);
  await page.getByRole('link', { name: 'Analytics' }).first().click();
  await expect(page.getByText('Attendance by session')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Export Excel' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Export Word' })).toBeVisible();
});
