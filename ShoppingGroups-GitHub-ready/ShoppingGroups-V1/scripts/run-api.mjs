import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { loadNeonEnvironment } from './neon-env.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const env = loadNeonEnvironment(root);
if (![env.ConnectionStrings__Database, env.DATABASE_URL_UNPOOLED, env.DATABASE_URL].some(value => value?.trim())) {
  console.error('No database connection configured. Run neon init and neon deploy, or set DATABASE_URL in a private .env.local file.');
  process.exit(1);
}
env.ASPNETCORE_ENVIRONMENT ??= 'Development';
env.ASPNETCORE_URLS ??= 'http://127.0.0.1:5080';
env.PublicUrl ??= 'http://localhost:5173';
env.DataProtection__Storage ??= 'Database';
env.Maintenance__IntervalSeconds ??= '900';
const api = spawn('dotnet', ['run', '--project', 'src/ShoppingGroups.Api', ...process.argv.slice(2)], { cwd: root, env, stdio: 'inherit' });
api.on('error', () => {
  console.error('Could not start dotnet. Install the .NET 10 SDK and retry.');
  process.exitCode = 1;
});
api.on('exit', code => { process.exitCode = code ?? 1; });
