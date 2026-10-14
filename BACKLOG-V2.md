# V2-backlog – hallien pohjat ja karttanäkymä

## Agenttityön hyväksyntä ilman ulkoisia palveluja — 14.10.2026
Tehtävien toteutus ja valmistuminen eivät edellytä käynnissä olevaa PostgreSQL:ää, Docker-daemonia, erillistä testikantaa/yhteysmerkkijonoa, selainajuria, karttapalvelua tai ihmisen ennakkoon tekemää hyväksyntää. Aloita ja jatka toteutusta niiden puuttumisesta huolimatta; älä pyydä clarificationia tai jätä koko tehtävää tekemättä tästä syystä. .NET/Node ja projektin tavallinen riippuvuuksien restore/asennus ovat edelleen buildin edellytyksiä.
Tuotantosovellus käyttää edelleen EF Corea/PostgreSQL:ää; älä korvaa sitä muistivarastolla tai vaihda stackia. Pakollinen näyttö: backend-build ja aidot palvelu-/API-testit testikellolla sekä paikallisilla testituplilla tai testiin rajatulla embedded-providerilla, frontend lint/typecheck/build ja mahdolliset DOM-komponenttitestit sekä offline OpenAPI/migraatio-SQL/mallin tarkistus. Testit eivät saa tarvita palvelinprosessia, verkkopalvelua, konttia tai salaisia asetuksia. Kartan ulkoinen riippuvuus korvataan testeissä stubilla.
Oikean PostgreSQL:n lukitus-/kilpailu-/migraatioajot, Docker-käynnistys, oikea selain/karttapalvelu ja restart-pysyvyys ovat erillisiä myöhempiä ihmisen tarkistuksia, eivät tämän agenttityön submission-/checklist-portteja. Dokumentoi ne ajamattomiksi. Älä väitä InMemory/SQLite-testin todistavan PostgreSQL-semanttiikkaa, merkitse skipped-integraatiotestiä läpäistyksi tai heikennä tuotannon invariantteja. Paikallinen hyväksyntä ja vielä vahvistamaton ympäristökäyttäytyminen erotetaan.
Jos hyväksytyssä designissa/pysyvässä checklistissä on vanha ulkoisen ympäristön vaatimus, tee tämän käyttäjän hyväksymän rajauksen mukainen design amendment ja päivitä checklist vaatimuksen sisältöä vastaavaksi. Älä kuittaa tekemätöntä ulkoista tarkistusta tehdyksi. Työ voi valmistua paikallisella näytöllä ja dokumentoidulla myöhemmän validoinnin listalla.


14.10.2026. Sopimus: [SPEC-V2.md](SPEC-V2.md), pohjalla [SPEC.md](SPEC.md). Nämä ovat repo-dokumentteja; työitemeitä tai milestonea ei ole tällä toimeksiannolla luotu PullPotentialiin.

## Ajotapa ja lähtöehto

V2 alkaa hyväksytystä ja integroidusta MVP:stä, ei rinnakkaisena keskeneräisen MVP:n kanssa. Tarkista alussa nykyinen koodi, testiharness ja avoimet validointirajat. Ei pohjan uusimista tai erillisiä DB/API/UI-itemeitä.

Yksi work-folder **v2**, milestone **v2**: PARK-V2-01 → PARK-V2-02 → PARK-V2-03. Ensimmäiset kaksi ovat kokonaisia toiminnallisia paketteja backendistä UI:hin testeineen. Viimeinen on rajattu hyväksyntä, ei koko testikehikon tai uuden arkkitehtuurin rakentaminen.

Sama Lead kantaa kokonaisvastuun, yksi Developer auttaa rajatuissa UI-/testi-/bulk-osuuksissa samassa item-worktreessä. Lead ratkaisee identiteetit, migraation, transaktiot ja API-sopimuksen. Ei pakotettua delegointia tai review-kierroksia. PM voi havainnoida poikkeamia; ei automaattista budjettien tai hyväksynnän ohitusta.

