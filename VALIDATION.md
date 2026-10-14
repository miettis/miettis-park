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
5. Toteuta PARK-01:n yhteydessä oikean PostgreSQL:n integraatiotestit ja PARK-02:ssa kilpailutilanteet.
