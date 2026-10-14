# Pohjan validointi 14.10.2026

## Suoritettu

- .NET Release build, publish ja lukittujen NuGet-riippuvuuksien restore.
- Microsoftin sisäänrakennettu build-time OpenAPI-generointi myös virheellisellä ConnectionStrings__Parking-arvolla ja startup-migraatioiden ollessa asetuksissa päällä. Generointi ei avannut tietokantaa tai HTTP-palvelinta.
- EF InitialCreate-migraation luonti, idempotentti PostgreSQL SQL-skripti ja tarkistus, ettei mallissa ole migraatiosta puuttuvia muutoksia.
- Offline EF-malliprobe: jsonb:n juuritaulukko, TimeSpan round-trip, hinnaston sisäisen muutoksen seuranta, yksi halli/viisi paikkaa, nullable VisitId, aktiivisuuden osittainen yksikäsitteisyysindeksi ja historian poistamisen estävät viiteavaimet.
- Nuxt lint, typecheck ja staattinen tuotantobuild.
- Julkaistun .NET-hostin HTTP-smoketesti staattisella frontendillä: aloitussivu, SPA-fallback, health, ajonaikainen OpenAPI sekä puuttuvan API-, OpenAPI- ja asset-polun 404.
- Nuxt-kehityspalvelimen suhteellinen /api-proxy samalle backendille. HTTP-testissä startup-migraatiot oli tarkoituksella pois, eikä tietokantaa käytetty.
- Compose-konfiguraation syntaksitarkistus (`docker compose config --quiet`), ilman konttien käynnistämistä.
- SemVer-julkaisumetadatan hyväksytyt vakaat/prerelease/build-versiot ja virheellisten versioiden hylkäys. Mitään ei julkaistu.

Offline-probe ja paikalliset HTTP-testiajurit ovat ignoroidussa artifacts-hakemistossa; ne eivät ole pysäköinnin toiminnallinen testiharness. Ylläpidettävät sovellustestit kuuluvat PARK-01-työpakettiin.

## Avoin validointi

- Docker-daemon ei ollut käytettävissä. Docker-imagea, Compose-käynnistystä tai oikeaa PostgreSQL-migraatiota/uudelleenkäynnistystä ei ole ajettu. Kontin tietokantayhteyttä tai SQL-rajoitteiden toimintaa kilpailussa ei väitetä validoiduksi.
- GitHub Actionsin julkaisu ja GHCR-oikeudet vaativat oikean tagiajon.
- Frontendin visuaalista/selaimen hyväksyntää ei tehty; HTTP-smoke ei korvaa sitä.
- npm audit: 11 high-havaintoa, jotka kulkevat braces/micromatch/globby- ja node-forge/listhen/Nitro-ketjujen kautta. Nuxt ja Vue päivitettiin korjattuihin versioihin (4.6.1 ja 3.5.44); audit fix ei poistanut jäljelle jääviä transitiivisia havaintoja. Tarkista upstream-korjaukset ennen kehityspalvelimen altistamista verkkoon. Docker-runtime sisältää vain generoituja staattisia tiedostoja, ei näitä Node-palvelinriippuvuuksia. Tämä ei ole väite koko frontendin haavoittumattomuudesta.
- Nuxt generate onnistui, mutta antoi Nitro cache-driver -resoluutioon liittyvän varoituksen. Staattinen output ja HTTP-proxy toimivat paikallisessa smoketestissä; Docker-build on vielä ajamatta.

## Ensimmäinen oikean tietokannan tarkistus

1. Aseta paikallinen .env ja käynnistä `docker compose up --build -d`.
2. Tarkista `/api/health`, aloitussivu ja kontin lokit. Health on liveness, ei DB-readiness.
3. Tarkista tietokannasta yksi parking_garages-rivi, viisi parking_lots-riviä ja jsonb-hinnaston kaksi porrasta.
4. Käynnistä app uudelleen ja varmista, että tiedot säilyvät eikä seed tuplaannu.
5. Tarkista myöhemmin oikealla PostgreSQL:llä normaali käynti ja atominen paikan käyttö/vapautus, aktiivisuuden osittaiset indeksit sekä erillisillä DbContext-yhteyksillä koordinoidut kilpailut. Nämä ovat ihmisen myöhempää ympäristönäyttöä, eivät agentin submission-portti.

## PARK-01:n paikallinen hyväksyntä 14.10.2026

Käyttäjän päivitetty rajaus hyväksyy agenttityön palveluriippumattomalla näytöllä. Se ei muuta tuotannon PostgreSQL-tietolähdettä eikä väitä pohjan avoimia ympäristötarkistuksia suoritetuiksi.

### Suoritettu