Lead julkaisee lyhyen designin ja ei-tyhjän pysyvän checklistin. Vain Lead tekee authoritative completion/submission-claimit. Nykyisen revision backend-build/backend-tests/frontend-checks-näyttö säilyy; tarkista todelliset komennot MVP:n lopputuloksesta. OpenAPI päivitetään ennen evidencerevision jäädyttämistä, eikä snapshot-verification saa muuttaa tracked sourcea.

Siirrä SPEC-V2 ja hyväksytyn MVP:n lyhyt tekninen luovutus knowledgeen backlogia tuotaessa. Itemien väliseen luovutukseen DTO:t, invarianssit, migraatio ja testikomennot; ei koko keskustelun kopiointia. Agentti toteuttaa vain käsiteltävän itemin.

## PARK-V2-01 – Hallien ylläpito ja tallennettava ruudukkopohja

**Task; P0; suurin paketti; edellytys hyväksytty MVP.**

Toteuta hallin luonti/perustietojen muokkaus sekä yksikerroksinen grid-editori /admin-osioon. Nimi, osoite ja kartalta valittava piste; osoitehakua ei tarvita. Maalaustyökalut ajorata/parkkipaikka/pyyhekumi, mitat 1–50, erillinen tallennus/peruutus, automaattinen numerointi sekä numeron muutos ja olemassa olevan paikan siirtäminen.

Sisällytä tarvittava EF-migraatio, API/service/DTO:t, Vue-komponentit ja testit. Suunnittele karttakomponentti niin, että seuraava item käyttää samaa karttatoimittajaa. Ei irrallista karttapalvelu-frameworkia, kirjastojen vertailuprojektia tai infraremonttia.

### Hyväksyntä/checklist

- [ ] Olemassa olevien hallien/paikkojen ID:t, käyttötilat, historia ja hinnasto säilyvät migraatiossa; olemassa olevat paikat saavat deterministisen pohjan.
- [ ] Uusi halli saa oletushinnaston ja tyhjän pohjan; osoite/piste validoidaan palvelimella. Vanha halli voi säilyttää puuttuvan sijainnin ilman keksittyä dataa.
- [ ] Ylläpitosivut ovat /admin-osiossa ilman authia/rooleja; nykyinen lukeva ylläpito säilyy tai yhdistyy hallitusti.
- [ ] Maalaaminen vetämällä ja yksittäinen klikkaus toimivat. Ei alueen täyttöä; yksi ruutu = yksi paikka.
- [ ] Tallentamaton luonnos ei muuta backendia; peruuta, poistumisvaroitus ja virhepalautteen jälkeinen luonnoksen säilyminen toimivat.
- [ ] Uudet numerot ovat deterministisiä ja hallikohtaisesti uniikkeja; ID säilyy paikan siirrossa/numeroa muutettaessa.
- [ ] Pohjan atominen tallennus validoi mitat, ruututyypit, koordinaatit, ID:t ja hallirajat. Palauttaa luotujen paikkojen oikeat ID:t.
- [ ] Käytössä/Unknown/aktiivisen Parkingin paikkaa ei poisteta tai maalata yli. Pienennys ei leikkaa paikkoja hiljaa.
- [ ] Vapaan paikan poistuminen kapasiteetista säilyttää historian ja viiteavaimet; aktiivisten numeroiden uniikki-indeksi vastaa uutta mallia.
- [ ] Paikalliset palvelutestit kattavat stale-version-, rollback- ja poisto-vs-käyttöönotto-konfliktipolut; offline-malli/SQL tarkistetaan. Oikean PostgreSQL:n kilpailut jäävät myöhempään ihmisen tarkistukseen.
- [ ] Offline-migraatio-/mallitarkistus ja paikalliset historiatestit sisältävät MVP:n historiallisen ja aktiivisen pysäköinnin; oikea migraatioajo myöhemmin.
- [ ] Karttatoimittaja, attribuutio, verkkoriippuvuus ja mahdollinen avain dokumentoitu. Ei oikeiden salaisuuksien versionhallintaa.
- [ ] Fokusoidut UI-/service-testit, backend/frontend-tarkistukset ja offline OpenAPI ajettu. Luovutus seuraavalle: layout DTO/version, paikkojen aktiivisuusehto ja karttakomponentin käyttö.

