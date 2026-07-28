import fs from "node:fs/promises";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);

function parseArgs(argv) {
  const result = {};
  for (let i = 0; i < argv.length; i += 1) {
    const token = argv[i];
    if (!token.startsWith("--")) {
      continue;
    }

    const key = token.slice(2);
    const next = argv[i + 1];
    if (next && !next.startsWith("--")) {
      result[key] = next;
      i += 1;
    } else {
      result[key] = true;
    }
  }

  return result;
}

function normalizeText(value) {
  const text = String(value ?? "")
    .replace(/\r\n/g, "\n")
    .replace(/\r/g, "\n")
    .replace(/\u00a0/g, " ")
    .replace(/\n\[\/code\]/gi, "[/code]")
    .trim();

  let inCodeBlock = false;
  return text.split("\n").map((line) => {
    if (/\[code\]/i.test(line)) {
      inCodeBlock = true;
    }

    const normalizedLine = inCodeBlock ? line.trimStart() : line;
    if (/\[\/code\]/i.test(normalizedLine)) {
      inCodeBlock = false;
    }

    return normalizedLine;
  }).join("\n").trim();
}

function assertTextEquals(actual, expected, label) {
  const actualNormalized = normalizeText(actual);
  const expectedNormalized = normalizeText(expected);
  if (actualNormalized !== expectedNormalized) {
    throw new Error(`${label} did not match after verification. Expected ${expectedNormalized.length} chars, found ${actualNormalized.length} chars.`);
  }
}

function buildEditUrl(modUrl) {
  const parsed = new URL(modUrl);
  const parts = parsed.pathname.split("/").filter(Boolean);
  const modsIndex = parts.indexOf("mods");
  if (modsIndex < 1 || modsIndex + 1 >= parts.length) {
    throw new Error(`Cannot build Nexus edit URL from ${modUrl}`);
  }

  const game = parts[modsIndex - 1];
  const modId = parts[modsIndex + 1];
  return `${parsed.origin}/games/${game}/mods/${modId}/edit/general`;
}

async function exists(filePath) {
  try {
    await fs.access(filePath);
    return true;
  } catch {
    return false;
  }
}

async function ensureDirectory(directory) {
  await fs.mkdir(directory, { recursive: true });
}

async function readJson(filePath) {
  const text = await fs.readFile(filePath, "utf8");
  return JSON.parse(text.replace(/^\uFEFF/, ""));
}

async function writeJson(filePath, value) {
  await fs.writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

async function isVisible(locator) {
  try {
    return await locator.isVisible({ timeout: 1500 });
  } catch {
    return false;
  }
}

async function clickByUpdaterId(page, id) {
  await page.locator(`[data-nexus-updater-id="${id}"]`).click();
}

async function replaceTextareaValue(page, textarea, value) {
  const locator = page.locator(`[data-nexus-updater-id="${textarea.id}"]`);
  await locator.scrollIntoViewIfNeeded();
  await clickByUpdaterId(page, textarea.id);
  await page.keyboard.press("Control+A");
  await page.keyboard.press("Backspace");
  if (value.length > 0) {
    await page.keyboard.insertText(value);
  }

  await locator.evaluate((element) => {
    element.dispatchEvent(new Event("change", { bubbles: true }));
    element.blur();
  });
}

async function clickButtonInfo(page, button) {
  await page.mouse.click(button.left + (button.width / 2), button.top + (button.height / 2));
}

async function getVisibleTextareas(page) {
  return await page.evaluate(() => {
    for (const element of document.querySelectorAll("[data-nexus-updater-id]")) {
      element.removeAttribute("data-nexus-updater-id");
    }

    return Array.from(document.querySelectorAll("textarea"))
      .map((element, index) => {
        const style = window.getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        const visible = style.display !== "none"
          && style.visibility !== "hidden"
          && rect.width > 1
          && rect.height > 1;
        const id = `textarea-${index}`;
        if (visible) {
          element.setAttribute("data-nexus-updater-id", id);
        }

        return {
          id,
          visible,
          top: rect.top,
          left: rect.left,
          width: rect.width,
          height: rect.height,
          value: element.value ?? "",
          placeholder: element.getAttribute("placeholder") ?? "",
          ariaLabel: element.getAttribute("aria-label") ?? ""
        };
      })
      .filter((item) => item.visible)
      .sort((a, b) => (a.top - b.top) || (a.left - b.left));
  });
}

async function getButtons(page) {
  return await page.evaluate(() => {
    for (const element of document.querySelectorAll("[data-nexus-button-id]")) {
      element.removeAttribute("data-nexus-button-id");
    }

    return Array.from(document.querySelectorAll("button,a,[role='button']"))
      .map((element, index) => {
        const style = window.getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        const text = (element.innerText || element.textContent || "").trim();
        const aria = element.getAttribute("aria-label") || "";
        const title = element.getAttribute("title") || "";
        const disabled = element.disabled === true || element.getAttribute("aria-disabled") === "true";
        const visible = style.display !== "none"
          && style.visibility !== "hidden"
          && rect.width > 1
          && rect.height > 1;
        const id = `button-${index}`;
        if (visible) {
          element.setAttribute("data-nexus-button-id", id);
        }

        return {
          id,
          visible,
          text,
          aria,
          title,
          disabled,
          top: rect.top,
          left: rect.left,
          width: rect.width,
          height: rect.height
        };
      })
      .filter((item) => item.visible)
      .sort((a, b) => (a.top - b.top) || (a.left - b.left));
  });
}

async function isLoggedOut(page) {
  if (await isVisible(page.getByText("Log in to Nexus Mods", { exact: true }))) {
    return true;
  }

  if (await isVisible(page.getByText("Email or Username", { exact: true }))) {
    return true;
  }

  const loginButton = page.getByText("Log in", { exact: true });
  return await isVisible(loginButton);
}

function isNexusAuthUrl(page) {
  const url = page.url();
  return url.includes("users.nexusmods.com")
    || url.includes("/auth/sign_in")
    || url.includes("/users/login")
    || url.includes("/login");
}

async function hasLoggedInSignal(page) {
  if (await isVisible(page.getByText("Welcome back,", { exact: false }))) {
    return true;
  }

  if (await isVisible(page.getByText("My content", { exact: true }))) {
    return true;
  }

  return false;
}

async function waitForLogin(page, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (page.isClosed()) {
      throw new Error("The isolated browser window was closed before login was detected. Rerun -LoginOnly and leave the window open until the tool closes it.");
    }

    if (await hasLoggedInSignal(page)) {
      return true;
    }

    try {
      await page.waitForTimeout(1500);
    } catch (error) {
      throw new Error("The isolated browser window was closed before login was detected. Rerun -LoginOnly and leave the window open until the tool closes it.");
    }
  }

  return false;
}

