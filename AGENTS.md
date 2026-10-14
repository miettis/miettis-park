# Toteutusohjeet

Lue SPEC.md ja käsiteltävä työpaketti BACKLOG.md:stä. STARTING-POINT.md kuvaa valmiin infrastruktuurin. ui-mocks on suunnitteluaineistoa, ei sovelluksen lähdekoodi.

- Backend: .NET, EF Core, PostgreSQL. Frontend: Nuxt/Vue, staattinen SPA.
- Käytä suhteellista /api-polkuja; tuotannossa backend palvelee myös frontendin.
- Käytä .NETin sisäänrakennettua OpenAPI-tukea. `node scripts/openapi.mjs` toimii ilman palvelimen tai tietokannan käynnistämistä. Älä lisää generointiin ulkoisia riippuvuuksia tai startup-sivuvaikutuksia.
- Paikan ja auton yhdistäminen on valinnaista. Käynti ei muuta paikkahavaintoja automaattisesti.
- Älä muuta pohjaa tai tietomallia laajasti ilman perusteltua tarvetta. Älä lisää autentikointia tai maksupalvelua.
- C#: file-scoped namespaces; aaltosulkeet kaikille kontrollirakenteille; yksi lause riville. Attribuutit omille riveilleen ominaisuuden yläpuolelle. Käytä tavanomaista .NET-muotoilua.
- Raportoi suoritetut tarkistukset ja erota paikalliset buildit tietokanta-, kontti- ja käyttöliittymävalidoinnista.