**Delegointi:** Lead omistaa migraation ja transaktion; Developer toteuttaa sovitun grid-editorin tai rajatut testi-/UI-osat vahvistetulla DTO-sopimuksella.

## PARK-V2-02 – Kartan käyttötilanne ja hallikohtainen simulaationäkymä

**Task; P0; keskisuuri; riippuvuus PARK-V2-01.**

Toteuta /-sivulle hallien kartta ja listaus. Karttamerkissä nimi/osoite ja esimerkiksi 3/5 varattu, Unknown erikseen. Merkistä/listasta suoraan /garages/{id}-näkymään. Näytä tallennettu ruudukko värikoodattuna ja kytke siihen MVP:n simulaatiotoiminnot ja kuitti. Kartta/editori käyttävät samaa sovittua karttakomponenttia.

### Hyväksyntä/checklist

- [ ] Yhteenvedon capacity/occupied/free/unknown perustuvat aktiivisiin ParkingLot-tiloihin, eivät Visitien määrään; poistuneet paikat eivät kuulu lukuihin.
- [ ] 3/5 varattu ja Unknown-lisämerkintä sekä 0/0 varattu ovat yhdenmukaiset kartalla, listalla ja paikkanäkymässä.
- [ ] Karttamerkin klikkaus/näppäimistöaktivointi avaa suoraan oikean hallin; ei pakollista infolaatikon välivaihetta.
- [ ] Puuttuva sijainti näkyy listalla; kartan latausvirhe ei riko listaa tai hallin simulaatiota. Ei virheellistä (0,0)-oletusmerkkiä.
- [ ] Paikat näkyvät vihreinä/punaisina/harmaina numeron ja tilatekstin/kuvakkeen kanssa; ajorata ja tyhjät ruudut erottuvat.
- [ ] Paikkahavainnot toimivat auton yhdistämisellä ja ilman; Visitin päättyminen ei vapauta paikkaa. Reload ja Päivitä hakevat oikean tilanteen API:sta.
- [ ] Hallikohtainen sisäänajo, paikan valinta/vapautus/vaihto, ulosajo ja kuitti toimivat kahdessa hallissa; ristiin viittaavat ID:t hylätään.
- [ ] Täysi halli on edelleen saatavuustieto, ei sisäänajon esto. Hallikohtainen laskutus käyttää hallin hinnastoa.
- [ ] Käyttötilan konfliktin jälkeen tilanne päivittyy; SSE välittää myös muista clienteistä tulevat muutokset.
- [ ] Kartan attribuutio, responsiivinen listavaihtoehto, kenttälabelit ja näppäimistökäyttö huomioitu.
- [ ] Normaalipolun ja virhetilojen testit sekä nykyisen revision build/test/lint/typecheck/OpenAPI-näyttö. Luovutus: reitit, päivityslogiikka ja tunnetut ympäristörajat.

**Delegointi:** Lead kytkee hallikohtaisen simulaation ja tilakyselyt; Developer toteuttaa sovittua kartta/lista/pohjakuva-UI:ta tai komponenttitestejä.

### PARK-V2-02:n reaaliaikaisuuden hyväksyntä

Reaaliaikainen päivitys kuuluu tähän samaan toiminnalliseen pakettiin, ei erilliseen infrastruktuuri-itemiin. Noudata SPEC-V2:n SSE-sopimusta.

