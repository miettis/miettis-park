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
