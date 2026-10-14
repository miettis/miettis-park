# AI:n käyttö

## Toteutunut 14.10.2026 mennessä

Codexia käytettiin tehtävänannon tulkinnan keskustelukumppanina ja tämän suunnitelman kirjoittamiseen. Käyttäjä valitsi stackin ja käsin tehtävän pohjan, ehdotti Visit/Parking-jakoa, valinnaista auton ja paikan yhdistämistä, TimeSpan-hintaportaita sekä uusintasisäänajon aikarajaheuristiikkaa. AI auttoi tarkentamaan poikkeamia, transaktioita, hyväksyntää ja työpakettien kokoa.

Projektin työ on suunniteltu PullPotentialin Lead/Developer-mallille: kokonaiset toiminnalliset työpaketit, rajattu delegointi ja toteutuksen mukana kertyvät testit.

Suunnitteluvaiheessa AI ei vielä toteuttanut sovelluskoodia tai julkaissut imagea. Pohjan toteutus on kirjattu erikseen alla.

## Täydennä toteutuksen aikana

- Käsin tehdyn pohjan commit ja todellinen sisältö:
- PullPotentialin projekti/milestone/item-tunnisteet:
- Lead/Developer/mahdollinen PM, mallit ja reasoning:
- Delegointiesimerkki: tavoite, rajat, tulos, Leadin tarkistus:
- AI:n koodi; ihmisen hyväksymät/muuttamat/hylkäämät ratkaisut:
- Käytetyt ohjeet/skills/MCP-työkalut ja niiden tarkoitus:
- Todelliset komennot/tulokset ja selain-/konttihyväksyntä:
- Suorittamatta jääneet tarkistukset ja syyt:
- Aika, tokenit/kustannukset ja ihmisen korjaukset erikseen:

Haastatteluun lyhyt yhteenveto ja pari konkreettista esimerkkiä. Ei tarvetta julkaista kokonaisia transkripteja, salaisuuksia tai epäolennaisia henkilötietoja. Agentin väite ei ole oma testinäyttö.

## Käyttöliittymäluonnos

AI tuotti ui-mocks-hakemiston itsenäisen HTML/CSS/JavaScript-prototyypin: porttisimulaatio, paikkahavainnot, lukeva hallinta, kuitti ja tilaviestit. Mukana valinnainen auton yhdistäminen ja erilliset hallikäynti-/paikkatilat. JavaScriptin keskeiset mockup-polut tarkistettiin DOM-stub-testillä ja desktop-ulkoasu headless Chromen kuvalla. Tämä ei ole Nuxt-sovelluksen toteutus tai tuotannon backend-/selainhyväksyntä.

## Projektipohjan toteutus 14.10.2026

Käyttäjän pyynnöstä Codex toteutti .NET/Nuxt-projektipohjan, entityt, EF-konfiguraation ja alkumigraation, Docker-/Compose-tiedostot sekä semanttisten tagien julkaisuworkflow'n. Käyttäjä edellytti .NETin sisäänrakennettua OpenAPI-kirjastoa ja generointia ilman käynnissä olevaa sovellusta. Toteutuksessa erotettiin design-time metadata tietokannan käynnistystoimista. Ohjeet ovat AGENTS.md:ssä. Toiminnallista pysäköintikoodia ei vielä toteutettu.

AI ajoi backend/frontend-buildit, lintin ja tyypityksen, offline-migraatio- ja malliprobot sekä paikalliset HTTP/proxy-smoketestit. Todelliset rajat ja riippuvuushavainnot: [VALIDATION.md](VALIDATION.md). Docker/PostgreSQL-integraatiota tai GitHub-julkaisua ei ajettu. PullPotentialin projektia tai backlogia ei luotu tällä toteutuskierroksella.

## PullPotential PARK-01 (projekti 34, item 1116)

Lähtöpohja on käyttäjän ilmoittama dev-base `a82ca21`. Park Lead toteutti hyväksytyn designin mukaiset DTO:t, kahdeksan API-reittiä, yhteisen normalisoinnin/ProblemDetails-sopimuksen, decimal/tick-hinnoittelun, PostgreSQL-transaktiot/hallilukon ja palvelut sekä xUnit-/TestServer-/offline-mallitestit. Käytössä olivat worktreen AGENTS/SPEC/BACKLOG/STARTING-POINT, linkitetty knowledge 103/104 ja engineering/verification-skillit; nykyinen käyttäjän hyväksymä paikallinen testirajaus ohitti repon vanhan ulkoisen ympäristön portin. Koodin generoitu OpenAPI tulee Microsoftin sisäänrakennetusta offline-työkalusta.

Member-251 sai rajatun delegoinnin vain clientiin ja root check-runneriin valmiin backend DTO-sopimuksen perusteella. Lead odotti yhden kirjoittajan työskentelyä ja tarkisti UI:n, sopimukset sekä testit. Lead poisti mockista mukaan tulleen tämän itemin ulkopuolisen hallintavälilehden, täydensi paikanvaihdon ja reload-jatkamisen testit, lisäsi lataus/rinnakkaislähetyksen eston ja asiakaspalvelu/tyhjä-tilatestit, jätti rekisterin uppercase-normalisoinnin palvelimen auktoriteetiksi sekä korjasi uuden Vitest-testiriippuvuuden audit-havainnot lukittuun 4.1.11-versioon. Nuxt/Vue-versioita, tuotannon mallia ja deploymentia ei vaihdettu.

