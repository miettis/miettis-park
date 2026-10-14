# PullPotential-backlog

14.10.2026. Työpaketit ovat valmistelua tuontiin, eivät vielä PullPotentialin tietokantaan luotuja tehtäviä. [STARTING-POINT.md](STARTING-POINT.md) on ajoa edeltävä ehto, ei agenttityöitemi. Vaatimukset: [SPEC.md](SPEC.md).

## Projektin ajotapa

Työ jaetaan kokonaisiin toiminnallisiin osuuksiin. Sama Lead vastaa tuotantopolusta ja rajatut Developer-tehtävät täydentävät sitä. Testit ja hyväksyntänäyttö kertyvät toteutuksen mukana.

- Toimiva pohja käsin. Ei erillisiä entity-, repository-, API-, UI-, Docker- tai CI-itemeitä.
- Kolme pakollista kokonaisuutta; ensimmäinen koko normaali polku, toinen poikkeamat, kolmas rajattu hyväksyntä. Jokainen toiminnallinen item sisältää backendin, frontendin ja testit.
- Sama Lead kokonaisvastuussa ja aluksi yksi Developer. Ei pakotettuja contributor-kierroksia tai konsensusta.
- Lead/Developer jakavat yhden item-branchin/worktreen; delegointi ei luo erillistä backlogitemiä/haaraa. PullPotential integroi itemit milestone-haaraan. Eri itemien sessiot eivät automaattisesti jatku samana keskusteluna: rajapintasopimus ja lyhyt luovutus kirjataan hyväksyttyyn designiin/yhteenvetoon.
- Lead päättää invariantit, hintakaavan ja transaktiot. Developer kerää rajattua aineistoa, toteuttaa sovittuja UI-/service-osia ja testejä. Delegoinnissa tavoite, tiedostorajat, tulos ja verifiointi; yksi kirjoittaja kerrallaan. Lead tarkistaa lopputuloksen.
- Leadille vahva koodausmalli, reasoning medium; Developerille edullisempi toimiva koodausmalli, low/medium. Konkreettiset nimet valitaan käytettävissä olevista malleista, ei lukita vanhenevaa nimeä tai hintaväitettä tähän dokumenttiin.
- Riippumattomat design/implementation review -agentit oletuksena pois. Design → implement → tarvittaessa repair, automaattiset tarkistukset ja ihmisen hyväksyntä. Review on itemikohtainen poikkeus.
- PM valinnainen supervisor poikkeamiin, ei pakollinen lisähyväksyntä. Aloita observe-only, jos käytössä. Ei budjettien tai vaatimusten ohitusta.
- Lead julkaisee lyhyen designin ja ei-tyhjän pysyvän checklistin. Käyttää vaihekohtaisia checklist/evidence/submission-työkaluja; lukee checklistin kaikki sivut. Pelkkä loppuviesti ei päätä työtä.
- Testit kertyvät itemeissä. Lopputehtävä ei aloita koko testikehikkoa eikä rajatonta hardening-urakkaa.
- PARK-01 saa suurimman suhteellisen budjetin. Raja ei nouse automaattisesti saman virheen toistuessa. Hidas aktiivinen build/delegointi ei yksin tarkoita jumia. Aika, Lead/Developer-käyttö ja ihmisen korjaukset kirjataan erikseen.

## Organisointi

Yksi milestone **M1 — Toimiva pysäköintihallisimulaatio**: PARK-01 → PARK-02 → PARK-03. Ei keinotekoisia Epic/Feature-tasoja; yksi työfolderi `Pysäköintisimulaatio` riittää. Folderi ryhmittelee, ei suorita tai hyväksy työtä.

Valinnainen PARK-04 jää M1:n ulkopuolelle omaan myöhempään milestoneen, jotta se ei estä pakollisen lopputuloksen valmistumista.

SPEC ja toteutunut lähtöpohjan tekninen sopimus liitetään project knowledgeen. Itemien kuvauksiin viedään alla olevat itsenäiset tavoitteet/rajaukset/hyväksynnät. Agenttiohje viittaa annettuun työitemiin, knowledgeen ja hyväksyttyyn designiin; ei ohjeeseen toteuttaa koko BACKLOG.md kerralla. Roolioverridet jätetään tyhjiksi ilman erityistä syytä.

## PARK-01 — Normaali pysäköintikäynti päästä päähän

**P0; keskisuuri/suurin paketti; Task; edellytys käsin tarkistettu pohja.**

Tee REST API, servicet ja porttisimulaation Vue/Nuxt-komponentit/reitti: rekisterin syöttö → saatavuus → vapaan paikan valinta → paikalta poistuminen/vaihto → ulosajo ja tallennettu kuitti. Paikan käyttö on itsenäinen toiminto: VisitId on valinnainen, ja paikan voi ottaa käyttöön ilman rekisteriä tai hallikäyntiä. Hinnoittelu käyttää TimeProvideria ja tarkkoja alkavia yksiköitä. Invariantit ja tietokantatransaktiot ovat mukana alusta alkaen, eivät myöhemmin lisättävä muistilukko.

