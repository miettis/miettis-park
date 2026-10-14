# V3-backlog – autokuva ja valittava simulaatioaika

14.10.2026. Vaatimukset: [SPEC-V3.md](SPEC-V3.md). Nämä ovat Markdown-työpaketteja; PullPotentialin work itemeitä tai milestonea ei luoda tällä toimeksiannolla.

## Ajotapa

Aloita nykyisen hyväksytyn/integratoidun v2:n päälle. Lue oikeat service/API/DTO/testirajapinnat, älä rakenna foundationia tai v2:ta uudelleen. Yksi folder v3 ja milestone v3: **PARK-V3-01 → PARK-V3-02 → PARK-V3-03**.

Sama Lead vastaa kokonaisista toiminnallisista paketeista. Developer voi auttaa rajatuissa UI-/adapteri-/testiosissa samassa item-worktreessä; ei erillisiä backend/frontend/DB-itemeitä tai pakotettuja contributor-kierroksia. Lead omistaa domainin aikajärjestyksen, hinnoittelun, transaktiot ja automaattisen sisäänajon orkestroinnin.

Design ja pysyvä checklist per item; testit kertyvät toteutuksen mukana. Nykyisen revision verification keys backend-build/backend-tests/frontend-checks; käytä ajettavan repon todellisia komentoja, OpenAPI päivitetään ennen evidencerevision jäädyttämistä.

Oikea OpenRouter-avain/malli, PostgreSQL, Docker, selain tai karttapalvelu **eivät ole toteutuksen tai submissionin edellytys**. Pakolliset testit käyttävät testituplia/stubattua HTTP:tä ja paikallista testikelloa. Testiharness ei saa kutsua maksullista palvelua; puuttuva palvelukonfiguraatio ei estä startupia/offline OpenAPI:a. Älä pyydä clarificationia pelkästä ympäristöpuutteesta; kirjaa myöhemmät ihmisen runtime-tarkistukset. Tuotanto käyttää edelleen oikeaa PostgreSQL:ää ja toteutettua OpenRouter-adapteria.

Liitä SPEC-V3 knowledgeen tuotaessa. Tarkista v2:n nykyinen luovutus/testikomennot. Älä kopioi vanhaa foundation-tilaa oletukseksi. Agentin loppuviesti ei korvaa authoritative submissionia/milestone-integraatiota. Ei releasea, tagia tai image-pushia ilman toimeksiantoa.

## PARK-V3-01 – Tapahtumakohtainen simulaatioaika päästä päähän

**Task; P0; keskisuuri; edellytys v2.**

Toteuta occurredAt kaikkien neljän simulaatiokomennon DTO/service/API/UI-polkuun, kuittiin sekä Duplicate/recovery-laskentaan. Lisää Simulation:Enabled ja yhteensopiva oletus palvelimen TimeProvideriin. UI:ssa tapahtuma-aika, näkyvä aikavyöhyke ja Nykyhetki/+10 min/+1 h/+3 h. Ei globaalisti muutettavaa simulaatiokelloa tai historian replayta.

### Hyväksyntä/checklist

