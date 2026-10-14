# Toteutusohjeet

## Agenttityön hyväksyntä ilman ulkoisia palveluja — 14.10.2026
Tehtävien toteutus ja valmistuminen eivät edellytä käynnissä olevaa PostgreSQL:ää, Docker-daemonia, erillistä testikantaa/yhteysmerkkijonoa, selainajuria, karttapalvelua tai ihmisen ennakkoon tekemää hyväksyntää. Aloita ja jatka toteutusta niiden puuttumisesta huolimatta; älä pyydä clarificationia tai jätä koko tehtävää tekemättä tästä syystä. .NET/Node ja projektin tavallinen riippuvuuksien restore/asennus ovat edelleen buildin edellytyksiä.
Tuotantosovellus käyttää edelleen EF Corea/PostgreSQL:ää; älä korvaa sitä muistivarastolla tai vaihda stackia. Pakollinen näyttö: backend-build ja aidot palvelu-/API-testit testikellolla sekä paikallisilla testituplilla tai testiin rajatulla embedded-providerilla, frontend lint/typecheck/build ja mahdolliset DOM-komponenttitestit sekä offline OpenAPI/migraatio-SQL/mallin tarkistus. Testit eivät saa tarvita palvelinprosessia, verkkopalvelua, konttia tai salaisia asetuksia. Kartan ulkoinen riippuvuus korvataan testeissä stubilla.
Oikean PostgreSQL:n lukitus-/kilpailu-/migraatioajot, Docker-käynnistys, oikea selain/karttapalvelu ja restart-pysyvyys ovat erillisiä myöhempiä ihmisen tarkistuksia, eivät tämän agenttityön submission-/checklist-portteja. Dokumentoi ne ajamattomiksi. Älä väitä InMemory/SQLite-testin todistavan PostgreSQL-semanttiikkaa, merkitse skipped-integraatiotestiä läpäistyksi tai heikennä tuotannon invariantteja. Paikallinen hyväksyntä ja vielä vahvistamaton ympäristökäyttäytyminen erotetaan.
Jos hyväksytyssä designissa/pysyvässä checklistissä on vanha ulkoisen ympäristön vaatimus, tee tämän käyttäjän hyväksymän rajauksen mukainen design amendment ja päivitä checklist vaatimuksen sisältöä vastaavaksi. Älä kuittaa tekemätöntä ulkoista tarkistusta tehdyksi. Työ voi valmistua paikallisella näytöllä ja dokumentoidulla myöhemmän validoinnin listalla.


Lue SPEC.md ja käsiteltävä työpaketti BACKLOG.md:stä. STARTING-POINT.md kuvaa valmiin infrastruktuurin. ui-mocks on suunnitteluaineistoa, ei sovelluksen lähdekoodi.

- Backend: .NET, EF Core, PostgreSQL. Frontend: Nuxt/Vue, staattinen SPA.
- Käytä suhteellista /api-polkuja; tuotannossa backend palvelee myös frontendin.
- Käytä .NETin sisäänrakennettua OpenAPI-tukea. `node scripts/openapi.mjs` toimii ilman palvelimen tai tietokannan käynnistämistä. Älä lisää generointiin ulkoisia riippuvuuksia tai startup-sivuvaikutuksia.
- Paikan ja auton yhdistäminen on valinnaista. Käynti ei muuta paikkahavaintoja automaattisesti.
- Älä muuta pohjaa tai tietomallia laajasti ilman perusteltua tarvetta. Älä lisää autentikointia tai maksupalvelua.
- C#: file-scoped namespaces; aaltosulkeet kaikille kontrollirakenteille; yksi lause riville. Attribuutit omille riveilleen ominaisuuden yläpuolelle. Käytä tavanomaista .NET-muotoilua.
- Raportoi suoritetut tarkistukset ja erota paikalliset buildit tietokanta-, kontti- ja käyttöliittymävalidoinnista.
