"use strict";

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const vortex = require("vortex-api");
const core = require("./promotion-core");

const GAME_ID = "taintedgrailthefallofavalon";
const POLL_INTERVAL_MS = 5000;
const PENDING_LOG_REPEAT_MS = 5 * 60 * 1000;
const DEPLOY_TIMEOUT_MS = 10 * 60 * 1000;
const CREATE_MOD_TIMEOUT_MS = 30 * 1000;
const ATTRIBUTE_VERIFY_TIMEOUT_MS = 5000;

class PendingPromotionError extends Error {}

function hashFile(filePath, algorithm) {
  return new Promise((resolve, reject) => {
    const hasher = crypto.createHash(algorithm);
    const stream = fs.createReadStream(filePath);
    stream.on("error", reject);
    stream.on("data", (chunk) => hasher.update(chunk));
    stream.on("end", () => resolve(hasher.digest("hex")));
  });
}

async function listFiles(root) {
  const result = [];
  async function visit(current) {
    const entries = await fs.promises.readdir(current, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(current, entry.name);
      if (entry.isDirectory()) {
        await visit(fullPath);
      } else if (entry.isFile()) {
        result.push(fullPath);
      }
    }
  }
  await visit(root);
  return result;
}

async function verifyArchive(request, existingDownload) {
  if (existingDownload !== undefined) {
    return;
  }
  const stats = await fs.promises.stat(request.archivePath);
  if (stats.size !== Number(request.archive.sizeBytes)) {
    throw new Error("Queued archive size no longer matches its Nexus release receipt.");
  }
  const [md5, sha256] = await Promise.all([
    hashFile(request.archivePath, "md5"),
    hashFile(request.archivePath, "sha256"),
  ]);
  if (md5 !== core.normalizedHash(request.archive.md5)
      || sha256 !== core.normalizedHash(request.archive.sha256)) {
    throw new Error("Queued archive hashes no longer match its Nexus release receipt.");
  }
}

async function verifyStaging(request) {
  const expected = new Map(request.payload.map((entry) => [entry.path.replace(/\\/g, "/"), entry]));
  const files = await listFiles(request.stagingPath);
  const actualPaths = files.map((filePath) => path.relative(request.stagingPath, filePath).replace(/\\/g, "/"));
  if (actualPaths.length !== expected.size || actualPaths.some((relativePath) => !expected.has(relativePath))) {
    throw new Error("Staged files no longer match the exact uploaded Nexus payload.");
  }
  for (let index = 0; index < files.length; index += 1) {
    const expectedEntry = expected.get(actualPaths[index]);
    const stats = await fs.promises.stat(files[index]);
    if (stats.size !== Number(expectedEntry.sizeBytes)
        || await hashFile(files[index], "sha256") !== core.normalizedHash(expectedEntry.sha256)) {
      throw new Error(`Staged file '${actualPaths[index]}' no longer matches the uploaded Nexus payload.`);
    }
  }
}

function findExistingDownload(state, request) {
  const downloads = state?.persistent?.downloads?.files || {};
  return Object.values(downloads).find((download) => (
    core.normalizedHash(download?.fileMD5) === core.normalizedHash(request.archive.md5)
      && Number(download?.size) === Number(request.archive.sizeBytes)
  ));
}

function importArchive(api, archivePath) {
  return new Promise((resolve, reject) => {
    let settled = false;
    const timer = setTimeout(() => {
      if (!settled) {
        settled = true;
        reject(new PendingPromotionError("Vortex archive import has not completed yet."));
      }
    }, 120000);
    api.events.emit("import-downloads", [archivePath], (downloadIds) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      if (!Array.isArray(downloadIds) || downloadIds.length !== 1) {
        reject(new PendingPromotionError("Vortex did not return one imported download ID."));
      } else {
        resolve(downloadIds[0]);
      }
    }, true);
  });
}

async function writeJsonAtomic(filePath, value) {
  const temporaryPath = `${filePath}.${crypto.randomBytes(8).toString("hex")}.tmp`;
  await fs.promises.writeFile(temporaryPath, JSON.stringify(value, null, 2), "utf8");
  await fs.promises.rename(temporaryPath, filePath);
}