- [ ] GET /api/garages/events tarjoaa yhden jaetun SSE-yhteyden; commitin jälkeinen garage-changed invalidointi päivittää karttamerkin, listan ja avoimen hallin paikkatilat.
- [ ] Pohjan/paikkamäärän ja hallin perustietojen muutokset päivittyvät; Visit-muutos ei muuta paikkalaskuria.
- [ ] Duplikaatit ja vanhentuneet HTTP-vastaukset eivät vääristä tilaa. Aktiivinen lomake, valinta ja tallentamaton editoriluonnos säilyvät.
- [ ] Yhteystila näkyy, katko säilyttää viimeisen datan, reconnect hakee kokonaisen tilannekuvan eikä edellytä tapahtumahistorian replayta. Alkuhauissa ei hukata invalidointeja. Päivitä on varavaihtoehto.
- [ ] Rollback ei julkaise tapahtumaa. Peruutus/tilausten siivous ja rajattu puskuri estävät resurssivuodot; ei henkilötietoja streamiin.
- [ ] Paikalliset publisher/API/client-testit ajettu ilman PostgreSQL/Docker/selainpalvelua. Proxy-/kahden selaimen näyttö dokumentoitu myöhemmäksi.

## PARK-V2-03 – Rajattu v2-hyväksyntä ja luovutus

**Task; P0; pieni; riippuvuus PARK-V2-02.**

Aja kertynyt testistö ja hyväksy kokonainen polku: luo halli → valitse osoite/sijainti → maalaa pohja → tallenna → avaa kartalta → simuloi käyttöä → tarkista luvut/värit/kuitti → reload/restart. Korjaa rajatut regressiot; ei yleistä hardening-, suorituskyky- tai dependency-upgrade-urakkaa.

- [ ] Dokumentoi myöhemmän ihmisen tarkistukseksi (ei agenttityön portti): Selaimessa koko polku kahdella hallilla sekä editorin peruutus, siirto/poisto, Unknown, tyhjä pohja ja karttavirheen listavaihtoehto.
- [ ] Olemassa olevat MVP:n hinnoittelu-/Duplicate-/Interrupted-/paikkariippumattomuus-testit läpäisevät.
- [ ] Paikalliset migraatio-SQL/malli-, concurrency-virhepolku- ja historiatestit ajettu. Oikean PostgreSQL:n migraatio/kilpailuajot dokumentoitu myöhemmäksi, ei väitetty suoritetuiksi.
- [ ] Dokumentoi myöhemmän ihmisen tarkistukseksi (ei agenttityön portti): Yhden Docker-imagen frontend/API, suorat /admin/garages/{id}/layout- ja /garages/{id}-reitit sekä refresh toimivat erillisen PostgreSQL:n kanssa. Tallennettu pohja/sijainti/käyttötila säilyy restartissa.
- [ ] Kartan mahdollinen ulkoinen avain/config/attribuutio ja virhetilanne dokumentoitu ilman salaisuuksia.
- [ ] README, AI-USAGE ja VALIDATION päivitetty toteutuneen työn ja todellisten tarkistusten perusteella. MVP- ja v2-dokumenttien reitti-/scope-muutokset selitetty.
- [ ] Nykyisen revision pakolliset verification-avaimet läpäisevät ja checklist on valmis; työ integroitu milestone-haaraan.
- [ ] Ihmisen lopullinen hyväksyntä, milestone-release, semanttinen tagi ja image-julkaisu erotettu agentin toteutuksen valmistumisesta. Ei julkaisua ilman toimeksiantoa.

Puuttuva Docker/PostgreSQL/selain/karttapalvelu kirjataan myöhemmän ihmisen tarkistukseksi; se ei edellytä clarificationia eikä estä agenttityön valmistumista. Agentti ei kuluta budjettia rajattomaan asentamiseen tai identtisiin retryihin.

V2:n myöhempään ihmisen ympäristöhyväksyntään kuuluu kahden selaimen SSE-päivitys ja yhteyskatkosta palautuminen tuotantoproxyn läpi. Se ei estä agenttityön paikallista hyväksyntää.