async function requireLoggedIn(page) {
  if (isNexusAuthUrl(page) || await isLoggedOut(page)) {
    throw new Error("Nexus is logged out in the isolated browser profile. Run -LoginOnly and log in there first.");
  }
}

async function openEditor(page, modUrl, timeoutMs) {
  const editUrl = buildEditUrl(modUrl);
  if (page.url().startsWith(editUrl)) {
    await requireLoggedIn(page);
    await page.getByText("Short description", { exact: true }).waitFor({ state: "visible", timeout: timeoutMs });
    await page.getByText("Full description", { exact: true }).waitFor({ state: "visible", timeout: timeoutMs });
    return;
  }

  await page.goto(modUrl, { waitUntil: "domcontentloaded", timeout: timeoutMs });
  await requireLoggedIn(page);

  const manageButton = page.locator("button, a, [role='button']").filter({ hasText: "Manage" }).first();
  const manageCount = await page.locator("button, a, [role='button']").filter({ hasText: "Manage" }).count();
  if (manageCount < 1 || !(await isVisible(manageButton))) {
    throw new Error("Could not find Manage on the Nexus mod page. The logged-in account may not own this mod or have edit permission.");
  }

  await manageButton.click();
  const modDetails = page.getByText("Mod details", { exact: true });
  if (await isVisible(modDetails)) {
    await Promise.all([
      page.waitForLoadState("domcontentloaded", { timeout: timeoutMs }).catch(() => {}),
      modDetails.click()
    ]);
  } else {
    await page.goto(editUrl, { waitUntil: "domcontentloaded", timeout: timeoutMs });
  }

  await page.waitForLoadState("domcontentloaded", { timeout: timeoutMs });
  if (!page.url().includes("/edit/general")) {
    await page.goto(editUrl, { waitUntil: "domcontentloaded", timeout: timeoutMs });
  }

  await page.getByText("Short description", { exact: true }).waitFor({ state: "visible", timeout: timeoutMs });
  await page.getByText("Full description", { exact: true }).waitFor({ state: "visible", timeout: timeoutMs });
}

