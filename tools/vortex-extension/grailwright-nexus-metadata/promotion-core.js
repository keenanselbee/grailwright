"use strict";

function asPositiveInteger(value) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function normalizedHash(value) {
  return String(value || "").trim().toLowerCase();
}

function normalizedIdentity(value) {
  return String(value || "").trim().toLowerCase();
}

function parseVersion(value) {
  const match = String(value || "").trim().match(/^(\d+)\.(\d+)\.(\d+)$/);
  return match === null ? undefined : match.slice(1).map(Number);
}

function compareVersions(left, right) {
  const leftParts = parseVersion(left);
  const rightParts = parseVersion(right);
  if (leftParts === undefined || rightParts === undefined) {
    return undefined;
  }
  for (let index = 0; index < leftParts.length; index += 1) {
    if (leftParts[index] !== rightParts[index]) {
      return leftParts[index] < rightParts[index] ? -1 : 1;
    }
  }
  return 0;
}

function modVersion(mod) {
  if (parseVersion(mod?.attributes?.version) !== undefined) {
    return mod.attributes.version;
  }
  return String(mod?.id || "").match(/ (\d+\.\d+\.\d+)$/)?.[1];
}

function buildVariantActivationPlan(mods, modState, targetModId, targetAttributes) {
  const targetVersion = targetAttributes?.version;
  if (parseVersion(targetVersion) === undefined) {
    return { switch: false, reason: "target-version-unknown" };
  }
  const targetLogicalName = normalizedIdentity(
    targetAttributes?.logicalFileName || targetAttributes?.name,
  );
  if (targetLogicalName === "") {
    return { switch: false, reason: "target-identity-unknown" };
  }
  const targetNexusModId = asPositiveInteger(targetAttributes?.modId);
  const enabledSiblings = Object.entries(mods || {})
    .filter(([id, mod]) => {
      if (id === targetModId || modState?.[id]?.enabled !== true) {
        return false;
      }
      const attributes = mod?.attributes || {};
      const siblingLogicalName = normalizedIdentity(attributes.logicalFileName || attributes.name);
      if (siblingLogicalName !== targetLogicalName) {
        return false;
      }
      const siblingNexusModId = asPositiveInteger(attributes.modId);
      return targetNexusModId === undefined || siblingNexusModId === undefined
        || siblingNexusModId === targetNexusModId;
    })
    .map(([id, mod]) => ({ id, version: modVersion(mod) }));

  const targetEnabled = modState?.[targetModId]?.enabled === true;
  if (enabledSiblings.length === 0) {
    return {
      switch: false,
      reason: targetEnabled ? "already-current" : "group-disabled",
    };
  }
  if (enabledSiblings.some((entry) => parseVersion(entry.version) === undefined)) {
    return { switch: false, reason: "enabled-version-unknown" };
  }
  if (enabledSiblings.some((entry) => compareVersions(entry.version, targetVersion) >= 0)) {
    return { switch: false, reason: "enabled-version-not-older" };
  }
  return {
    switch: true,
    disableModIds: enabledSiblings.map((entry) => entry.id),
    enableTarget: !targetEnabled,
  };
}

function findMatchingNexusMetadata(results, request) {
  const expectedModId = asPositiveInteger(request?.nexus?.modId);
  const expectedFileId = asPositiveInteger(request?.nexus?.fileId);
  const expectedMd5 = normalizedHash(request?.archive?.md5);
  const expectedSize = Number(request?.archive?.sizeBytes);

  return (results || []).map((entry) => entry?.value || entry).find((meta) => {
    const details = meta?.details || {};
    return meta?.source === "nexus"
      && asPositiveInteger(details.modId) === expectedModId
      && asPositiveInteger(details.fileId) === expectedFileId
      && normalizedHash(meta.fileMD5) === expectedMd5
      && Number(meta.fileSizeBytes) === expectedSize;
  });
}

function buildModAttributes(request, metadata, archiveLocalPath) {
  const details = metadata?.details || {};
  const remoteName = request.archive.remoteFileName || metadata?.fileName;
  return {
    source: "nexus",
    modId: asPositiveInteger(request.nexus.modId),
    fileId: asPositiveInteger(request.nexus.fileId),
    version: request.version,
    fileMD5: normalizedHash(request.archive.md5),
    fileSize: Number(request.archive.sizeBytes),
    fileName: archiveLocalPath || remoteName || request.archive.fileName,
    logicalFileName: request.nexus.logicalFileName || metadata?.logicalFileName || request.displayName,
    downloadGame: request.gameId,
    homepage: request.nexus.url || details.homepage,
    author: details.author,
    description: details.description,
    fileType: String(request.nexus.category || metadata?.category || "").toUpperCase(),
    isPrimary: Boolean(request.nexus.isPrimary),
    grailwrightCollectionReady: true,
  };
}

