# Käsin toteutettava lähtöpohja — ei agenttibacklogia

Tämä tehdään ihmisen ohjaamana ennen PullPotential-milestonea. Pohja ei kuulu BACKLOG.md:n työitemeihin.

- [x] Käynnistyvä .NET-projekti, DI ja HTTP/JSON-perusasetukset; versiot lukittu projektitiedostoihin.
- [x] Käynnistyvä Vue/Nuxt-pohja ja tyhjä näkymä. Ei vielä sovelluksen komponentteja/reittejä.
- [x] Kehityksen suhteellinen /api ja dev proxy; tuotannossa .NETin staattinen frontend ja API samassa originissa.
- [x] SPEC.md:n entityt, enumit, navigaatiot, tyypitetty TimeSpan-hintaporras, DbContext ja EF-konfiguraatio. Myös nullable VisitId, ClosedAt ja Interrupted/Unknown-tilat kuuluvat pohjaan. VisitId:n aktiivisuusindeksi koskee vain ei-null-arvoja.
- [x] PostgreSQL jsonb-/aikamappaukset, viiteavaimet, aktiivisten rivien osittaiset yksikäsitteiset indeksit ja ensimmäinen migraatio. Indeksien ehdot vastaavat enumien tallennusta.
- [x] Toistettava alustus: yksi halli, viisi paikkaa ja hinnasto. Uudelleenkäynnistys ei nollaa tai tuplaa dataa.
- [x] Yhteysmerkkijono/ympäristöasetukset ilman repoon tallennettuja salaisuuksia. TimeProvider.System rekisteröitynä tai rekisteröitävissä palveluille.
- [x] Dockerfile rakentaa frontendin ja backendin yhteen runtime-imageen. Runtime ei tarvitse Nodea; PostgreSQL on erillinen palvelu.
- [x] GitHub Actions rakentaa/julkaisee imagen semanttisista versiontageista. Sovitaan esimerkiksi v1.2.3 ja v1.2.3-rc.1; virheellinen tagi ei julkaise. Registry/nimi/tagit dokumentoidaan, salaisuudet ympäristössä.
- [ ] Paikallinen käynnistys, migraatio ja kontin tietokantayhteys kokeiltu. Mahdollinen Compose kuuluu tähän pohjaan, ei sovellusbacklogiin.
- [x] READMEen toteutuneen rakenteen start/build/lint/migraatio/konttikomennot; ei arvaamalla kirjattuja komentoja.
- [x] SPEC:n oletukset vahvistettu/muutettu: 2 minuutin raja, kapasiteetin johtaminen, CurrentStatus, poikkeavien käyntien sulkeminen sekä itsenäiset paikkahavainnot. Ulosajo ei vapauta paikkaa; täysi halli on saatavuustieto eikä sisäänajon esto.
- [ ] Pohja commitoitu ja tunniste kirjattu. Agenttityö alkaa tunnetusta toimivasta tilasta.

Pohjaan eivät kuulu REST-endpointit, hinnoittelu-/pysäköintiservicet, porttikomponentit, hallinta tai niiden testit. Tarvittava testiprojektin/harnessin perustaminen kuuluu ensimmäiseen toiminnalliseen työpakettiin.

Agentti raportoi pohjan ristiriidan täsmällisesti. Ei stackin vaihtoa, koko tietomallin uudistamista tai deploymentin uusimista. Pieni välttämätön DI-/reititys-/testattavuuskytkentä on normaalia integraatiota; uusi infrastruktuuri tai merkittävä skeemamuutos vaatii erillisen päätöksen.

Pohja toteutettu 14.10.2026. Valmiit kohdat kuvaavat lähdekoodia ja paikallista validointia; Docker-/PostgreSQL-ajo sekä GitHub-julkaisu ovat edelleen avoimia. Tarkat tulokset: [VALIDATION.md](VALIDATION.md). Commitia tai tagia ei tehty.
