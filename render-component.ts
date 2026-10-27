// Static React rendering checks markup and account context without a browser or network.
import { readFileSync, existsSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";
import ts from "typescript";
const nodeRequire = createRequire(import.meta.url);
const modules = new Map<string, { exports: any }>();
function load(filename: string): any {
  if (modules.has(filename)) return modules.get(filename)!.exports;
  const module = { exports: {} as any }; modules.set(filename, module);
  const output = ts.transpileModule(readFileSync(filename, "utf8"), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2023, jsx: ts.JsxEmit.ReactJSX } }).outputText;
  const localRequire = (name: string) => {
    if (!name.startsWith(".")) return nodeRequire(name);
    const base = resolve(dirname(filename), name);
    const file = [base, base + ".tsx", base + ".ts"].find(path => existsSync(path));
    if (!file) throw new Error(`Missing UI module: ${name}`);
    return load(file);
  };
  vm.runInThisContext(`(function(require,module,exports){${output}\n})`, { filename })(localRequire, module, module.exports);
  return module.exports;
}
export function component(name: string): any { return load(fileURLToPath(new URL(`../src/${name}.tsx`, import.meta.url))); }
