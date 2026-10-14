import { spawnSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const clientDirectory = resolve(repositoryRoot, 'src', 'client')
const npmCli = process.env.npm_execpath

if (!npmCli) {
  console.error('Run this check through `npm run check` so the active npm CLI can be resolved.')
  process.exit(1)
}

function run(args) {
  const result = spawnSync(process.execPath, [npmCli, ...args], {
    cwd: clientDirectory,
    stdio: 'inherit'
  })
  if (result.error) {
    console.error(`Could not start npm: ${result.error.message}`)
    process.exit(1)
  }
  if (result.status !== 0) {
    process.exit(result.status ?? 1)
  }
}

run(['ci'])
run(['run', 'lint'])
run(['run', 'typecheck'])
run(['run', 'test'])
run(['run', 'build'])
