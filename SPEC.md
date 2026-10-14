# Pysäköintihallisimulaation spesifikaatio

Päiväys: 14.10.2026. Dokumentti erottaa tehtävän vaatimukset ja toteutusta varten tehdyt oletukset. Oletukset vahvistetaan ennen käsin tehtävää lähtöpohjaa.

## 1. Tavoite ja rajaus

.NET HTTP API, EF Core/PostgreSQL ja Vue/Nuxt-webkäyttöliittymä. Frontend käyttää suhteellista `/api`-osoitetta. Kehityksessä Nuxtin dev proxy ohjaa backendille. Tuotannossa .NET palvelee API:n ja frontendin staattiset tiedostot samasta originista yhdessä Docker-imagessa. Erillistä tuotannon Node-palvelua ei tarvita.

Aluksi yksi alustettu halli ja paikat 1–5. Tietomalli tukee hallin tunnistetta, mutta monen hallin hallintakäyttöliittymä ei kuulu tehtävään. Portti ja auto simuloidaan käyttöliittymässä. Ei kirjautumista, oikeaa maksamista, porttilaitteita, kameraintegraatiota tai viestiväylää.

Oikean hallin portin rekisteritunnistus ja paikkojen läsnäoloanturit eivät välttämättä yhdistä autoa paikkaan. Paikan käyttö voidaan kirjata ilman tietoa autosta tai hallikäynnistä. Käyttäjä voi halutessaan antaa paikkavalinnan yhteydessä Visit-tunnisteen lisätiedoksi; yhdistäminen ei ole pakollista. Laskutettava hallikäynti ja yksittäisellä paikalla oleskelu mallinnetaan silti erikseen: auton paikanvaihto ei aloita laskutusta uudelleen.

## 2. Tietomalli — käsin tehtävän pohjan sopimus

Aikaleimat: UTC, C#:ssa DateTimeOffset ja PostgreSQL:ssa timestamptz. Kestot: TimeSpan. Raha: decimal-eurot, ei liukulukuja. Tunnisteiden konkreettinen tyyppi valitaan pohjassa ja pidetään yhtenäisenä.

| Entiteetti | Kentät |
| --- | --- |
| ParkingGarage | Id, Name, ParkingFeeConfiguration (tyypitetty jsonb-taulukko) |
| ParkingLot | Id, ParkingGarageId, Number, CurrentStatus: Free / Occupied / Unknown |
| Visit | Id, ParkingGarageId, LicenseNumber, StartAt, EndAt?, ClosedAt?, Status: Active / Completed / Interrupted, ClosureReason?, Cost? |
| Parking | Id, VisitId?, ParkingLotId, StartAt, EndAt?, ClosedAt?, Status: Active / Completed / Interrupted |

Suunnitteluoletukset:

- Kapasiteetti johdetaan paikkojen määrästä; erillistä rinnakkaista TotalLots-tietoa ei tallenneta.
- Parking.VisitId on nullable. Rekisterinumero saadaan mahdollisesta Visit-suhteesta; ilman yhteyttä se on tuntematon. Paikan käyttö ja laskutettava käynti ovat itsenäisiä tietoja.
- EndAt tarkoittaa normaalisti havaittua poistumista. Interrupted-rivillä EndAt jää nulliksi; ClosedAt kertoo hallinnollisen sulkemisen ajan. Completed-rivillä ClosedAt = EndAt. Keskeytyneelle Visitille ei keksitä hintaa.
- CurrentStatus säilytetään, koska häiriössä paikka voi olla Unknown ilman luotettavaa aktiivista Parking-yhteyttä. Occupied ei tarkoita ennakkovarausta. Varauksia ei toteuteta.

Tietokantainvariantit:

