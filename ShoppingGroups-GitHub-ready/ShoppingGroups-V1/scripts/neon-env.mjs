import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseEnv } from 'node:util';

// Match Neon CLI: an existing .env is used; otherwise .env.local is used.
// Only the server process receives these values. Never pass them to Vite.
export function loadNeonEnvironment(root, inherited = process.env) {
  const env = { ...inherited };
  const file = ['.env', '.env.local'].map(name => join(root, name)).find(existsSync);
  if (file) {
    const values = parseEnv(readFileSync(file, 'utf8'));
    for (const [key, value] of Object.entries(values)) {
      if (env[key] === undefined) env[key] = value;
    }
  }
  return env;
}
