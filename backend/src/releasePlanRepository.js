import { readFile as readFileAsync } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const releasePlanPath = path.resolve(__dirname, "../data/release-plan.json");

let cachedReleasePlan = null;

export async function getReleasePlan() {
  if (!cachedReleasePlan) {
    const raw = await readFileAsync(releasePlanPath, "utf8");
    cachedReleasePlan = JSON.parse(raw);
  }

  return cachedReleasePlan;
}