- Yksikäsitteinen (ParkingGarageId, Number).
- Enintään yksi Active Visit / (ParkingGarageId, normalisoitu LicenseNumber).
- Enintään yksi Active Parking / ParkingLotId. Enintään yksi Active Parking / VisitId silloin, kun VisitId ei ole null; tunnistamattomia pysäköintejä saa olla useilla eri paikoilla.
- Viiteavaimet Visit → ParkingGarage, Parking → ParkingLot ja valinnainen Parking → Visit.
- EF-konfiguraation osittaiset yksikäsitteiset indeksit vastaavat enumien todellista tallennusmuotoa.
- Jos VisitId annetaan, service tarkistaa samassa transaktiossa, että käynti on aktiivinen ja kuuluu paikan halliin. Ilman VisitId:tä hallikäyntiä tai rekisterinumeroa ei vaadita.
- Paikan tilan tarkistus ja pysäköinnin tallennus sekä saman rekisterin käyntimuutokset tehdään atomisesti. SELECT ennen INSERTiä tai prosessin muistilukko ei yksin riitä useassa instanssissa. Aktiivisten Visitien määrää ei rajata paikkamäärään.
- Lukitusjärjestys: halli ennen paikkoja, tarvittaessa paikat tunnistejärjestyksessä. Valittu ratkaisu dokumentoidaan lyhyesti.

## 3. Hinnoittelu

| Hintaportaan kenttä | Tyyppi | Merkitys |
| --- | --- | --- |
| Amount | decimal | Euroa / alkava yksikkö, esim. 0.50m. Enintään kaksi desimaalia. |
| PerUnit | TimeSpan | Esim. 00:10:00. |
| StartDuration | TimeSpan | Portaan alku hallikäynnin alkamisesta. |
| EndDuration | TimeSpan? | Portaan loppu; viimeisellä portaalla null. |

Alustettu hinnasto: ensimmäiset 3 h: 0,50 € / alkava 10 min; seuraava aika: 0,30 € / alkava 10 min. JSONissa kestot ovat .NETin TimeSpan-merkkijonoja, esimerkiksi `00:10:00` ja `03:00:00`, eivät epäselviä numeroyksiköitä.

Validointi: Amount >= 0, PerUnit > 0, ensimmäinen alku 0, rajat kasvavat, ei aukkoja/päällekkäisyyksiä ja vain viimeinen loppu voi olla null. Ei yleiskäyttöistä sääntömoottoria.

Jokaiselle portaalle lasketaan sen aikavälille osuva kesto. Positiivinen kesto pyöristetään ylöspäin PerUnit-yksiköiksi, nollakesto antaa nolla yksikköä. Portaiden hinnat summataan. Käytetään tarkkaa kestoa/tick-jakoa, ei ensin katkaistuja kokonaisminuutteja.

| Koko käynnin kesto | Hinta |
| --- | --- |
| 0 | 0,00 € |
| 1 tick / 1 s / 1 min | 0,50 € |
| 10 min | 0,50 € |
| 10 min + 1 tick | 1,00 € |
| 2 h 59 min 59 s | 9,00 € |
| 3 h | 9,00 € |
| 3 h + 1 tick | 9,30 € |
| 3 h 10 min | 9,30 € |
| 3 h 10 min + 1 tick | 9,60 € |
| 4 h | 10,80 € |

TimeProvider injektoidaan serviceen. Asiakas ei määrää laskutuksen StartAt/EndAt-aikoja. Lopullinen Cost tallennetaan ulosajossa. Hinnasto on tämän version ajon aikana kiinteä; muokkaus kesken käynnin ja hinnastoversiot eivät kuulu tehtävään. Päättyneiden käyntien summa ei muutu myöhemmin.

## 4. Komennot ja poikkeamat

Rekisteri normalisoidaan Trim + invariantti uppercase. Tyhjä arvo ja yli 20 merkkiä hylätään. Sisäisiä välimerkkejä ei poisteta eikä kansainvälistä formaattivalidointia rakenneta. Sama normalisointi kaikissa operaatioissa.