function attributeValuesEqual(key, actual, expected) {
  if (["modId", "fileId", "fileSize"].includes(key)) {
    return Number(actual) === Number(expected);
  }
  return actual === expected;
}

function attributesMatch(actualAttributes, expectedAttributes) {
  return Object.entries(expectedAttributes)
    .filter(([, value]) => value !== undefined)
    .every(([key, value]) => attributeValuesEqual(key, actualAttributes?.[key], value));
}

async function setAndVerifyModAttributes(api, gameId, modId, attributes) {
  const changedAttributes = Object.entries(attributes)
    .filter(([, value]) => value !== undefined)
    .filter(([key, value]) => !attributeValuesEqual(
      key,
      api.getState()?.persistent?.mods?.[gameId]?.[modId]?.attributes?.[key],
      value,
    ));
  changedAttributes.forEach(([key, value]) => {
    api.store.dispatch(vortex.actions.setModAttribute(gameId, modId, key, value));
  });

  const deadline = Date.now() + ATTRIBUTE_VERIFY_TIMEOUT_MS;
  while (Date.now() <= deadline) {
    const mod = api.getState()?.persistent?.mods?.[gameId]?.[modId];
    if (mod !== undefined && attributesMatch(mod.attributes, attributes)) {
      return { mod, changed: changedAttributes.length > 0 };
    }
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
  throw new PendingPromotionError(`Waiting for Vortex to persist grouping metadata for '${modId}'.`);
}

function buildInitialStagedAttributes(request, installTime) {
  const groupingRequest = request.requestType === "local-grouping"
    ? request
    : {
      gameId: request.gameId,
      displayName: request.displayName,
      version: request.version,
      grouping: {
        source: "grailwright-local",
        modId: request?.nexus?.modId,
        logicalFileName: request?.nexus?.logicalFileName || request.displayName,
        nexusUrl: request?.nexus?.url,
      },
    };
  return {
    ...core.buildLocalGroupingAttributes(groupingRequest, {}),
    installTime,
  };
}

async function loadGroupingCatalog(bridgeRoot) {
  const recordsRoot = path.join(bridgeRoot, "catalog-records");
  await fs.promises.mkdir(recordsRoot, { recursive: true });
  const entries = [];
  let invalidRecordCount = 0;
  for (const recordFile of (await fs.promises.readdir(recordsRoot)).filter((name) => name.endsWith(".json"))) {
    try {
      const record = JSON.parse(await fs.promises.readFile(path.join(recordsRoot, recordFile), "utf8"));
      if (record.schemaVersion !== 1 || record.gameId !== GAME_ID
          || !record.packageName || !record.displayName || !record.stagedNamePrefix || !record.logicalFileName) {
        invalidRecordCount += 1;
      } else {
        entries.push(record);
      }
    } catch (_) {
      invalidRecordCount += 1;
    }
  }
  let requiredPackageNames = [];
  try {
    const completion = JSON.parse(await fs.promises.readFile(
      path.join(bridgeRoot, "catalog-complete.json"),
      "utf8",
    ));
    if (completion.schemaVersion !== 1 || completion.gameId !== GAME_ID
        || !Array.isArray(completion.packageNames) || completion.packageNames.length === 0) {
      invalidRecordCount += 1;
    } else {
      requiredPackageNames = completion.packageNames;
    }
  } catch (error) {
    if (error?.code !== "ENOENT") {
      invalidRecordCount += 1;
    }
  }
  const requiredPackageNameSet = new Set(requiredPackageNames);
  const currentEntries = requiredPackageNames.length === 0
    ? entries
    : entries.filter((entry) => requiredPackageNameSet.has(entry.packageName));
  const recordedPackageNames = new Set(currentEntries.map((entry) => entry.packageName));
  const missingRequiredRecords = requiredPackageNames.filter((name) => !recordedPackageNames.has(name)).length;
  invalidRecordCount += missingRequiredRecords;
  return {
    entries: currentEntries,
    invalidRecordCount,
    available: requiredPackageNames.length > 0 && missingRequiredRecords === 0,
  };
}

async function completeRequest(api, bridgeRoot, requestPath, request, archiveId, metadata) {
  const state = api.getState();
  const download = state?.persistent?.downloads?.files?.[archiveId];
  const attributes = core.buildModAttributes(request, metadata, download?.localPath);

  await setAndVerifyModAttributes(api, request.gameId, request.stagedModId, attributes);
  api.store.dispatch(vortex.actions.setModArchiveId(request.gameId, request.stagedModId, archiveId));
  if (typeof vortex.actions.setDownloadInstalled === "function") {
    api.store.dispatch(vortex.actions.setDownloadInstalled(archiveId, request.gameId, request.stagedModId));
  }
  if (typeof vortex.actions.setDownloadModInfo === "function") {
    api.store.dispatch(vortex.actions.setDownloadModInfo(archiveId, "meta", metadata));
  }

  const acknowledgementRoot = path.join(bridgeRoot, "acknowledgements");
  await fs.promises.mkdir(acknowledgementRoot, { recursive: true });
  await writeJsonAtomic(path.join(acknowledgementRoot, `${request.requestId}.json`), {
    schemaVersion: 1,
    requestId: request.requestId,
    status: "completed",
    completedAt: new Date().toISOString(),
    gameId: request.gameId,
    stagedModId: request.stagedModId,
    archiveId,
    nexus: {
      modId: attributes.modId,
      fileId: attributes.fileId,
      source: attributes.source,
    },
  });
  await fs.promises.unlink(requestPath);
  await fs.promises.rm(path.join(bridgeRoot, "archives", request.requestId), { recursive: true, force: true });
  vortex.log("info", "Grailwright promoted staged mod to verified Nexus metadata", {
    modId: request.stagedModId,
    nexusModId: attributes.modId,
    nexusFileId: attributes.fileId,
  });
}

async function completeLocalGroupingRequest(api, bridgeRoot, requestPath, request, stagedMod) {
  const attributes = core.buildLocalGroupingAttributes(request, stagedMod.attributes);
  const verified = await setAndVerifyModAttributes(
    api,
    request.gameId,
    request.stagedModId,
    attributes,
  );
  const activation = await activateNewLocalVersion(api, request, {
    ...verified.mod.attributes,
  });

  const acknowledgementRoot = path.join(bridgeRoot, "acknowledgements");
  await fs.promises.mkdir(acknowledgementRoot, { recursive: true });
  await writeJsonAtomic(path.join(acknowledgementRoot, `${request.requestId}.json`), {
    schemaVersion: 1,
    requestId: request.requestId,
    status: "local-grouped",
    completedAt: new Date().toISOString(),
    gameId: request.gameId,
    stagedModId: request.stagedModId,
    grouping: {
      modId: verified.mod.attributes?.modId,
      logicalFileName: attributes.logicalFileName,
      source: verified.mod.attributes?.source,
      collectionReady: attributes.grailwrightCollectionReady,
    },
    activation,
  });
  await fs.promises.unlink(requestPath);
  vortex.log("info", "Grailwright grouped local staged mod", {
    modId: request.stagedModId,
    nexusModId: verified.mod.attributes?.modId,
    version: attributes.version,
    activationStatus: activation.status,
  });
}

function deployMods(api) {
  return new Promise((resolve, reject) => {
    let settled = false;
    let timer;
    const finish = (error) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      if (error != null) {
        reject(error instanceof Error ? error : new Error(String(error)));
      } else {
        resolve();
      }
    };
    timer = setTimeout(() => {
      finish(new Error("Vortex deployment did not complete within ten minutes."));
    }, DEPLOY_TIMEOUT_MS);
    api.events.emit("deploy-mods", finish);
  });
}

