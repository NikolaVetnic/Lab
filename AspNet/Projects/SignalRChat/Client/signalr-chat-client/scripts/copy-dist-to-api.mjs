import { cp, mkdir, rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const clientDirectory = resolve(scriptDirectory, "..");
const repoDirectory = resolve(clientDirectory, "../..");

const distDirectory = resolve(clientDirectory, "dist");
const apiWwwrootDirectory = resolve(
  repoDirectory,
  "Api/SignalRChat.Api/wwwroot",
);

await rm(apiWwwrootDirectory, { recursive: true, force: true });
await mkdir(apiWwwrootDirectory, { recursive: true });
await cp(distDirectory, apiWwwrootDirectory, { recursive: true });

console.log(`Copied React build to ${apiWwwrootDirectory}`);