- [ ] Puuttuva aika käyttää kerran ratkaistua palvelinaikaa; eksplisiittinen offset normalisoidaan UTC:ksi. Nykyiset kutsut, myös paikan exit ilman bodya, toimivat.
- [ ] Aika koskee sisäänajoa, paikalle ajoa, paikalta poistumista ja ulosajoa; tekniset audit-/timeout-/SSE-ajat pysyvät oikeassa kellossa.
- [ ] Simulation:Enabled=false hylkää annetun ajan selkeästi, mutta normaali palvelinaikainen polku toimii. Demossa asetus true, dokumentoitu.
- [ ] Menneet/tulevat ajat sallitaan, mutta saman auton/hallin/paikan aikajärjestyksen rikkominen palauttaa EVENT_TIME_CONFLICT ilman osittaista tallennusta.
- [ ] Saman ajan peräkkäiset tapahtumat ja nollakesto toimivat. Eri autojen tai eri paikkojen ajat eivät aseta hallinlaajuista rajaa; rinnakkaiset pyynnöt eivät jaa muutettavaa kelloa.
- [ ] Optional VisitId:n nykytilan/hallin/alkuajan validointi sekä Visit/Parking-riippumattomuus säilyvät.
- [ ] Hinnoittelu/kuitti 0/10 min/3 h/3 h +1 s/4 h sekä Duplicate-raja testataan tuotantopalvelun kautta testikellolla ilman oikeaa odottamista.
- [ ] UI:n aika/pikatoiminnot, onnistumis-/kenttä-/aikavirheet ja SSE-päivityksen aikainen syötteen säilyminen katettu paikallisilla testeillä.
- [ ] Backend/frontend-testit ja build/lint/typecheck/OpenAPI läpäisevät. Luovutus seuraavalle: DTO/service-aikaparametri, konfliktit ja aiemman käyntihistorian tarkistuksen paikka.

**Delegointi:** Lead toteuttaa aikajärjestyksen ja hinnan palvelusopimuksen; Developer voi tehdä ajansyöttökomponentin tai rajatapauksien testimatriisin.

## PARK-V3-02 – Sisäänajo suoraan autokuvasta

**Task; P0; keskisuuri/suurin; riippuvuus PARK-V3-01.**

Toteuta ILicensePlateReader, yksi OpenRouter-kuvamalliadapteri ja multipart-sisäänajoendpoint sekä käyttöliittymän kuvan tiedostovalinta, raahaus lähetysalueelle ja lähetys/esikatselu. Käytä käyttäjän ennen lähetystä valitsemaa tapahtuma-aikaa. Kun löytyy yksi uniikki kelvollinen rekisteri, kutsu nykyistä sisäänajopalvelua suoraan ilman manuaalista vahvistusta.

Ei yleistä AI-chatia, kahta provideria, videota, rekisteriehdotuksen hyväksymismodaalia tai kilven rajauseditoria. Epäselvä/usean rekisterin tulos ei avaa porttia tai kirjaa Visitiä; käyttäjä voi lähettää uuden kuvan tai käyttää käsinpolkua.

### Hyväksyntä/checklist

- [ ] Valittu provider OpenRouter, eksplisiittinen konfiguroitava kuvamalli/avain backendissä; oikea adapteri on toteutettu, ei runtime-fake. Salaisuuksia ei vuoda clientiin/lokeihin.
- [ ] Puuttuva konfiguraatio ei kaada sovellusta tai offline OpenAPI:a; kuvatoiminto palauttaa hallitun tilan ja käsinsyöttö toimii.
- [ ] JPEG/PNG, todellisen kuvaformaatin tarkistus, 8 MiB -raja, rajatut resurssit ja ulkoiselle palvelulle lähettämisen UI-teksti. Kuvaa ei tallenneta pysyvästi.
- [ ] Rakenteinen provider-vastaus validoidaan/normalisoidaan ja identtiset ehdokkaat yhdistetään. Ei merkkien arvaamista, tekaistua confidence-rajaa tai kuvassa olevien ohjeiden suorittamista.
- [ ] Yksi tunnistettu rekisteri → automaattinen sisäänajo saman pyynnön aikana; palauttaa rekisterin, ajan, VisitId:n ja Entry/Duplicate/RecoveredMissingExit-tuloksen ilman hyväksyntävaihetta.
- [ ] EffectiveOccurredAt ratkaistaan ennen provider-kutsua. Tunnistuksen viive ei muuta saapumisaikaa. Hallin/ajan syöte validoidaan etukäteen ja nykytilan/aikajärjestyksen ehdot uudelleen transaktiossa tunnistuksen jälkeen.
- [ ] Ulkoista kutsua ei tehdä avoimen DB-transaktion/lukon sisällä. Rinnakkaiset kuva-/käsinpyynnöt säilyttävät yhden Active Visitin invariantin.
- [ ] Tyhjä/epäselvä/usean eri rekisterin tulos, rikkinäinen JSON, invalid image/size ja providerin timeout/402/429/5xx: hallittu virhe, ei Visitiä/Parkingia, ei automaattista maksullista retryä.
- [ ] UI:ssa kuvan valinta tiedostovalitsimella tai raahaamalla lähetysalueelle, yhteinen tiedostovalidointi, raahauksen kohdistumispalaute, näppäimistöllä toimiva tiedostovalinta, esikatselu, valittu aika, käsittelytila, rinnakkaislähetyksen esto, onnistumistulos ja selkeä uuden kuvan/käsinsyötön varavaihtoehto. SSE ei hävitä syötettä/esikatselua.
- [ ] Tiedostovalinta ja drag-and-drop käyttävät samaa polkua; kumpikaan ei lähetä kuvaa ennen Lue kilpi ja aja sisään -painiketta. Molemmat tavat sekä virheellinen pudotettu tiedosto katettu paikallisilla DOM-testeillä.
- [ ] API/service/adapteri-/DOM-testit stubatulla HTTP:llä todistavat automaattisen sisäänajon, ajan, duplikaatit ja kaikki keskeiset virhepolut ilman ulkoista palvelua/avainta.
- [ ] Build/test/lint/typecheck/offline OpenAPI läpäisevät. Luovutus: provider-konfiguraatio, tulos-/virhekoodit, kuvankäsittelyrajat sekä oikeilla kuvilla vielä tarkistettava tunnistustarkkuus.