async function activateNewLocalVersion(api, request, targetAttributes) {
  const state = api.getState();
  const profile = typeof vortex.selectors.activeProfile === "function"
    ? vortex.selectors.activeProfile(state)
    : undefined;
  if (profile === undefined || profile.gameId !== request.gameId) {
    return { status: "unchanged", reason: "no-active-profile" };
  }
  const plan = core.buildVariantActivationPlan(
    state?.persistent?.mods?.[request.gameId] || {},
    profile.modState || {},
    request.stagedModId,
    targetAttributes,
  );
  if (!plan.switch) {
    return { status: "unchanged", reason: plan.reason };
  }
  if (typeof vortex.actions.setModEnabled !== "function") {
    throw new Error("This Vortex build does not expose the profile activation action.");
  }

  const changeOptions = {
    allowAutoDeploy: false,
    reason: "version_update",
  };
  plan.disableModIds.forEach((modId) => {
    api.store.dispatch(vortex.actions.setModEnabled(profile.id, modId, false));
  });
  api.events.emit("mods-enabled", plan.disableModIds, false, request.gameId, changeOptions);
  if (plan.enableTarget) {
    api.store.dispatch(vortex.actions.setModEnabled(profile.id, request.stagedModId, true));
    api.events.emit("mods-enabled", [request.stagedModId], true, request.gameId, changeOptions);
  }

  try {
    await deployMods(api);
    return {
      status: "switched",
      profileId: profile.id,
      disabledModIds: plan.disableModIds,
      enabledModId: request.stagedModId,
      deployment: "completed",
    };
  } catch (error) {
    vortex.log("error", "Grailwright switched the active mod version but deployment failed", {
      modId: request.stagedModId,
      error: error.message,
    });
    if (typeof api.showErrorNotification === "function") {
      api.showErrorNotification(
        "Grailwright version switch needs deployment",
        error,
        { allowReport: false },
      );
    }
    return {
      status: "switched",
      profileId: profile.id,
      disabledModIds: plan.disableModIds,
      enabledModId: request.stagedModId,
      deployment: "failed",
      deploymentError: error.message,
    };
  }
}

