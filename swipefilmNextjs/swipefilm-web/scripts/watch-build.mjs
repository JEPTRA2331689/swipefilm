// ✅ Alternative à `next dev` pour tester les pages authentifiées : le cookie
// de session ne part pas cross-origin depuis next dev (localhost:3000) vers
// le backend (localhost:5250) — credentials: "same-origin" volontaire, voir
// lib/api.ts. Ce script rebuild l'export statique et le recopie dans
// wwwroot à chaque sauvegarde — pas besoin de relancer `dotnet run`, le
// middleware de fichiers statiques relit le disque à chaque requête.
//
// Usage : npm run watch:build
// Puis juste F5 dans le navigateur après chaque sauvegarde (build ~2-4s).

import { spawn } from "node:child_process";
import { watch } from "node:fs";
import { cp, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = path.resolve(__dirname, "..");
const OUT_DIR = path.join(WEB_ROOT, "out");
const BACKEND_WWWROOT = path.resolve(
  WEB_ROOT,
  "..",
  "..",
  "swipefilm",
  "swipefilm",
  "wwwroot",
);
const WATCH_DIRS = ["app", "components", "hooks", "lib", "types"].map((d) =>
  path.join(WEB_ROOT, d),
);

let building = false;
let pending = false;

const NEXT_BIN = path.join(
  WEB_ROOT,
  "node_modules",
  ".bin",
  process.platform === "win32" ? "next.cmd" : "next",
);

// ✅ .cmd shims exigent shell:true sur Windows (spawn EINVAL sinon) — la
// commande est un chemin local fixe, pas une entrée utilisateur, donc pas de
// risque d'injection ici malgré l'avertissement de dépréciation sur args[].
function run(cmdString) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmdString, { cwd: WEB_ROOT, stdio: "inherit", shell: true });
    child.on("exit", (code) =>
      code === 0 ? resolve() : reject(new Error(`${cmdString} exited ${code}`)),
    );
  });
}

async function build() {
  if (building) {
    pending = true;
    return;
  }
  building = true;
  const start = Date.now();
  console.log("\n[watch-build] Building…");

  try {
    await run(`"${NEXT_BIN}" build`);
    await rm(BACKEND_WWWROOT, { recursive: true, force: true });
    await cp(OUT_DIR, BACKEND_WWWROOT, { recursive: true });
    console.log(`[watch-build] Done in ${Date.now() - start}ms — refresh your browser.`);
  } catch (err) {
    console.error("[watch-build] Build failed:", err.message);
  } finally {
    building = false;
    if (pending) {
      pending = false;
      build();
    }
  }
}

let debounceTimer;
function onChange(eventType, filename) {
  if (!filename) return;
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(build, 300);
}

for (const dir of WATCH_DIRS) {
  try {
    watch(dir, { recursive: true }, onChange);
    console.log(`[watch-build] Watching ${path.relative(WEB_ROOT, dir)}/`);
  } catch (err) {
    console.error(`[watch-build] Could not watch ${dir}:`, err.message);
  }
}

console.log(`[watch-build] wwwroot target: ${BACKEND_WWWROOT}`);
build();
