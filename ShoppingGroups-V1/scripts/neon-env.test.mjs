import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { loadNeonEnvironment } from './neon-env.mjs';

function directory(t) {
  const root = mkdtempSync(join(tmpdir(), 'shopping-neon-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  return root;
}
test('Neon .env.local loads quoted server credentials without changing inherited variables', t => {
  const root = directory(t);
  writeFileSync(join(root, '.env.local'), 'DATABASE_URL="postgres://user:p%23%3B%3D@host/db?sslmode=require"\nPublicUrl="https://file.example"\n');
  const inherited = { PublicUrl: 'https://process.example' };
  const env = loadNeonEnvironment(root, inherited);
  assert.equal(env.DATABASE_URL, 'postgres://user:p%23%3B%3D@host/db?sslmode=require');
  assert.equal(env.PublicUrl, 'https://process.example');
  assert.equal(inherited.DATABASE_URL, undefined);
});
test('Neon existing .env wins over .env.local without merging stale credentials', t => {
  const root = directory(t);
  writeFileSync(join(root, '.env'), 'DATABASE_URL=postgres://u:p@current/db\n');
  writeFileSync(join(root, '.env.local'), 'DATABASE_URL=postgres://u:p@stale/db\nDATABASE_URL_UNPOOLED=postgres://u:p@stale-direct/db\n');
  const env = loadNeonEnvironment(root, {});
  assert.equal(env.DATABASE_URL, 'postgres://u:p@current/db');
  assert.equal(env.DATABASE_URL_UNPOOLED, undefined);
});
test('Server environment works without local credential files', t => {
  const root = directory(t);
  assert.deepEqual(loadNeonEnvironment(root, { DATABASE_URL_UNPOOLED: 'postgres://u:p@direct/db' }), { DATABASE_URL_UNPOOLED: 'postgres://u:p@direct/db' });
});
