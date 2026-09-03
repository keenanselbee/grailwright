"use strict";

const path = require("path");
const vortex = require("vortex-api");

const GAME_ID = "taintedgrailthefallofavalon";
const GAME_EXECUTABLE = "Fall of Avalon.exe";
const HOOK_ID = "grailwright-foa-unelevated";
const TASK_NAME = "Grailwright Launch Tainted Grail";

function getTaskSchedulerPath() {
  return path.join(process.env.SystemRoot || "C:\\Windows", "System32", "schtasks.exe");
}

function samePath(left, right) {
  return path.resolve(left).toLowerCase() === path.resolve(right).toLowerCase();
}

function rewriteFoaLaunch(api, call) {
  const state = api.getState();
  if (vortex.selectors.activeGameId(state) !== GAME_ID) {
    return call;
  }

  const discovery = state?.settings?.gameMode?.discovered?.[GAME_ID];
  if (!discovery?.path || discovery.store !== vortex.util.steam.id) {
    return call;
  }

  const expectedExecutable = path.join(discovery.path, GAME_EXECUTABLE);
  if (!samePath(call.executable, expectedExecutable)) {
    return call;
  }

  const taskSchedulerPath = getTaskSchedulerPath();
  vortex.log("info", "Grailwright redirected Tainted Grail directly through the limited launch task");
  return {
    ...call,
    executable: taskSchedulerPath,
    args: ["/Run", "/TN", TASK_NAME],
    options: {
      ...call.options,
      shell: false,
      detach: true,
    },
  };
}

function main(context) {
  context.registerStartHook(200, HOOK_ID, async (call) => rewriteFoaLaunch(context.api, call));
  return true;
}

module.exports = {
  GAME_EXECUTABLE,
  GAME_ID,
  HOOK_ID,
  TASK_NAME,
  default: main,
  getTaskSchedulerPath,
  rewriteFoaLaunch,
  samePath,
};