Suoritettu näyttö ja korjatut testifiksuurivirheet sekä vielä ajamattomat PostgreSQL/Docker/selain/restart-tarkistukset eritellään VALIDATION.md:ssä. Paikalliset testit eivät korvaa oikean tietokantamoottorin tai selaimen validointia; ne ovat käyttäjän hyväksymä agenttityön completion-näyttö. Ihmisen uutta selain-/konttihyväksyntää tai koodikorjausta ei ole kirjattu tälle itemille. PullPotential hallitsee commitit/integraation; agentit eivät tehneet commitia, tagia, image-pushia tai releasea. Tokeni-/kustannus-/aikatiedot kuuluvat PullPotentialin execution/evidence-historiaan; niitä ei arvioida tässä dokumentissa.
## PullPotential PARK-02 (projekti 34, item 1117)

Park Lead toteutti konfiguroitavan DuplicateEntryWindow-asetuksen ja kriittisen recovery-siirtymän: sulkeminen tallennetaan ennen korvaavaa Active-riviä samassa tuotannon transaktiossa. Lead vastasi invariansseista, paikallisesta snapshot-rollback-/rendezvous-kilpailuharnessista sekä service/API/offline-matriisista ja näyttöketjusta. Hinnoittelukaavaa, PostgreSQL-provideria, skeemaa, kahdeksaa reittiä tai deploymentia ei uusittu.

Member-251 sai delegoinnin #27 vain nykyisiin clientin app/DOM-tiedostoihin valmiin DTO-/virhesopimuksen mukaan: erottele Duplicate/recovery/virheet, poista vanha kuitti ulosajoyrityksellä ja testaa resurssien refresh. Lead odotti yhden kirjoittajan mallilla, tarkisti diffin ja DOM-testit sekä lisäsi recoveryn ja kilpailussa suljetun Visitin valintalistan tarkistukset. LotCard tyhjentää nyt suljetun Visitin aiemman valinnan myös valmiiksi avoimessa lomakkeessa. Riippuvuuksia ei vaihdettu. Testit käyttävät API-stubeja, eivät selainta tai ulkoista palvelua.

Hyväksytty design ja pysyvä checklist sekä nykyrevision platform evidence ovat PullPotentialissa; paikalliset tulokset, alkuvaiheen restore-puute, riskit ja ajamattomat ympäristötarkistukset on erotettu VALIDATION.md:ssä. Heuristiikan laskutuksen uudelleenaloitus-/väärinkäyttöriski sekä request-id:n ja asiakaspalvelukorjauksen/ohituksen puuttuminen on dokumentoitu READMEen. Testituplan serialisointi ja tilannekuvan palautus eivät todista PostgreSQL:n lukitus-/rollback-/uniikkiussemantiikkaa. Oikea PostgreSQL, Docker, restart ja selain jäävät myöhemmiksi ihmisen tarkistuksiksi, eivät tämän agenttityön portiksi.

Käytössä olivat samat AGENTS/SPEC/knowledge 103/104 sekä engineering/verification-ohjeet. Ihmisen uutta ympäristöhyväksyntää tai koodikorjausta ei ole kirjattu. Ei commitia, tagia, pushia tai releasea; PullPotential vastaa Git-finalisoinnista. Malli-/aika-/tokeni-/kustannustiedot löytyvät execution-historiasta, eikä niitä arvioida tässä.

## PullPotential PARK-04 (projekti 34, item 1118)

Park Lead toteutti hyväksytyn pienen designin suoraan ilman delegointia: lukeva `/admin`, rajattu useAdminDashboard-composable, sivupalkin reittilinkit ja taulukkotyylit, 15 paikallista admin-DOM-testiä sekä kaksi aidon service/API-putken lukusopimustapausta testikellolla. Kriittinen Visit/Parking-riippumattomuus säilyy: historiallista yhteyttä ei siirretä eikä rekisteristä yhdistetä uuteen käyntiin. Backendin tuotantopalvelut, tietomalli, transaktiot ja hinnoittelu eivät muuttuneet. Ei uusia riippuvuuksia, authia tai kirjoittavia hallintatoimintoja.

Lead tarkisti tuotannon koodin/testit ja korjasi testifiksuurin mount-ajoituksen sekä DOMWrapper-tyypityksen. Promise.allSettled toteuttaa designin yhteisen julkaisun ja odottaa virheessäkin kaikkien käynnistettyjen hakujen päättymistä, joten refresh-vartio ei avaudu kesken muiden hakujen. Paikalliset tulokset (99 backend / 26 DOM, lint/typecheck/build, offline OpenAPI/malli/SQL) sekä todelliset näyttörajat on kirjattu VALIDATION.md:hen. Nykyrevision platform evidence ja pysyvä checklist/submission kuuluvat PullPotentialiin.

Käytössä olivat AGENTS/SPEC/BACKLOG/STARTING-POINT, knowledge 103/104, engineering/verification-skillit sekä ui-mocks vain ulkoasureferenssinä. Ulkoisia versionkohtaisia API-ratkaisuja ei tarvittu: nykyisiä Vue/Nuxt/DTO/testiharness-sopimuksia käytettiin sellaisenaan. Oikea selain/näppäimistö/visuaalinen hyväksyntä, saman Docker-imagen suora `/admin` ja refresh, oikea PostgreSQL ja restart ovat ajamatta, myöhempiä ihmisen tarkistuksia eivätkä agenttityön portteja. Ihmisen uusia koodikorjauksia tai ympäristöhyväksyntää ei ole kirjattu. Ei commitia/pushia/tagia/releasea; PullPotential hallitsee Git-finalisoinnin. Aika/tokenit/kustannukset löytyvät execution-historiasta, niitä ei arvioida tässä.