**Delegointi:** Lead omistaa providerin jälkeisen atomisen sisäänajon; Developer voi tehdä rajatun HttpClient-adapterin, kuvalomakkeen tai stub-testit sovitulla sopimuksella.

## PARK-V3-03 – Paikallinen loppuhyväksyntä ja luovutus

**Task; P0; pieni; riippuvuus PARK-V3-02.**

Aja kertynyt palveluriippumaton testistö ja todenna kokonaisuus paikallisesti: kuvavastaus testituplasta → suora sisäänajo valitulla ajalla → erillinen paikkahavainto → paikan vapautus → ulosajo myöhemmällä ajalla → oikea kuitti. Korjaa rajatut regressiot. Ei uutta testikehikkoa tai avointa hardening-urakkaa.

- [ ] Kokonainen polku katettu aidon domain-palvelun ja testiin rajatun tunnistusadapterin kautta; ei pelkkä käsin laskettu mock-kuitti.
- [ ] Hinnoittelurajat, Duplicate/recovery, väärä aikajärjestys ja kuvavirheet läpäisevät; v2:n SSE/kartta/pohja ja vanhat käsinkutsut eivät regressioidu.
- [ ] Default test run toimii ilman provider-avainta, PostgreSQL:ää, Dockeria tai selainta. Oikean providerin testit ovat erikseen opt-in, eivät hiljaisesti läpäistyjä tai oletusajon ehtoja.
- [ ] README/AI-USAGE/VALIDATION kuvaavat todellisen toteutuksen, simulaatioajan rajoituksen, provider-asetukset, kuvan lähettämisen/säilytyksen sekä ajamattoman tunnistustarkkuus-/runtime-näytön.
- [ ] Ihmisen myöhemmäksi tarkistuslistaksi kirjattu oikean mallin 15–20 autokuvan kokeilu (selkeä, vino, heikko valo, ei kilpeä, useita autoja), automaattisen sisäänajon oikeellisuus, viive/kustannus sekä selain-/kontti-/PostgreSQL-ajo. Ei maksullista kokeilua ilman avainta ja erillistä toimeksiantoa.
- [ ] Nykyisen revision verification/checklist/submission valmiit ja itemit integroituvat milestone-haaraan. Ihmisen hyväksyntä/release/tagi/image-julkaisu erotettu tästä.

Puuttuva ulkoinen testaus dokumentoidaan myöhemmäksi, eikä se estä agenttityön paikallista valmistumista. Mallin tunnistustarkkuudesta tai fyysisen portin turvallisuudesta ei anneta lupausta testituplan perusteella.