Tee tarvittava testiprojekti/harness ja fokusoidut testit. Virhevastauksen muoto sovitaan nyt; poikkeavien käyntien täydellinen heuristiikka tulee PARK-02:ssa. Ei adminia, kirjautumista, asiakaspalveluohitusta tai lähtöpohjan entity-/deployment-työtä.

### Hyväksyntä/checklist

- [ ] Normaalin polun SPEC-endpointit tallentavat EF/PostgreSQL:ään; ei rinnakkaista muistissa olevaa tietolähdettä.
- [ ] Sisäänajo, vapaan paikan valinta, vapautus/vaihto ja ulosajo toimivat samassa UI:ssa. Lataus/virhepalaute ja labelit mukana.
- [ ] Normalisointi ja tyhjän/pitkän rekisterin palaute ovat yhdenmukaiset.
- [ ] Hintataulukon rajat, nollakesto ja virheellinen hinnasto testattu oikeaa tuotantopalvelua ja testikelloa vasten.
- [ ] Saatavuus perustuu paikkojen tiloihin; täysi halli näytetään mutta ei estä sisäänajohavainnon kirjaamista. Occupied/Unknown-paikka ei ole valittavissa.
- [ ] Paikan käyttö ja vapautus onnistuvat ilman VisitId:tä/rekisteriä. Valinnainen käyntiyhteys validoidaan, ja useita tunnistamattomia pysäköintejä voi olla eri paikoilla.
- [ ] Ulosajo tallentaa Costin ja ajat kerran; päättynyt kuitti ei muutu. Ulosajo ei muuta paikkojen tilaa tai sulje Parkingia.
- [ ] Reloadin jälkeen aktiivista käyntiä voi jatkaa API:n tiedon avulla.
- [ ] PostgreSQL-integraatiotesti normaalista käynnistä ja ainakin yhdestä aktiivisen paikan yksikäsitteisyysrajoitteesta. InMemory ei korvaa tätä näyttöä.
- [ ] Backend-build/test ja frontend-build/lint suoritettu. Suorittamatta jäänyt tarkistus näkyy erikseen.
- [ ] Lyhyt luovutus: reitit, servicerajapinnat, lukituksen/invarianttien paikka, testikomennot ja PARK-02:n jäljellä oleva rajaus.

**Delegointi:** Lead sopii API/transaktion ja hintakaavan; Developer toteuttaa sovitut komponentit tai hintataulukon testit. Lead kokoaa ja kokeilee polun.

## PARK-02 — Puuttuvat havainnot, kilpailut ja virhepolut

**P0; keskisuuri; Task; riippuvuus PARK-01.**

Täydennä samaa toteutusta 2 minuutin konfiguroitavalla Duplicate-heuristiikalla, Interrupted-käyntien sulkemisilla, toisistaan riippumattomilla käynti-/paikkahavainnoilla ja asiakaspalveluvirheellä. Ei API-/UI-arkkitehtuurin uudistamista, request-id-palvelua, maksamista tai asiakaspalvelu-UI:ta.

PostgreSQL-kilpailutestit käyttävät erillisiä DbContext-yhteyksiä ja koordinoidusti yhtä aikaa alkavia operaatioita, eivät kahta peräkkäistä pyyntöä. Tarkista lopulliset rivit ja häviäjän hallittu vastaus.

### Hyväksyntä/checklist

- [ ] Ikä <= 2 min palauttaa saman Visitin muuttamatta StartAt-aikaa; täsmälleen raja testattu.
- [ ] Ikä > 2 min sulkee vanhan ja avaa uuden atomisesti; vanha on Interrupted ilman keksittyä EndAt/Costia.
- [ ] Käynnin keskeytys tai ulosajo ei muuta siihen mahdollisesti yhdistettyä Parkingia/paikkaa. LeaveParkingLot vapauttaa paikan riippumatta siitä, tunnetaanko auto tai onko liitetty Visit jo suljettu.
- [ ] Puuttuva sisäänajo → ACTIVE_VISIT_NOT_FOUND, asiakaspalvelulippu ja gateOpened=false; UI näyttää ohjeen.
- [ ] Ulosajon toisto ei muuta aiempaa kuittia tai veloita uudelleen.
- [ ] Toisen hallin paikka, suljettu Visit, Occupied/Unknown ja käynnin toinen aktiivinen paikka hylätään hallitusti.
- [ ] Kaksi kilpailijaa samalle paikalle: yksi onnistuu, yksi Active Parking, oikea paikkatila.
- [ ] Saman rekisterin kilpailevat sisäänajot eivät luo kahta Active Visitiä. Eri rekisterien havainnot voidaan kirjata myös täydelle hallille.
- [ ] Eri paikkojen tunnistamattomat pysäköinnit onnistuvat rinnakkain; nullable VisitId ei aiheuta virheellistä yksikäsitteisyyskonfliktia.
- [ ] UI päivittää tilanteen myös konfliktista ja erottaa Duplicate-, Interrupted- ja virhetulokset.
- [ ] Ei SQL-/stack trace -vuotoa tai tarpeetonta rekisteritietojen lokitusta.
- [ ] Testit ajettu; heuristiikan väärinkäyttö-/korjausrajoitus ja mahdollisesti puuttuva PostgreSQL-näyttö dokumentoitu.