async function ensureSourceMode(page, timeoutMs) {
  await page.getByText("Full description", { exact: true }).scrollIntoViewIfNeeded();
  let textareas = await getVisibleTextareas(page);
  if (textareas.length >= 2) {
    return textareas;
  }

  const editors = page.locator(".ProseMirror, [contenteditable='true']");
  const editorCount = await editors.count();
  if (editorCount > 0) {
    await editors.nth(editorCount - 1).click();
    await page.keyboard.press("Control+Shift+S");
    await page.waitForTimeout(500);
    textareas = await getVisibleTextareas(page);
    if (textareas.length >= 2) {
      return textareas;
    }
  }

  const buttons = await getButtons(page);
  const sourceButton = buttons
    .filter((button) => {
      const haystack = `${button.text} ${button.aria} ${button.title}`.toLowerCase();
      return haystack.includes("source")
        || button.text === "[]"
        || button.text === "<>"
        || button.text === "</>";
    })
    .sort((a, b) => b.top - a.top)[0];

  if (!sourceButton) {
    throw new Error("Could not find the full-description source toggle or rich-text editor.");
  }

  await clickButtonInfo(page, sourceButton);

  const deadline = Date.now() + timeoutMs;
  do {
    textareas = await getVisibleTextareas(page);
    if (textareas.length >= 2) {
      return textareas;
    }

    await page.waitForTimeout(250);
  } while (Date.now() < deadline);

  throw new Error("Full-description source textarea did not appear after opening source mode.");
}

async function readDescriptions(page, timeoutMs) {
  const textareas = await ensureSourceMode(page, timeoutMs);
  if (textareas.length < 2) {
    throw new Error(`Expected at least two visible textareas in the Nexus editor, found ${textareas.length}.`);
  }

  return {
    shortDescription: textareas[0].value,
    fullDescription: textareas[textareas.length - 1].value
  };
}

async function fillDescriptions(page, desiredShort, desiredFull, timeoutMs) {
  let textareas = await ensureSourceMode(page, timeoutMs);
  if (textareas.length < 2) {
    throw new Error(`Expected at least two visible textareas in the Nexus editor, found ${textareas.length}.`);
  }

  if (normalizeText(textareas[0].value) !== normalizeText(desiredShort)) {
    await replaceTextareaValue(page, textareas[0], desiredShort);
  }

  textareas = await ensureSourceMode(page, timeoutMs);
  if (normalizeText(textareas[textareas.length - 1].value) !== normalizeText(desiredFull)) {
    await replaceTextareaValue(page, textareas[textareas.length - 1], desiredFull);
  }

  const afterFill = await readDescriptions(page, timeoutMs);
  assertTextEquals(afterFill.shortDescription, desiredShort, "Short description field");
  assertTextEquals(afterFill.fullDescription, desiredFull, "Full description field");
}

async function clickSave(page, timeoutMs) {
  const buttons = await getButtons(page);
  const saveButton = buttons
    .filter((button) => button.text === "Save" && !button.disabled)
    .sort((a, b) => b.top - a.top)[0];

  if (!saveButton) {
    throw new Error("Could not find an enabled Save button after editing.");
  }

  await clickButtonInfo(page, saveButton);
  await page.waitForLoadState("networkidle", { timeout: Math.min(timeoutMs, 15000) }).catch(() => {});
  await page.waitForTimeout(2500);
}

async function hasUnsavedModal(page) {
  return await isVisible(page.getByText("Unsaved changes", { exact: true }));
}

async function verifySaved(page, modUrl, expectedShort, expectedFull, timeoutMs) {
  const editUrl = buildEditUrl(modUrl);
  await page.goto(editUrl, { waitUntil: "domcontentloaded", timeout: timeoutMs });
  await page.waitForLoadState("domcontentloaded", { timeout: timeoutMs });
  if (await hasUnsavedModal(page)) {
    throw new Error("Nexus showed the Unsaved changes prompt during verification. The save likely did not complete.");
  }

  const afterReload = await readDescriptions(page, timeoutMs);
  assertTextEquals(afterReload.shortDescription, expectedShort, "Saved short description");
  assertTextEquals(afterReload.fullDescription, expectedFull, "Saved full description");
  return afterReload;
}

async function createBackup(request, previous, desired, action) {
  const modBackupRoot = path.join(request.backupRoot, request.packageName);
  await ensureDirectory(modBackupRoot);
  const safeTimestamp = new Date().toISOString().replace(/[:.]/g, "-");
  const backupPath = path.join(modBackupRoot, `${safeTimestamp}-${action}.json`);
  const backup = {
    createdAt: new Date().toISOString(),
    action,
    packageName: request.packageName,
    displayName: request.displayName,
    nexusUrl: request.nexusUrl,
    previous,
    desired,
    restoreCommand: `tools/Update-NexusDescription.ps1 -Mod ${request.packageName} -BackupPath "${backupPath}" -Save`
  };
  await writeJson(backupPath, backup);
  return backupPath;
}