### EnterParkingGarage(garageId, licenseNumber)

- Saatavuuden isFull tarkoittaa, ettei yhtään Free-paikkaa ole. Aktiivisten Visitien määrä raportoidaan erikseen eikä siitä päätellä paikkojen käyttöä.
- Sisäänajohavainto kirjataan myös silloin, kun isFull=true. Täysi halli on saatavuustieto, ei sisäänajon estävä liiketoimintasääntö. Tuntematon halli hylätään.
- Suunnitteluoletus: konfiguroitava DuplicateEntryWindow = 2 min; ikä lasketaan palvelimen ajasta alkuperäiseen StartAt-aikaan.
- Saman rekisterin Active Visit, ikä <= 2 min: palautetaan sama Visit Duplicate-tuloksena. StartAt, kapasiteetti ja paikat eivät muutu.
- Ikä > 2 min: vanha Visit → Interrupted, ClosureReason = MissingExitBeforeNewEntry, ClosedAt = nyt, EndAt/Cost = null. Uusi Visit alkaa nyt. Sulkeminen ja avaaminen ovat sama transaktio; samalle rekisterille jää yksi aktiivinen hallikäynti.
- Vanhaan Visitiin mahdollisesti liitetty Parking ja paikan tila eivät muutu. Uusi porttihavainto ei kerro paikan vapautumisesta. Yhteys vanhaan käyntiin säilyy historiallisena lisätietona eikä siirry automaattisesti uuteen käyntiin.
- Aikaraja on käyttäjän valitsema heuristiikka, ei luotettava häiriön/väärinkäytön tunnistin. Yli rajan tuleva havainto voi aloittaa laskutuksen uudelleen. Riski dokumentoidaan. Pysyvä request-id-idempotenssi on jatkokehitys, ei tämän version vaatimus.

### EnterParkingLot(lotId, visitId?)

Paikka on Free. VisitId voidaan jättää pois: silloin luodaan tunnistamaton Parking ilman hallikäyntiä tai rekisterinumeroa. Jos VisitId annetaan, Visitin on oltava Active ja samasta hallista eikä sillä saa olla muuta aktiivista Parkingia. Parkingin luonti ja paikan Occupied-tila ovat sama transaktio. Kilpailijoista vain yksi onnistuu. Tunnetun auton paikanvaihto edellyttää ensin poistumista vanhalta paikalta.

### LeaveParkingLot(lotId)

Simuloi paikan vapautumisen havaintoa: sulkee mahdollisen aktiivisen Parkingin Completed-tilaan ja muuttaa paikan Free-tilaan atomisesti. Visit ja laskutus jatkuvat. Jo vapaan paikan vapautus on onnistunut no-op. Unknown/Occupied ilman aktiivista Parkingia voidaan vapauttaa tällä nimenomaisella havaintokomennolla; puuttuva Parking-havainto voidaan raportoida varoituksena. Pelkkä puuttuva VisitId on normaali tilanne, ei virhe tai varoitus. Ei automaattista taustavapautusta.

### LeaveParkingGarage(garageId, licenseNumber)

- Päätetään aktiivinen Visit, lasketaan hinta StartAt → palvelimen nyt ja tallennetaan Cost/EndAt/ClosedAt. Palautetaan kuitti ja simuloitu porttipäätös.
- Ulosajo ei sulje Parking-rivejä tai muuta paikkojen tiloja edes silloin, kun VisitId-yhteys tunnetaan. Paikan vapautuminen kirjataan erillisellä LeaveParkingLot-havainnolla. Laskutus perustuu Visitiin eikä riipu paikkatiedon olemassaolosta.
- Ilman aktiivista käyntiä: ACTIVE_VISIT_NOT_FOUND, gateOpened=false ja requiresCustomerService=true. Hintaa ei arvata.
- Toistettu ulosajo ei tee uutta veloitusta tai muuta aiempaa Costia. Rekisteriin perustuva toisto ilman aktiivista käyntiä palauttaa yllä olevan poikkeaman, ei sattumanvaraista vanhaa kuittia.
- Asiakaspalvelun ohitus ja manuaalinen laskutus jäävät jatkokehitykseen. UI näyttää yhteydenotto-ohjeen ilman keksittyä puhelinnumeroa.