async function failRequest(bridgeRoot, requestPath, request, error) {
  const failedRoot = path.join(bridgeRoot, "failed");
  const acknowledgementRoot = path.join(bridgeRoot, "acknowledgements");
  await Promise.all([
    fs.promises.mkdir(failedRoot, { recursive: true }),
    fs.promises.mkdir(acknowledgementRoot, { recursive: true }),
  ]);
  await writeJsonAtomic(path.join(acknowledgementRoot, `${request.requestId}.json`), {
    schemaVersion: 1,
    requestId: request.requestId,
    status: "failed",
    failedAt: new Date().toISOString(),
    error: error.message,
  });
  await fs.promises.rename(requestPath, path.join(failedRoot, path.basename(requestPath)));
  vortex.log("error", "Grailwright Nexus metadata promotion failed", {
    requestId: request.requestId,
    error: error.message,
  });
}

function findStagedMod(api, request) {
  const state = api.getState();
  const activeGameId = vortex.selectors.activeGameId(state);
  if (activeGameId !== request.gameId) {
    throw new PendingPromotionError(`Waiting for '${request.gameId}' to be the active Vortex game.`);
  }
  const stagedMod = state?.persistent?.mods?.[request.gameId]?.[request.stagedModId];
  if (stagedMod === undefined) {
    throw new PendingPromotionError(`Waiting for Vortex to discover staged mod '${request.stagedModId}'.`);
  }
  const installationPath = stagedMod.installationPath || request.stagedModId;
  const vortexStagingRoot = typeof vortex.selectors.installPathForGame === "function"
    ? vortex.selectors.installPathForGame(state, request.gameId)
    : undefined;
  if (vortexStagingRoot !== undefined
      && path.resolve(request.stagingPath).toLowerCase()
        !== path.resolve(vortexStagingRoot, installationPath).toLowerCase()) {
    throw new Error("Metadata staging path does not match the Vortex mod entry.");
  }
  if (vortexStagingRoot === undefined
      && path.basename(path.resolve(request.stagingPath)).toLowerCase()
        !== String(installationPath).toLowerCase()) {
    throw new Error("Metadata staging folder does not match the Vortex mod entry.");
  }
  return { state, stagedMod };
}

