# Testikattavuuden tarkistus 15.10.2026

Katselmoinnissa tarkistettiin olennaiset toiminnalliset vaatimukset ja niiden testit. Rivipeittoprosenttia ei mitattu. Testisarja sisältää puhtaita yksikkötestejä, tuotantopalvelujen testejä paikallisella EF-testiharnessilla, HTTP-sopimustestejä TestServerillä sekä Vue-komponentti-/DOM-testejä. Niitä ei kaikkia luokitella yksikkötesteiksi.

## Olennaiset toimintapolut

| Toiminto | Testit ja varmennetut asiat |
| --- | --- |
| Hinnan laskenta | `PricingTests`, `ServiceTests`: alkava aikayksikkö, 3 h raja ja seuraava tick, decimal-hinnat, nolla/negatiivinen kesto, virheelliset portaat ja suuri kesto. |
| Halliin saapuminen ja poistuminen | `ServiceTests`, `ApiTests`, `RecoveryTests`, `RecoveryApiTests`: rekisterin validointi/normalisointi, tuplakutsu, puuttuva poistuminen, puuttuva saapuminen, tallennettu kuitti ja riippumattomat paikkahavainnot. |
| Paikan käyttö | `ServiceTests`, `ConcurrencyTests`, `index.test.ts`: vapaa/varattu/tuntematon, valinnainen auto, väärän hallin tai päättyneen käynnin linkitys, jo pysäköity auto, paikanvaihto, kilpailevat paikalliset komennot ja käyttäjälle näkyvä virhe. |
| Simulaation aika | `EventTimeTests`, `EventTimeInput.test.ts`, `index.test.ts`: aikavyöhyke, kelvoton/puuttuva aika, kerran ratkaistu aika, historian aikarajat ja simulaation poistaminen käytöstä. |
| Hallit ja pohjaeditori | `GarageManagementTests`, `ModelTests`, `garageAdmin.test.ts`, `GarageLayoutView.test.ts`: validointi, ruudukon maalaus/mitat/numerot, siirrot, poistot, identiteetin ja historian säilyminen, vanhentunut versio, palautus ja värikoodatut solut. |
| Kartta | `GarageMap.test.ts`: pisteiden suodatus ja nollakoordinaatit, koordinaatit ennen markkerin liittämistä, puuttuvasta sijainnista valintaan siirtyminen, hallilinkit ja tietojen päivitys. Hoverin visuaalinen pinojärjestys vaatii selaintarkistuksen. |
| Kuvan tunnistus | `PlateRecognitionTests`, `OpenRouterContractTests`, `ImageEntryTests`, `ImageEntryPanel.test.ts`, `garageEvents.test.ts`: tiedostorajat, oikea dekoodaus, epäselvä/provider-virhe, rakenteinen vastaus, timeout/peruutus, ei kirjoitusta virheessä, automaattinen sisäänajo, onnistumisen jälkeinen tyhjennys ja virheen jälkeinen uusi yritys. |
| Reaaliaikaisuus | `GarageEventsTests`, `garageEvents.test.ts`: commitin jälkeiset tapahtumat, virheiden rollback, SSE-yhteyden katko/palautuminen, vanhentuneen vastauksen torjunta sekä syötteiden säilyminen. |
| Hinnaston muokkaus | `GarageDetailsTests`, `GarageDetailsCoverageTests`, `GarageDetails.test.ts`: tallennus, vanhentunut versio, aktiiviset käynnit, validointimatriisi, rollback, paikallinen rinnakkainen päivitys, desimaalien esitysmuoto, portaiden lisäys/poisto, latausvirhe ja päällekkäisten tallennusten esto. |
| Vierailuloki | `GarageDetailsTests`, `GarageDetailsCoverageTests`, `GarageDetails.test.ts`, `garageApi.test.ts`: hallirajaus, tasatilanteiden deterministinen järjestys, sivutus, käyntien tilat/hinnat, virhe ja uudelleenyritys, SSE:n ohittaman vastauksen torjunta sekä haun peruminen komponentin poistuessa. |
| Hallinäkymä ja paikkamodaali | `index.test.ts`: navigaatio ja välilehtien näppäimistökäyttö, vain valitun paikan toiminnot, sulkeminen/Escape-tapahtuma/välilehden vaihto, tapahtuma-ajan säilyminen ja modaalissa näkyvä toast. |
| Kokoavat polut ja rajapinta | `V2AcceptanceTests`, `V3AcceptanceTests`, `ModelTests`, `ApiTests`: hallikohtainen tila, kuva → käynti → riippumaton paikka → ulosajo → kuitti, offline-malli/migraatiosopimus, OpenAPI ja turvalliset virhevastaukset. |

## Täydennykset ja korjaukset

Lisättiin 13 backend- ja 7 frontend-tapausta. Tarkistuksessa löytyi hinnastoeditorin puutteita: uuden portaan raja oli aina 3 h, poistaminen saattoi jättää aukon ja tallennuksen ohjelmallinen tuplalähetys oli mahdollinen. Korjaukset varmistettiin käyttäytymistesteillä. Muutoksia tuotannon tietokantarakenteeseen tai riippuvuuksiin ei tarvittu.

Suoritettu onnistuneesti: koko backend-sarja **264 testiä, 0 failed / 0 skipped**, koko frontend-sarja **99 testiä**, ESLint, Nuxt-tyypitys ja frontendin tuotantobuild. Backend-build tuotti OpenAPI-kuvauksen offline. Lopullinen uusien backend-testien muotoilu ja lokin SSE-testin tarkennus varmistettiin vielä kohdennetuilla ajoilla.

## Erillinen ympäristövalidointi

Oikean PostgreSQL:n lukitus-/transaktiosemantiikkaa ei todisteta InMemory-harnessilla. Oikean selaimen natiivi dialogifokus/Escape/backdrop, responsiivinen ulkoasu, WebGL, karttalaatikoiden päällekkäisyys, ulkoiset karttatiilet, kahden selaimen SSE proxyn läpi, oikeiden autokuvien mallilaatu, Docker ja prosessin restart ovat edelleen erillisiä ympäristötarkistuksia. Näitä ei merkitä yksikkötesteillä hyväksytyiksi.