## 5. REST API

Ei event sourcingia, viestiväylää tai erillistä command frameworkia. Rajapinnan tunnisteet voivat olla pohjassa valittua tyyppiä.

| Metodi/reitti | Tarkoitus |
| --- | --- |
| GET /api/garages | Alustetut hallit, aluksi yksi. |
| GET /api/garages/{garageId}/availability | capacity, activeVisits, freeLots, occupiedLots, unknownLots, isFull, paikkalista. |
| GET /api/garages/{garageId}/visits/active | Aktiiviset käynnit ja mahdollinen yhdistetty paikka; tukee reloadia. Ilman yhteyttä paikka ei ole tiedossa. |
| GET /api/garages/{garageId}/parkings/active | Paikkojen aktiiviset pysäköinnit, myös ilman VisitId:tä. Rekisteri nullable; pysäköintiaika saadaan Parking-riviltä. |
| POST /api/garages/{garageId}/entries | licenseNumber → Entry/Duplicate/RecoveredMissingExit ja VisitId. |
| POST /api/garages/{garageId}/lots/{lotId}/entries | visitId? → tunnistettu tai tunnistamaton pysäköinti. Myös tyhjä JSON-olio hyväksytään. |
| POST /api/garages/{garageId}/lots/{lotId}/exit | Paikan vapautushavainto. |
| POST /api/garages/{garageId}/exits | licenseNumber → VisitId, ajat, duration, cost, currency=EUR. |

400: virheellinen syöte. 404: halli/paikka/käyntitunniste puuttuu tai kuuluu toiseen halliin. 409: LOT_UNAVAILABLE, VISIT_NOT_ACTIVE, VISIT_ALREADY_PARKED tai ACTIVE_VISIT_NOT_FOUND. Yhdenmukainen ProblemDetails, extensions: code, gateOpened=false ja tarvittaessa requiresCustomerService. Cost on JSON-numero, ei lokalisoitu teksti.

Saatavuus on tilannekuva, ei paikkavaraus. Backend tarkistaa kaiken uudelleen kirjoittavan operaation transaktiossa. Tietokantakonfliktit palautetaan hallitusti; ei SQL-/stack trace -vuotoa asiakkaalle.

## 6. Käyttöliittymä

Pakollinen `/`: hallin nimi ja saatavuus, rekisterikenttä ja sisäänajo, viiden paikan itsenäinen käyttötilasimulaatio, valinnainen käyntiin yhdistäminen, paikalta poistuminen/vaihto, ulosajo ja kuitti. Paikan voi merkitä käytetyksi valitsematta autoa tai luomatta hallikäyntiä. Täysi halli näytetään tietona; se ei estä sisäänajohavainnon kirjaamista. Kuitti näyttää rekisterin, ajat, keston ja eurohinnan. Occupied/Unknown-paikkaa ei voi valita.

Paikkakohtainen toiminto "Simuloi paikan vapautuminen" on käytettävissä Occupied/Unknown-paikoille myös silloin, kun niihin ei liity luotettavaa aktiivista käyntiä. Näin paikan havainto voidaan simuloida ilman valinnaista admin-näkymää. Paikan käyttö ja hallin sisään-/ulosajot ovat erillisiä toimintoja.

Saatavuus päivitetään onnistumisen ja konfliktin jälkeen. Toisen selaimen muutokset voi nähdä manuaalisella päivityksellä; SignalR/SSE ei ole vaatimus. Reloadin jälkeen aktiiviset käynnit luetaan API:sta ja niitä voi jatkaa. Oikeellisuus ei riipu selaimen muistista.