async function discoverStagedMod(api, request) {
  const state = api.getState();
  if (state?.persistent?.mods?.[request.gameId]?.[request.stagedModId] !== undefined) {
    return false;
  }
  if (vortex.selectors.activeGameId(state) !== request.gameId) {
    throw new PendingPromotionError(`Waiting for '${request.gameId}' to be the active Vortex game.`);
  }
  if (path.basename(request.stagedModId) !== request.stagedModId) {
    throw new Error("Staged mod ID is not a safe Vortex installation folder name.");
  }
  const vortexStagingRoot = typeof vortex.selectors.installPathForGame === "function"
    ? vortex.selectors.installPathForGame(state, request.gameId)
    : undefined;
  if (!vortexStagingRoot) {
    throw new PendingPromotionError("Waiting for Vortex to expose its mod staging path.");
  }
  const expectedStagingPath = path.resolve(vortexStagingRoot, request.stagedModId);
  if (path.resolve(request.stagingPath).toLowerCase() !== expectedStagingPath.toLowerCase()) {
    throw new Error("Metadata staging path is outside the expected Vortex mod folder.");
  }
  let stats;
  try {
    stats = await fs.promises.stat(expectedStagingPath);
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new PendingPromotionError(`Waiting for staged mod folder '${request.stagedModId}'.`);
    }
    throw error;
  }
  if (!stats.isDirectory()) {
    throw new Error("Queued Vortex staging path is not a directory.");
  }

  await new Promise((resolve, reject) => {
    let settled = false;
    let timer;
    const finish = (error) => {
      if (settled) {
        return;
      }
      settled = true;
      clearTimeout(timer);
      if (error != null) {
        reject(error instanceof Error ? error : new Error(String(error)));
      } else {
        resolve();
      }
    };
    timer = setTimeout(() => {
      finish(new PendingPromotionError("Waiting for Vortex to register the staged mod."));
    }, CREATE_MOD_TIMEOUT_MS);
    api.events.emit("create-mod", request.gameId, {
      id: request.stagedModId,
      type: "",
      installationPath: request.stagedModId,
      state: "installed",
      attributes: buildInitialStagedAttributes(request, new Date(stats.ctime).toString()),
    }, finish);
  });
  vortex.log("info", "Grailwright registered an externally staged mod with Vortex", {
    modId: request.stagedModId,
  });
  return true;
}

async function processLocalGroupingCatalog(api, bridgeRoot, requestPath, request) {
  const state = api.getState();
  if (vortex.selectors.activeGameId(state) !== request.gameId) {
    throw new PendingPromotionError(`Waiting for '${request.gameId}' to be the active Vortex game.`);
  }
  const mods = state?.persistent?.mods?.[request.gameId] || {};
  let matched = 0;
  for (const stagedMod of Object.values(mods)) {
    const catalogEntry = request.mods.find((entry) => {
      const escapedPrefix = entry.stagedNamePrefix.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
      return new RegExp(`^${escapedPrefix} [0-9]+\\.[0-9]+\\.[0-9]+$`, "i").test(stagedMod.id);
    });
    if (catalogEntry === undefined) {
      continue;
    }
    const versionMatch = stagedMod.id.match(/ ([0-9]+\.[0-9]+\.[0-9]+)$/);
    const localRequest = {
      gameId: request.gameId,
      displayName: catalogEntry.displayName,
      version: versionMatch?.[1] || stagedMod.attributes?.version || "",
      grouping: {
        source: "grailwright-local",
        modId: catalogEntry.modId,
        logicalFileName: catalogEntry.logicalFileName,
        nexusUrl: catalogEntry.nexusUrl,
      },
    };
    const attributes = core.buildLocalGroupingAttributes(localRequest, stagedMod.attributes);
    await setAndVerifyModAttributes(api, request.gameId, stagedMod.id, attributes);
    matched += 1;
  }

  const acknowledgementRoot = path.join(bridgeRoot, "acknowledgements");
  await fs.promises.mkdir(acknowledgementRoot, { recursive: true });
  await writeJsonAtomic(path.join(acknowledgementRoot, `${request.requestId}.json`), {
    schemaVersion: 1,
    requestId: request.requestId,
    status: "catalog-grouped",
    completedAt: new Date().toISOString(),
    gameId: request.gameId,
    catalogMods: request.mods.length,
    matchedModRecords: matched,
  });
  await fs.promises.unlink(requestPath);
  vortex.log("info", "Grailwright grouped staged mod catalog", {
    catalogMods: request.mods.length,
    matchedModRecords: matched,
  });
}