function buildLocalGroupingAttributes(request, existingAttributes = {}) {
  const requestedModId = asPositiveInteger(request?.grouping?.modId);
  const existingModId = asPositiveInteger(existingAttributes?.modId);
  const isVerifiedNexus = existingAttributes?.source === "nexus"
    && asPositiveInteger(existingAttributes?.fileId) !== undefined;
  if (isVerifiedNexus && requestedModId !== undefined && existingModId !== undefined
      && requestedModId !== existingModId) {
    throw new Error("Local grouping request conflicts with the staged mod's verified Nexus page.");
  }

  const attributes = {
    name: request.displayName,
    version: request.version,
    logicalFileName: request.grouping.logicalFileName,
    downloadGame: request.gameId,
    grailwrightCollectionReady: isVerifiedNexus,
  };
  if (!isVerifiedNexus) {
    attributes.source = "grailwright-local";
  }
  if (requestedModId !== undefined && (!isVerifiedNexus || existingModId === undefined)) {
    attributes.modId = requestedModId;
  }
  if (request.grouping.nexusUrl) {
    attributes.homepage = request.grouping.nexusUrl;
  }
  return attributes;
}

function catalogIdentifiesMod(entry, mod) {
  const attributes = mod?.attributes || {};
  const escapedPrefix = String(entry?.stagedNamePrefix || "").replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  if (escapedPrefix !== ""
      && new RegExp(`^${escapedPrefix} [0-9]+\\.[0-9]+\\.[0-9]+$`, "i").test(String(mod?.id || ""))) {
    return true;
  }

  const logicalFileName = String(attributes.logicalFileName || attributes.name || "").trim().toLowerCase();
  const expectedLogicalFileName = String(entry?.logicalFileName || entry?.displayName || "").trim().toLowerCase();
  if (logicalFileName === "" || logicalFileName !== expectedLogicalFileName) {
    return false;
  }
  return true;
}

function buildCollectionReadiness(mods, modState, catalogState = {}) {
  const catalog = Array.isArray(catalogState) ? catalogState : (catalogState.entries || []);
  const catalogAvailable = Array.isArray(catalogState)
    ? catalog.length > 0
    : catalogState.available === true;
  const invalidCatalogRecordCount = Array.isArray(catalogState)
    ? 0
    : Number(catalogState.invalidRecordCount || 0);
  const entries = Object.entries(modState || {}).filter(([, state]) => state?.enabled === true)
    .map(([id]) => {
      const mod = mods?.[id];
      const catalogEntry = catalog.find((entry) => catalogIdentifiesMod(entry, mod || { id }));
      if (mod === undefined) {
        if (catalogEntry === undefined) {
          return undefined;
        }
        const versionMatch = String(id).match(/ ([0-9]+\.[0-9]+\.[0-9]+)$/);
        return {
          stagedModId: id,
          displayName: catalogEntry.displayName || id,
          version: versionMatch?.[1] || "",
          source: "",
          modId: undefined,
          expectedModId: asPositiveInteger(catalogEntry.modId),
          fileId: undefined,
          coverage: "missing-vortex-record",
          ready: false,
        };
      }
      const attributes = mod.attributes || {};
      const verifiedNexusMetadata = attributes.source === "nexus"
        && asPositiveInteger(attributes.modId) !== undefined
        && asPositiveInteger(attributes.fileId) !== undefined;
      const metadataMarked = attributes.source === "grailwright-local"
        || attributes.grailwrightCollectionReady !== undefined;
      if (!metadataMarked && catalogEntry === undefined) {
        return undefined;
      }
      const catalogCovered = catalogEntry !== undefined;
      const metadataCovered = metadataMarked || verifiedNexusMetadata;
      const expectedModId = asPositiveInteger(catalogEntry?.modId);
      const actualModId = asPositiveInteger(attributes.modId);
      const nexusIdentityMismatch = expectedModId !== undefined
        && actualModId !== undefined
        && actualModId !== expectedModId;
      const ready = catalogCovered
        && metadataCovered
        && !nexusIdentityMismatch
        && verifiedNexusMetadata
        && (expectedModId === undefined || actualModId === expectedModId)
        && attributes.grailwrightCollectionReady !== false;
      const versionMatch = String(mod.id || "").match(/ ([0-9]+\.[0-9]+\.[0-9]+)$/);
      return {
        stagedModId: mod.id,
        displayName: attributes.name || attributes.logicalFileName || catalogEntry?.displayName || mod.id,
        version: attributes.version || versionMatch?.[1] || "",
        source: attributes.source || "",
        modId: actualModId,
        expectedModId,
        fileId: asPositiveInteger(attributes.fileId),
        coverage: !catalogCovered
          ? "missing-catalog"
          : (nexusIdentityMismatch
            ? "nexus-id-mismatch"
            : (!metadataCovered ? "missing-metadata" : "covered")),
        ready,
      };
    })
    .filter((entry) => entry !== undefined)
    .sort((left, right) => left.displayName.localeCompare(right.displayName));
  const unaccountedEnabledCount = entries.filter((entry) => entry.coverage !== "covered").length;
  return {
    allReady: catalogAvailable
      && invalidCatalogRecordCount === 0
      && entries.length > 0
      && unaccountedEnabledCount === 0
      && entries.every((entry) => entry.ready),
    catalogAvailable,
    catalogModCount: catalog.length,
    invalidCatalogRecordCount,
    managedEnabledCount: entries.length,
    unaccountedEnabledCount,
    entries,
  };
}