function getDesiredFromBackup(backup) {
  if (!backup.previous) {
    throw new Error("Backup JSON does not contain a previous description payload.");
  }

  return {
    shortDescription: backup.previous.shortDescription ?? "",
    fullDescription: backup.previous.fullDescription ?? ""
  };
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.request) {
    throw new Error("Missing --request.");
  }

  const request = await readJson(args.request);
  const timeoutMs = Math.max(30, Number(request.timeoutSeconds || 180)) * 1000;
  const toolRoot = process.env.NEXUS_DESCRIPTION_TOOL_ROOT;
  if (!toolRoot) {
    throw new Error("NEXUS_DESCRIPTION_TOOL_ROOT is not set.");
  }

  const playwrightPackage = path.join(toolRoot, "node_modules", "playwright");
  if (!(await exists(path.join(playwrightPackage, "package.json")))) {
    throw new Error(`Playwright package is not installed at ${playwrightPackage}`);
  }

  const { chromium } = require(playwrightPackage);
  await ensureDirectory(request.profileRoot);
  await ensureDirectory(request.backupRoot);

  if (request.browser !== "Chrome") {
    throw new Error("Only Chrome is supported for Nexus description updates.");
  }

  let browser = null;
  let context = null;
  let page = null;
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${request.remoteDebuggingPort}`, {
    timeout: timeoutMs
  });
  context = browser.contexts()[0];
  if (!context) {
    context = await browser.newContext();
  }

  const pages = context.pages();
  page = pages.find((candidate) => candidate.url().includes("nexusmods.com")) || pages[0] || await context.newPage();

  page.setDefaultTimeout(timeoutMs);
  page.on("dialog", async (dialog) => {
    await dialog.dismiss();
  });

  try {
    if (request.action === "login") {
      await page.goto("https://www.nexusmods.com/", { waitUntil: "domcontentloaded", timeout: timeoutMs });
      const loggedIn = await waitForLogin(page, timeoutMs);
      if (!loggedIn) {
        throw new Error("Timed out waiting for Nexus login in the isolated browser profile.");
      }

      console.log(JSON.stringify({
        status: "logged-in",
        profileRoot: request.profileRoot,
        currentUrl: page.url()
      }, null, 2));
      return;
    }

    await openEditor(page, request.nexusUrl, timeoutMs);
    const previous = await readDescriptions(page, timeoutMs);

    let desired = {
      shortDescription: request.desiredShortDescription,
      fullDescription: request.desiredFullDescription
    };

    if (request.action === "revert-save" || request.action === "revert-review") {
      const restoreBackup = await readJson(request.restoreBackupPath);
      desired = getDesiredFromBackup(restoreBackup);
    }

    const backupPath = await createBackup(request, previous, desired, request.action);
    const shortDescriptionChanged = normalizeText(previous.shortDescription) !== normalizeText(desired.shortDescription);
    const fullDescriptionChanged = normalizeText(previous.fullDescription) !== normalizeText(desired.fullDescription);

    if (request.action === "review" || request.action === "revert-review") {
      console.log(JSON.stringify({
        status: "reviewed",
        action: request.action,
        nexusUrl: request.nexusUrl,
        backupPath,
        restoreBackupPath: request.restoreBackupPath || null,
        currentShortLength: normalizeText(previous.shortDescription).length,
        currentFullLength: normalizeText(previous.fullDescription).length,
        desiredShortLength: normalizeText(desired.shortDescription).length,
        desiredFullLength: normalizeText(desired.fullDescription).length,
        shortDescriptionChanged,
        fullDescriptionChanged
      }, null, 2));
      return;
    }

    if (!shortDescriptionChanged && !fullDescriptionChanged) {
      console.log(JSON.stringify({
        status: "already-current",
        action: request.action,
        nexusUrl: request.nexusUrl,
        backupPath,
        restoreBackupPath: request.restoreBackupPath || null,
        shortLength: normalizeText(previous.shortDescription).length,
        fullLength: normalizeText(previous.fullDescription).length
      }, null, 2));
      return;
    }

    await fillDescriptions(page, desired.shortDescription, desired.fullDescription, timeoutMs);
    await clickSave(page, timeoutMs);
    const verified = await verifySaved(page, request.nexusUrl, desired.shortDescription, desired.fullDescription, timeoutMs);

    console.log(JSON.stringify({
      status: "saved-and-verified",
      action: request.action,
      nexusUrl: request.nexusUrl,
      backupPath,
      restoreBackupPath: request.restoreBackupPath || null,
      shortLength: normalizeText(verified.shortDescription).length,
      fullLength: normalizeText(verified.fullDescription).length
    }, null, 2));
  } finally {
    if (browser) {
      if (!request.keepOpen) {
        await browser.close();
      }
    }
  }
}

main().then(() => {
  process.exit(0);
}).catch((error) => {
  console.error(error && error.stack ? error.stack : String(error));
  process.exit(1);
});