async function reconcileGroupingCatalog(api, catalogState) {
  if (!catalogState.available) {
    return 0;
  }
  const state = api.getState();
  if (vortex.selectors.activeGameId(state) !== GAME_ID) {
    return 0;
  }
  const mods = state?.persistent?.mods?.[GAME_ID] || {};
  let repaired = 0;
  for (const stagedMod of Object.values(mods)) {
    const catalogEntry = catalogState.entries.find((entry) => core.catalogIdentifiesMod(entry, stagedMod));
    if (catalogEntry === undefined) {
      continue;
    }
    const version = stagedMod.attributes?.version
      || String(stagedMod.id || "").match(/ ([0-9]+\.[0-9]+\.[0-9]+)$/)?.[1];
    if (!version) {
      continue;
    }
    const existingModId = core.asPositiveInteger(stagedMod.attributes?.modId);
    const expectedModId = core.asPositiveInteger(catalogEntry.modId);
    const verifiedNexus = stagedMod.attributes?.source === "nexus"
      && core.asPositiveInteger(stagedMod.attributes?.fileId) !== undefined;
    if (verifiedNexus && existingModId !== undefined && expectedModId !== undefined
        && existingModId !== expectedModId) {
      continue;
    }
    const attributes = core.buildLocalGroupingAttributes({
      gameId: GAME_ID,
      displayName: catalogEntry.displayName,
      version,
      grouping: {
        source: "grailwright-local",
        modId: catalogEntry.modId,
        logicalFileName: catalogEntry.logicalFileName,
        nexusUrl: catalogEntry.nexusUrl,
      },
    }, stagedMod.attributes);
    const result = await setAndVerifyModAttributes(api, GAME_ID, stagedMod.id, attributes);
    if (result.changed) {
      repaired += 1;
    }
  }
  if (repaired > 0) {
    vortex.log("info", "Grailwright repaired staged mod grouping metadata", { repaired });
  }
  return repaired;
}

async function processLocalGroupingRequest(api, bridgeRoot, requestPath) {
  const request = JSON.parse(await fs.promises.readFile(requestPath, "utf8"));
  core.validateRequest(request);
  if (request.requestType === "local-grouping-catalog") {
    await processLocalGroupingCatalog(api, bridgeRoot, requestPath, request);
    return;
  }
  if (request.requestType !== "local-grouping") {
    throw new Error("Non-grouping request was placed in the local grouping queue.");
  }
  if (request.gameId !== GAME_ID) {
    throw new Error(`Unsupported grouping game '${request.gameId}'.`);
  }
  await discoverStagedMod(api, request);
  const { stagedMod } = findStagedMod(api, request);
  await completeLocalGroupingRequest(api, bridgeRoot, requestPath, request, stagedMod);
}

async function processRequest(api, bridgeRoot, requestPath) {
  const request = JSON.parse(await fs.promises.readFile(requestPath, "utf8"));
  core.validateRequest(request);
  if (request.gameId !== GAME_ID) {
    throw new Error(`Unsupported promotion game '${request.gameId}'.`);
  }
  const expectedArchiveRoot = path.resolve(bridgeRoot, "archives", request.requestId);
  const resolvedArchivePath = path.resolve(request.archivePath);
  if (path.dirname(resolvedArchivePath).toLowerCase() !== expectedArchiveRoot.toLowerCase()) {
    throw new Error("Promotion archive is outside its guarded Vortex bridge directory.");
  }
  request.archivePath = resolvedArchivePath;

  await discoverStagedMod(api, request);
  let { state } = findStagedMod(api, request);

  await verifyStaging(request);
  let existingDownload = findExistingDownload(state, request);
  await verifyArchive(request, existingDownload);

  const metadataResults = await api.lookupModMeta({
    fileName: request.archive.fileName,
    filePath: existingDownload === undefined ? request.archivePath : undefined,
    fileMD5: request.archive.md5,
    fileSize: Number(request.archive.sizeBytes),
    gameId: request.gameId,
  });
  const metadata = core.findMatchingNexusMetadata(metadataResults, request);
  if (metadata === undefined) {
    throw new PendingPromotionError("Waiting for Vortex metadata lookup to expose the exact Nexus file.");
  }

  let archiveId = existingDownload?.id;
  if (archiveId === undefined) {
    archiveId = await importArchive(api, request.archivePath);
    state = api.getState();
    existingDownload = state?.persistent?.downloads?.files?.[archiveId];
    if (existingDownload === undefined
        || core.normalizedHash(existingDownload.fileMD5) !== core.normalizedHash(request.archive.md5)) {
      throw new PendingPromotionError("Imported Vortex download has not retained the expected archive hash yet.");
    }
  }

  await completeRequest(api, bridgeRoot, requestPath, request, archiveId, metadata);
}