function shouldLogPending(previous, reason, now, repeatMs) {
  return previous === undefined
    || previous.reason !== reason
    || now - previous.loggedAt >= repeatMs;
}

function validateRequest(request) {
  if (request?.schemaVersion !== 1 || !/^[a-f0-9]{24}$/.test(String(request.requestId || "")) || !request.gameId) {
    throw new Error("Invalid Grailwright Nexus metadata promotion request.");
  }
  if (request.requestType === "local-grouping-catalog") {
    if (!Array.isArray(request.mods) || request.mods.length === 0
        || request.mods.some((entry) => !entry.packageName || !entry.displayName
          || !entry.stagedNamePrefix || !entry.logicalFileName
          || (entry.modId != null && asPositiveInteger(entry.modId) === undefined))) {
      throw new Error("Invalid Grailwright local grouping catalog.");
    }
    return;
  }
  if (!request.stagedModId || !request.stagingPath) {
    throw new Error("Invalid Grailwright Nexus metadata promotion request.");
  }
  if (request.requestType === "local-grouping") {
    if (!request.displayName || !request.version || !request?.grouping?.logicalFileName
        || request.grouping.source !== "grailwright-local") {
      throw new Error("Invalid Grailwright local grouping request.");
    }
    if (request.grouping.modId != null && asPositiveInteger(request.grouping.modId) === undefined) {
      throw new Error("Local grouping request contains an invalid Nexus mod ID.");
    }
    return;
  }
  if (request.requestType !== undefined && request.requestType !== "nexus-promotion") {
    throw new Error(`Unsupported Grailwright metadata request type '${request.requestType}'.`);
  }
  if (!request.archivePath) {
    throw new Error("Invalid Grailwright Nexus metadata promotion request.");
  }
  if (!asPositiveInteger(request?.nexus?.modId) || !asPositiveInteger(request?.nexus?.fileId)) {
    throw new Error("Promotion request is missing a positive Nexus mod ID or file ID.");
  }
  if (!/^[a-f0-9]{32}$/.test(normalizedHash(request?.archive?.md5))
      || !/^[a-f0-9]{64}$/.test(normalizedHash(request?.archive?.sha256))) {
    throw new Error("Promotion request contains invalid archive hashes.");
  }
  if (!Array.isArray(request.payload) || request.payload.length === 0) {
    throw new Error("Promotion request has no staged payload manifest.");
  }
}

module.exports = {
  asPositiveInteger,
  buildVariantActivationPlan,
  buildCollectionReadiness,
  buildLocalGroupingAttributes,
  buildModAttributes,
  catalogIdentifiesMod,
  findMatchingNexusMetadata,
  normalizedHash,
  shouldLogPending,
  validateRequest,
};
