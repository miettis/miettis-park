# Miettis Park

.NET-, PostgreSQL- ja Vue/Nuxt-pohjainen pysäköintihallisimulaatio rekrytoinnin ennakkotehtävään.

- [SPEC.md](SPEC.md): toiminnallinen ja tekninen sopimus.
- [STARTING-POINT.md](STARTING-POINT.md): pohjan sisältö ja valmiustarkistus.
- [BACKLOG.md](BACKLOG.md): toiminnalliset työpaketit.
- [AI-USAGE.md](AI-USAGE.md): AI:n käyttö.
- [VALIDATION.md](VALIDATION.md): suoritetut tarkistukset ja avoimet validoinnit.
- [Käyttöliittymäluonnokset](ui-mocks/index.html): erillinen prototyyppi esimerkkidatalla.

## Toteutettu pohja

`src/MiettisPark.Api` sisältää .NET 10 -hostin, EF Core/Npgsql-mallin, alkumigraation sekä siemendatan: yksi halli, viisi paikkaa ja kaksi hintaporrasta. Hinnasto tallennetaan jsonb-taulukkona, kestot ovat C#:ssa TimeSpan-arvoja ja aikaleimat UTC DateTimeOffset -arvoja. Paikan valinnainen VisitId mahdollistaa tunnistamattomat paikkahavainnot.

`src/client` on Nuxt/Vue SPA yhdellä tyhjällä aloitusnäkymällä. Pysäköinnin REST API, servicet, komponentit, reitit ja toiminnalliset testit toteutetaan backlogissa. Hostissa on vain infrastruktuurin `/api/health`-endpoint, joka ei tarkista tietokannan valmiutta.

SDK-versio on global.json-tiedostossa, Node-versio .node-version-tiedostossa. NuGet- ja npm-riippuvuudet on lukittu. Tarvitset .NET SDK:n, Node 24:n ja PostgreSQL 17:n tai Dockerin.

## Kehitys

Repojuuressa PowerShellillä:

```powershell
Copy-Item .env.example .env
# Vaihda .env-tiedoston POSTGRES_PASSWORD paikalliseksi salasanaksi.
docker compose up -d database
dotnet tool restore
$env:ConnectionStrings__Parking = 'Host=localhost;Database=miettis_park;Username=miettis_park;Password=OMA_PAIKALLINEN_SALASANA'
dotnet run --project src/MiettisPark.Api
```

Backend kuuntelee kehityksessä osoitteessa http://localhost:5080. Sovella .env-tiedostossa muuttamasi tietokannan nimi, käyttäjä ja portti myös yhteysmerkkijonoon. .NET ei lue .env-tiedostoa; Compose lukee. Vaihtoehtoisesti käytä omaa PostgreSQL-instanssia. Älä tallenna oikeaa salasanaa versionhallintaan. Compose-esimerkin salasanaan ei tule käyttää yhteysmerkkijonon erottimia, kuten puolipistettä.

Toisessa terminaalissa:

```powershell
cd src/client
npm ci
npm run dev
```

Avaa http://localhost:3000. Frontend käyttää suhteellista `/api`-polkua, jonka Nuxt proxy välittää kehityksessä backendille. `API_PROXY_TARGET` voi vaihtaa backend-originin; älä lisää siihen `/api`-päätettä. Tuotannossa proxyä tai Node-palvelinta ei ole.

`Database__InitializeOnStartup=true` (oletus) soveltaa migraatiot ennen HTTP-palvelimen käynnistymistä. Siemendata on migraatiossa; uudelleenkäynnistys ei nollaa dataa. Yhden instanssin simulaatiossa tämä on tarkoituksellinen yksinkertaistus. Tuotannon migraatioajoon voidaan käyttää erillistä käyttöoikeutta ja ajaa migraatiot ennen deploymentia.

## OpenAPI ilman käynnissä olevaa sovellusta

```powershell
node scripts/openapi.mjs
```

Tulos: `src/MiettisPark.Api/OpenApi/miettis-park.json`. Sama generointi tapahtuu tavallisen backend-buildin yhteydessä. Käytössä ovat **Microsoft.AspNetCore.OpenApi** ja **Microsoft.Extensions.ApiDescription.Server**, ei Swashbuckle/NSwag.

Microsoftin build-työkalu kutsuu entry pointia endpoint-metadatan löytämiseksi, mutta ei käynnistä kuuntelevaa HTTP-palvelinta. Program tunnistaa tämän ajon ja ohittaa migraatiot sekä startupin tietokantayhteyden. Generointi onnistuu ilman tietokantaa, yhteysmerkkijonoa tai vapaata palvelinporttia. Pidä mahdolliset tulevat ulkoiset startup-integraatiot saman suojauksen takana. Ajonaikainen kuvaus on `/openapi/v1.json`.

## Build ja EF

```powershell
dotnet restore MiettisPark.slnx --locked-mode
dotnet build MiettisPark.slnx -c Release
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/MiettisPark.Api --configuration Release
dotnet ef migrations script --idempotent --project src/MiettisPark.Api --configuration Release --output artifacts/migrations.sql
cd src/client
npm ci
npm run lint
npm run typecheck
npm run build
```

Migraation luonti ja SQL-generointi käyttävät design-time DbContextFactorya, eivät avaa tietokantayhteyttä. Uusi migraatio: `dotnet ef migrations add Nimi --project src/MiettisPark.Api`. Tietokantaan soveltaminen: `dotnet ef database update --project src/MiettisPark.Api`, kun `ConnectionStrings__Parking` on asetettu. Älä käytä `EnsureCreated`-kutsua migraatioiden rinnalla.

## Kontti ja julkaisu

```powershell
docker compose up --build -d
```

Sovellus: http://localhost:8080. Dockerfile rakentaa frontendin ja backendin yhteen ei-root-runtime-imageen; PostgreSQL on erillinen Compose-palvelu ja data nimetyssä volumessa. `docker compose down` säilyttää datan. `APP_PORT` ja `POSTGRES_PORT` ovat muutettavissa .env-tiedostossa.

GitHub Actions julkaisee imagen `ghcr.io/<owner>/<repository>` semanttisesta tagista, esimerkiksi `v1.2.3` tai `v1.2.3-rc.1`. Virheellinen versiotagi pysäyttää työn ennen julkaisua. Docker-tagit ovat versio ilman v-etuliitettä, `sha-<12 merkkiä>` ja vain vakaalle versiolle `latest`. SemVerin mahdollinen `+build` tallennetaan Docker-tagissa `_build`-muodossa; build-metadatatagi ei muuta latestia. Julkaisu käyttää GitHubin GITHUB_TOKENia ja packages:write-oikeutta. Imagea ei ole vielä julkaistu eikä workflowta ajettu GitHubissa.

## V2-suunnitelma

MVP:n jälkeinen hallien ylläpito, ruudukkopohjat ja kartta: [SPEC-V2.md](SPEC-V2.md) ja [BACKLOG-V2.md](BACKLOG-V2.md). Suunnitelma ei tarkoita, että ominaisuudet on toteutettu tai työitemit luotu PullPotentialiin.