function getVortexUserDataPath(context) {
  if (typeof context?.api?.getVortexPath === "function") {
    return context.api.getVortexPath("userData");
  }
  if (typeof vortex?.util?.getVortexPath === "function") {
    return vortex.util.getVortexPath("userData");
  }
  throw new Error("This Vortex build does not expose a user-data path resolver.");
}

function main(context) {
  context.once(() => {
    const bridgeRoot = path.join(getVortexUserDataPath(context), "grailwright-nexus-metadata");
    const requestsRoot = path.join(bridgeRoot, "requests");
    const groupingRequestsRoot = path.join(bridgeRoot, "grouping-requests");
    let processing = false;
    let lastReadinessState = "";
    const pendingLogState = new Map();

    const refreshCollectionReadiness = async () => {
      const state = context.api.getState();
      const profile = typeof vortex.selectors.activeProfile === "function"
        ? vortex.selectors.activeProfile(state)
        : undefined;
      if (profile === undefined || profile.gameId !== GAME_ID) {
        return;
      }
      const catalogState = await loadGroupingCatalog(bridgeRoot);
      await reconcileGroupingCatalog(context.api, catalogState);
      const refreshedState = context.api.getState();
      const readiness = core.buildCollectionReadiness(
        refreshedState?.persistent?.mods?.[GAME_ID] || {},
        (typeof vortex.selectors.activeProfile === "function"
          ? vortex.selectors.activeProfile(refreshedState)
          : profile)?.modState || {},
        catalogState,
      );
      const signature = JSON.stringify({ profileId: profile.id, ...readiness });
      if (signature === lastReadinessState) {
        return;
      }
      lastReadinessState = signature;
      await writeJsonAtomic(path.join(bridgeRoot, "collection-readiness.json"), {
        schemaVersion: 2,
        refreshedAt: new Date().toISOString(),
        gameId: GAME_ID,
        profileId: profile.id,
        profileName: profile.name,
        ...readiness,
      });
    };

    const processRequestDirectory = async (root, handler, queueName) => {
      await fs.promises.mkdir(root, { recursive: true });
      const requestFiles = (await fs.promises.readdir(root))
        .filter((name) => name.endsWith(".json"))
        .sort();
      for (const requestFile of requestFiles) {
        const requestPath = path.join(root, requestFile);
        const pendingKey = `${queueName}|${requestFile}`;
        try {
          await handler(context.api, bridgeRoot, requestPath);
          pendingLogState.delete(pendingKey);
        } catch (error) {
          if (error instanceof PendingPromotionError || error?.code === "ENOENT") {
            const now = Date.now();
            const reason = error.message;
            const previous = pendingLogState.get(pendingKey);
            if (core.shouldLogPending(previous, reason, now, PENDING_LOG_REPEAT_MS)) {
              vortex.log("debug", `Grailwright ${queueName} request remains pending`, {
                request: requestFile,
                reason,
              });
              pendingLogState.set(pendingKey, { reason, loggedAt: now });
            }
          } else {
            pendingLogState.delete(pendingKey);
            let request = { requestId: path.basename(requestFile, ".json") };
            try {
              request = JSON.parse(await fs.promises.readFile(requestPath, "utf8"));
            } catch (_) {}
            await failRequest(bridgeRoot, requestPath, request, error);
          }
        }
      }
    };

    const processQueue = async () => {
      if (processing) {
        return;
      }
      processing = true;
      try {
        await processRequestDirectory(requestsRoot, processRequest, "Nexus promotion");
        await processRequestDirectory(groupingRequestsRoot, processLocalGroupingRequest, "local grouping");
        await refreshCollectionReadiness();
      } catch (error) {
        vortex.log("error", "Could not process Grailwright Nexus metadata queue", { error: error.message });
      } finally {
        processing = false;
      }
    };

    processQueue();
    const timer = setInterval(processQueue, POLL_INTERVAL_MS);
    if (typeof timer.unref === "function") {
      timer.unref();
    }
  });
  return true;
}

module.exports = {
  activateNewLocalVersion,
  buildInitialStagedAttributes,
  default: main,
  deployMods,
  discoverStagedMod,
  getVortexUserDataPath,
  reconcileGroupingCatalog,
  setAndVerifyModAttributes,
};