Lataustila, kenttävirheet, konfliktit, Duplicate, keskeytyksen varoitus ja asiakaspalvelutarve näkyvät selvästi. Painikkeet estävät tahattoman rinnakkaislähetyksen, mutta backend ei luota tähän. Labelit, näppäimistökäyttö ja muu kuin pelkkään väriin perustuva palaute.

Valinnainen `/admin`: erillinen lukeva näkymä samassa sovelluksessa. Paikkamäärät ja aktiiviset pysäköinnit taulukossa (paikka, rekisteri jos tiedossa, pysäköinnin alku, kulunut aika). Tuntematon rekisteri näytetään tekstinä "Ei tiedossa". Aktiiviset hallikäynnit näytetään tarvittaessa erikseen; puuttuva paikkayhteys tarkoittaa "Paikka ei tiedossa", ei päätelmää että auto olisi ajamassa. Ei kirjautumista, asetusten muokkausta tai portinohitusta.

## 7. Hyväksyntä ja testaus

Jokainen toiminnallinen työpaketti lisää omat testinsä. Loppuhyväksyntä ei aloita koko testikehikon rakentamista.

- Hintataulukon rajat, negatiivinen kesto ja virheellinen hinnasto; Fake TimeProvider, ei oikeaa odottamista.
- Normaali sisäänajo, paikka, paikanvaihto, ulosajo, tallennettu kuitti ja reload-jatkaminen.
- isFull=false kun vähintään yksi paikka on Free, isFull=true kun kaikki ovat Occupied/Unknown. Käyntimäärä ei korvaa paikkahavaintoja; sisäänajon kirjaaminen ei esty täydestä hallista.
- Paikalle ajaminen ja sieltä poistuminen ilman VisitId:tä tai rekisteriä; useita tunnistamattomia pysäköintejä eri paikoilla. Valinnainen VisitId-yhteys validoidaan.
- Täsmälleen 2 min → Duplicate; 2 min + 1 tick → uusi Visit ja vanha Interrupted ilman keksittyä EndAt/Costia.
- Hallikäynnin ulosajo tai keskeytys ei muuta paikkatietoa. Vain paikan oma vapautushavainto sulkee Parkingin ja vapauttaa paikan; se toimii myös ilman VisitId:tä.
- Puuttuva sisäänajo → asiakaspalvelu, ei avautumista tai veloitusta. Ulosajon toisto ei veloita uudelleen.
- Rinnakkaiset paikkavalinnat ja sisäänajot oikealla PostgreSQL:llä ja erillisillä DbContext-yhteyksillä. InMemory/SQLite ei todista PostgreSQL-lukitusta tai osittaisia indeksejä.
- Kilpailun jälkeen korkeintaan yksi Active Parking per paikka ja per annettu VisitId, ja häviäjälle hallittu vastaus. Null-VisitId:t eivät estä muiden paikkojen tunnistamattomia pysäköintejä. Saman rekisterin sisäänajokilpailu ei luo kahta Active Visitiä.
- Yhden originin konttikäyttö ja SPA-reitin refresh ilman erillistä Node-palvelua.

Build/test ei korvaa selain- tai konttihyväksyntää. Suoritettu, epäonnistunut ja ympäristön vuoksi suorittamatta jäänyt tarkistus erotellaan. Ei valmiusväitettä pelkän agentin vakuutuksen perusteella.

## 8. Pois rajatut / seuraavat ominaisuudet

Kirjautuminen, maksut, oikeat portit/anturit, ennakkovaraukset, huoltotilat, monihalli-UI, ajonaikainen hinnasto/hintaversiot, varma kameraviestien idempotenssi, asiakaspalvelun korjaus/ohitus ja automaattinen Unknown-sovittaminen. Ennen oikeaa käyttöä määritellään henkilötietojen säilytys ja suojaus. Simulaatio ei väitä tuotantovalmiutta.
