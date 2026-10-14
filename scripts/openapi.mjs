import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const result = spawnSync('dotnet', [
  'build', 'src/MiettisPark.Api/MiettisPark.Api.csproj',
  '--configuration', 'Release', '--no-incremental',
  '-p:OpenApiGenerateDocuments=true'
], { cwd: root, stdio: 'inherit' })

if (result.error) throw result.error
if (result.status !== 0) process.exit(result.status ?? 1)

const documentPath = new URL('../src/MiettisPark.Api/OpenApi/miettis-park.json', import.meta.url)
const document = JSON.parse(readFileSync(documentPath, 'utf8'))
if (!document.openapi || !document.paths?.['/api/health']) {
  throw new Error('Built-in OpenAPI output is missing or does not contain the foundation endpoint.')
}
console.log(`OpenAPI generated: ${fileURLToPath(documentPath)}`)