**Delegointi:** Developer tekee sovittuja kilpailutestejä ja virhepalautekomponentteja; Lead tarkistaa transaktiot ja näyttöketjun. Vaatimuksia ei heikennetä testien vihreäksi saamiseksi.

## PARK-03 — Rajattu loppuhyväksyntä ja luovutus

**P0; pieni; Task; riippuvuus PARK-02.**

Aja kertynyt testistö, kokeile pakollinen polku selaimessa ja olemassa olevassa kontissa, viimeistele README/AI-USAGE. Ei uuden koko testiston rakentamista tai avointa korjausurakkaa. Korjaa rajatut hyväksyntäpolun regressiot; laajuusmuutos on erillinen päätös.

- [ ] Kertyneet testit, build ja lint ajettu toistettavilla dokumentoiduilla komennoilla.
- [ ] Selaimessa normaali käynti/kuitti, täysi halli, varattu paikka, Duplicate, puuttuva sisäänajo ja reload kokeiltu. Automatisoi tärkein polku, jos sopiva harness on jo käytettävissä; muu näyttö kirjataan manuaaliseksi.
- [ ] 3 tunnin hintaraja todennettu testikellolla. Ei julkista tuotanto-API:a mielivaltaisille laskutusajoille.
- [ ] Olemassa olevasta Dockerfilestä rakennettu image toimii erillisen PostgreSQL:n kanssa: frontend, /api, SPA-refresh ja tietojen säilyminen sovelluksen restartissa. Pohjan vika raportoidaan, ei uusita deploymentia.
- [ ] README: käynnistys, testit, arkkitehtuuri, oletukset, tehdyt/pois jätetyt asiat ja seuraavat askeleet.
- [ ] AI-USAGE: todellinen käsin tehty pohja, Lead/Developerin työ, ihmisen tarkistus/korjaukset ja suorittamatta jäänyt näyttö.
- [ ] Itemit integroitu milestone-haaraan. Ihmisen milestone-release/tagi erotetaan tästä. Ei tagia, image-pushia tai ympäristödeployta ilman sitä koskevaa toimeksiantoa.

Jos selain/Docker/PostgreSQL ei ole ympäristössä käytettävissä, puuttuva näyttö nimetään eikä sitä väitetä läpäistyksi. Agentti ei jää budjettia kuluttavaan asentamis-/uudelleenyrityssilmukkaan. Ihminen tekee puuttuvan hyväksynnän ennen työn esittelyä.

## PARK-04 — Valinnainen hallintanäkymä

**P2; pieni; Task; riippuvuus PARK-02; oma milestone vasta M1:n jälkeen.**

Erillinen `/admin` olemassa olevien availability/active parkings/active visits -endpointien päälle. Vapaiden/varattujen/tuntemattomien paikkojen määrät ja aktiiviset pysäköinnit (paikka, rekisteri jos tiedossa, pysäköinnin alku, kulunut aika). Aktiiviset hallikäynnit tarvittaessa erikseen. Rekisteri/paikka voi olla "Ei tiedossa"; puuttuvasta yhteydestä ei päätellä auton fyysistä sijaintia. Ei kirjautumista, hinnastonmuokkausta tai portinohitusta.

- [ ] Määrät vastaavat API:a ja myös tunnistamattomat pysäköinnit näkyvät. Puuttuva rekisteri tai paikkayhteys esitetään selvästi ilman keksittyä yhdistämistä.
- [ ] Kulunut aika ei muutu laskutuksen auktoriteetiksi.
- [ ] Lataus/virhe/tyhjä tila, manuaalinen päivitys ja näppäimistökäyttö toimivat.
- [ ] Rajattu komponentti-/selainnäyttö; porttisimulaatio ei regressioidu.
- [ ] Suora reitti ja refresh toimivat samassa Docker-imagessa.

## Valmius ja ajon arviointi

Itemin valmius: hyväksyntäkriteerien näyttö, valmistunut checklist, sallittu authoritative submission ja onnistunut milestone-integraatio. Se ei automaattisesti tarkoita ihmisen hyväksyntää tai julkaistua imagea. Todentamattomat kohdat estävät valmiusväitteen tai näkyvät nimenomaisesti ihmisen hyväksyntään jätettynä rajauksena.

Kirjaa design-/implementation-aika, Lead/Developer-tokenit ja laskutuslähde, korjaukset, ympäristöpuutteet ja ihmisen työ erikseen. Providerin kustannukset ja SDK-tokenit eivät ole sama mittari. Tavoite on toimiva ennakkotehtävä ja perustellut valinnat, ei maksimaalinen agentti- tai itemimäärä.