- `dotnet test MiettisPark.slnx -c Release`: 54 xUnit-testiä läpäisi, 0 skipped. Tuotannon hinnan kaikki SPEC-rajat ja virheelliset hinnastot; normaalin käynnin/paikanvaihdon testikello, nollakesto, tallennettu kuitti ja toisto; tuore DbContext/API-reload; täydelle hallille sisäänajo; tunnistamattomat paikkahavainnot; VisitId/Occupied/Unknown/404/409/normalisoinnin virheet; tunnettu DB-konflikti testituplalla ja geneerinen sisäisen virheen HTTP-vastaus.
- Sama xUnit-ajo tarkisti offline Npgsql-mallin, enumien tallennuksen, nullable VisitId:n, Restrict-FK:t, lifecycle- ja aikarajoitteet, partial unique -indeksit, seedin/jsonb/TimeSpan-roundtripin ja mutable-hinnaston seurannan. Migraation idempotentti PostgreSQL-SQL generoitiin muistissa; `HasPendingModelChanges` oli false. Ei tietokantayhteyttä.
- `npm run check`: puhdas npm ci, lint, typecheck, 7 API-stubattua DOM-testiä sekä staattinen Nuxt build läpäisivät. Testit kattavat sisäänajon, valinnaisen käyntiyhteyden, paikanvaihdon, palvelimen kuitin, itsenäisen paikkakäytön, Occupied/Unknown-estot, konfliktin refreshin, API-remount/reload-jatkamisen, täyden hallin sisäänajon, rinnakkaislähetyksen eston, asiakaspalveluohjeen sekä tyhjän/virheellisen latauksen. Ei selainajuria tai palvelinprosessia.
- Check-runnerin paikallinen erillinen CLI-probe simuloi jokaista viittä epäonnistuvaa vaihetta: ajo pysähtyi kyseiseen vaiheeseen exit 23:lla; onnistuva probe ajoi kaikki viisi vaihetta client-cwd:ssä. Probe-tiedostot poistettiin.
- `dotnet format ... whitespace` rajattiin muutettuihin backend-/testitiedostoihin; `git diff --check` ei löytänyt whitespace-virheitä.
- Backend Release build ja sisäänrakennettu offline-OpenAPI-generointi onnistuvat paikallisesti. Lopullisen revision build/test/frontend-checks tallennetaan PullPotentialin RunVerification-avaimille `backend-build`, `backend-tests`, `frontend-checks`; platform evidence on auktoritatiivinen tulos, ei tämän tekstin omistajaväite.

### Toteutuksen aikana korjatut epäonnistumiset ja varoitukset

- Ensimmäinen HTTP-testifiksuuri deserialisoi string-enumin ilman enumikonvertteria; se korjattiin testifiksuurissa, ei tuotannon JSON-sopimusta muuttamalla. Ensimmäinen offline-mallitesti luki konvention kautta valitun enumikonvertterin väärästä metadata-API:sta; testi käyttää nyt toteutunutta type mapping -konvertteria. Molemmat testit läpäisevät lopullisessa ajossa; epäonnistumisia ei ohitettu.
- Kehitystyökalun ympäristöstä tuli paikallisessa buildissa puuttuvan `Microsoft.WebTools.ApiEndpointDiscovery` hosting-startup-assemblyn lokiviesti. Buildin/OpenAPI-generoinnin exit oli 0; tämä ei ollut PostgreSQL-ajo tai tuotannon käynnistysvalidointi.
- DOM-harnessin alkuperäinen Vitest 4.0.18 toi kriittisen UI-palvelin-advisoryn ja moderate-havainnon. Uusi testiriippuvuus päivitettiin lukittuun 4.1.11-versioon (GHSA-5xrq-8626-4rwp / GHSA-82fw-gwwq-j7x9); puhdas check läpäisi. Testit käyttävät vain `vitest run`, eivät avointa Vitest UI -palvelinta. Lopullinen npm-asennus ilmoitti edelleen 11 high-havaintoa pohjan Nuxtin transitiivisissa ketjuissa, ei critical/moderate-havaintoja. Niitä ei korjattu stackin versiovaihdolla tässä itemissä. Tämä ei ole kattava turvallisuusarvio.
- Nuxt generate antoi edelleen pohjasta tunnetun Nitro cache-driver -resoluutiovaroituksen sekä npm riippuvuuksien deprecation/install-script-varoituksia. Staattinen build onnistui; ei väitettä Docker-runtimen tarkistamisesta.

### Myöhemmät ihmisen ympäristötarkistukset — ajamatta

- Oikean PostgreSQL:n normaali käynti, SQL-lifecycle/FK/partial unique -rajoitteiden runtime-toiminta, transaktion rollback ja halli-ennen-paikkaa-lukitusten todellinen kilpailukäyttäytyminen. Koordinoi erillisten yhteyksien kilpailut samalle paikalle, samalle Visitille, samalle rekisterille sekä eri paikkojen null-VisitId-havainnoille.
- Oikean PostgreSQL:n migraation soveltaminen ja restart-pysyvyys; sovelluksen restart ei tuplaa seediä tai menetä tallennettua kuittia.
- Docker-image/Compose-käynnistys, tietokantayhteys ja yhden originin SPA/API/reitin refresh.
- Oikean selaimen näppäimistö-/visuaalinen hyväksyntä. DOM-stubit eivät todista selainta, CSS-ulkoasua tai verkkoyhteyttä oikeaan backendiin. Karttapalvelua ei tarvita tämän itemin UI:ssa.
- GitHub Actions/tagijulkaisu ja GHCR-oikeudet pysyvät foundationin avoimina tarkistuksina; mitään ei julkaistu.

Nämä ajamattomat tarkistukset eivät ole submission-portteja eivätkä aiheuta clarificationia. Tuotannon transaktioita tai tietokantainvariantteja ei korvattu testituplilla. Testituplat eivät simuloi DB-rollbackia/SQL-rajoitteita, eikä niiden tuloksista väitetä PostgreSQL-semanttiikkaa. PARK-02 täydentää kahden minuutin Duplicate/Interrupted-heuristiikan ja laajan paikallisen konfliktitestimatriisin.