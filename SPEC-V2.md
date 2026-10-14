# V2 – hallien ylläpito, ruudukkopohja ja kartta

## Agenttityön hyväksyntä ilman ulkoisia palveluja — 14.10.2026
Tehtävien toteutus ja valmistuminen eivät edellytä käynnissä olevaa PostgreSQL:ää, Docker-daemonia, erillistä testikantaa/yhteysmerkkijonoa, selainajuria, karttapalvelua tai ihmisen ennakkoon tekemää hyväksyntää. Aloita ja jatka toteutusta niiden puuttumisesta huolimatta; älä pyydä clarificationia tai jätä koko tehtävää tekemättä tästä syystä. .NET/Node ja projektin tavallinen riippuvuuksien restore/asennus ovat edelleen buildin edellytyksiä.
Tuotantosovellus käyttää edelleen EF Corea/PostgreSQL:ää; älä korvaa sitä muistivarastolla tai vaihda stackia. Pakollinen näyttö: backend-build ja aidot palvelu-/API-testit testikellolla sekä paikallisilla testituplilla tai testiin rajatulla embedded-providerilla, frontend lint/typecheck/build ja mahdolliset DOM-komponenttitestit sekä offline OpenAPI/migraatio-SQL/mallin tarkistus. Testit eivät saa tarvita palvelinprosessia, verkkopalvelua, konttia tai salaisia asetuksia. Kartan ulkoinen riippuvuus korvataan testeissä stubilla.
Oikean PostgreSQL:n lukitus-/kilpailu-/migraatioajot, Docker-käynnistys, oikea selain/karttapalvelu ja restart-pysyvyys ovat erillisiä myöhempiä ihmisen tarkistuksia, eivät tämän agenttityön submission-/checklist-portteja. Dokumentoi ne ajamattomiksi. Älä väitä InMemory/SQLite-testin todistavan PostgreSQL-semanttiikkaa, merkitse skipped-integraatiotestiä läpäistyksi tai heikennä tuotannon invariantteja. Paikallinen hyväksyntä ja vielä vahvistamaton ympäristökäyttäytyminen erotetaan.
Jos hyväksytyssä designissa/pysyvässä checklistissä on vanha ulkoisen ympäristön vaatimus, tee tämän käyttäjän hyväksymän rajauksen mukainen design amendment ja päivitä checklist vaatimuksen sisältöä vastaavaksi. Älä kuittaa tekemätöntä ulkoista tarkistusta tehdyksi. Työ voi valmistua paikallisella näytöllä ja dokumentoidulla myöhemmän validoinnin listalla.


14.10.2026. Tämä täydentää [MVP-spesifikaatiota](SPEC.md). Toteutus alkaa hyväksytyn MVP:n jälkeen; dokumentti ei väitä MVP:tä valmistuneeksi. Työpaketit: [BACKLOG-V2.md](BACKLOG-V2.md).

## Sovittu rajaus

- Ei kirjautumista, käyttöoikeusrooleja tai erillistä ylläpitosovellusta. Ylläpitosivut ovat /admin-osiossa; niiden URL ei ole pääsynhallinta.
- Hallien luonti ja muokkaus: nimi, osoite, karttasijainti ja yhden kerroksen ruudukkopohja.
- Yksinkertainen maalauseditori: ajorata, parkkipaikka ja pyyhekumi. Maalaus onnistuu hiirellä vetämällä; myös yksittäinen ruudun klikkaus toimii. Ei alueen täyttöä, kiertämistä tai reititystä.
- Karttamerkissä näkyy varattujen paikkojen määrä suhteessa kokonaismäärään. Merkkiä klikkaamalla suoraan hallin värikoodattuun paikkanäkymään, ei infolaatikon välivaihetta.
- Käyttötila muuttuu edelleen MVP:n simulaatiolla. Tässä varattu tarkoittaa Occupied-tilaa, ei ennakkovarausta.
- Nykyinen .NET/EF/PostgreSQL/Nuxt-, OpenAPI- ja yhden Docker-imagen pohja säilyy. Ei uutta infrastruktuurikerrosta.

## Näkymät

| Reitti | Sisältö |
| --- | --- |
| / | Hallit kartalla ja listana. Nimi, osoite ja esimerkiksi 3/5 varattu. Merkki/listalinkki avaa hallin. |
| /garages/{id} | Hallin nimi/osoite, värikoodattu ruudukko ja MVP:n sisäänajo-, paikkahavainto-, ulosajo- ja kuittitoiminnot. |
| /admin | Ylläpidon aloitus ja linkit hallien ylläpitoon; nykyinen lukeva ylläpitonäkymä säilytetään tai yhdistetään ilman tietojen katoamista. |
| /admin/garages | Hallilista ja uuden hallin luonti. |
| /admin/garages/{id} | Hallin perustietojen muokkaus. |
| /admin/garages/{id}/layout | Ruudukon maalauseditori. |

Hallilista on käytettävissä myös kartan latauksen epäonnistuessa. Suorat linkit ja SPA-refresh toimivat tuotannon samassa originissa. Puuttuva halli näyttää ymmärrettävän 404-tilan.

## Hallin perustiedot ja kartta

Nimi ja osoite ovat pakollisia luotaessa hallia. Osoite on yksinkertainen tekstikenttä. Sijainti asetetaan kartalta valittavalla pisteellä ja sitä voi muuttaa ennen tallennusta. Osoitteen ja pisteen vastaavuus on ylläpitäjän vastuulla; osoitehakua/geokoodausta ei toteuteta.

Koordinaatit ovat latitude/longitude, molemmat asetettu tai molemmat puuttuvat. Leveysaste -90…90 ja pituusaste -180…180; virheellinen arvo hylätään backendissä. Olemassa olevalla MVP-hallilla saa olla puuttuva osoite/sijainti migraation jälkeen; niitä ei keksitä. Puuttuvalla sijainnilla halli näkyy listalla merkinnällä Sijainti puuttuu mutta ei karttamerkkinä. Uusi halli tarvitsee osoitteen ja pisteen.

Karttamerkki ja listaus näyttävät esimerkiksi **3/5 varattu**. Kokonaismäärä on aktiivisten parkkipaikkojen määrä, varattu on Occupied. Unknown ei lasketa varatuksi tai vapaaksi; se näkyy erikseen, esimerkiksi **3/5 varattu · 1 tuntematon**. MVP:n isFull on edelleen freeLots == 0. Visitien määrästä ei päätellä paikkojen käyttöä.

Hallin nimi, osoite ja käyttötilanne näkyvät karttamerkin yhteydessä; pienen näytön listaus sisältää samat tiedot. Merkki toimii myös näppäimistöltä ja avaa suoraan /garages/{id}. Kartta sovitetaan hallien sijainteihin. Ei käyttäjän paikannusta, ajoreittiohjeita tai sijainnin seurantaa.

Karttakirjasto ja taustakartan toimittaja valitaan toteutuksen designissa. Dokumentoi käyttöehdot, vaaditut attribuutiot, verkkoriippuvuus ja mahdollinen avain. Älä keksi API-avainta, julkista palvelua tai palvelulupausta. Ulkoisen palvelun saatavuus ei saa estää hallilistan tai simulaation käyttöä. Kartta tarvitsee verkkoyhteyden; offline-karttoja ei vaadita.

## Ruudukkopohja ja editori

Ylläpitäjä määrittelee leveyden ja korkeuden. Toteutusraja: kumpikin 1–50 ruutua. Yksi ruutu vastaa yhtä parkkipaikkaa tai yhtä ajoradan osaa. Ruudun tyyppi on Empty, Road tai ParkingLot. Ei suuntaa, fyysisiä mittoja, sisään-/ulosajopisteitä, esteettömyysluokkia tai algoritmista saavutettavuustarkistusta.

Editorissa ovat työkalut Ajorata, Parkkipaikka ja Pyyhekumi sekä Tallenna/Peruuta. Vedon aikana ruutu käsitellään vain kerran. Maalaaminen muuttaa paikallista luonnosta; backendin käyttötilanne ei muutu ennen onnistunutta tallennusta. Tallentamaton poistuminen varoitetaan. Epäonnistunut tallennus säilyttää luonnoksen ja kertoo syyn. Ei erillistä luonnosjulkaisu- tai hyväksyntäjärjestelmää.

Uudet paikat numeroidaan hallikohtaisesti automaattisesti pienimmällä vapaalla positiivisella numerolla, ruudut rivi-/sarakejärjestyksessä. Paikan numero on muokattavissa ja aktiivisten paikkojen numerot ovat hallissa yksikäsitteisiä. Tunniste erotetaan numerosta: olemassa olevaa paikkaa siirrettäessä tunniste, numero ja historia säilyvät. Valitulle olemassa olevalle paikalle voidaan valita uusi tyhjä ruutu; ei vaadita erillistä drag-and-drop-siirtotyökalua.

Pohjan pienentäminen ei poista paikkoja hiljaisesti. Ulkopuolelle jäävät parkkipaikat pitää ensin siirtää tai poistaa nimenomaisesti. Ulkopuolelle jäävä ajorata voidaan poistaa luonnoksesta käyttäjälle näkyvällä vahvistuksella. Road/Empty-ruudut eivät lisää kapasiteettia.

## Paikkojen tila ja historian säilyminen

Paikkanäkymä käyttää tallennettua pohjaa. Vapaa = vihreä, käytössä = punainen, tuntematon = harmaa; numerot ja tilateksti/kuvakkeet täydentävät väriä. Ajorata ja tyhjät ruudut erottuvat paikoista. Ruudun valinta avaa samat paikkasimulaation toiminnot kuin MVP:ssä. Occupied/Unknown-paikkaan ei voi ajaa, mutta vapautumishavainto voidaan simuloida.

- Visit ja Parking säilyvät itsenäisinä; VisitId on edelleen valinnainen.
- Paikan siirtäminen ruudukossa tai numeron muuttaminen ei muuta käyttötilaa tai aktiivista Parkingia.
- Occupied/Unknown-paikkaa tai paikkaa, jolla on aktiivinen Parking, ei saa poistaa tai maalata ajoradaksi/tyhjäksi. Vapauta se ensin simulaatiolla. Tarkista ehto backendissä tallennuksen hetkellä.
- Poistettu vapaa paikka poistuu kapasiteetista ja nykyisestä pohjasta, mutta vanhat Parking-rivit ja niiden paikkaviittaukset säilyvät. Käytä eläkkeelle siirtoa/soft-deleteä, ei historiarivien cascade-poistoa.
- Aktiivisen paikan numero voidaan käyttää uudelleen vasta vanhan paikan poistuttua aktiivisesta joukosta; historiallinen paikkatunniste ei muutu.
- Uusi halli aloittaa tyhjällä pohjalla ja kapasiteetilla 0. Ensimmäinen tallennettu parkkipaikka kasvattaa kapasiteettia. Tyhjä halli on sallittu ylläpidossa, ei virheellinen tietomalli.
- Uusi halli saa MVP:n oletushinnaston. Ajonaikainen hinnastonmuokkaus ei kuulu v2:een; jokaisen hallin Visit käyttää oman hallinsa hinnastoa.

## Tallennus ja tekninen sopimus

Laajenna olemassa olevaa ParkingGaragea osoitteella, nullable-koordinaateilla sekä pohjan mitoilla ja concurrency-versiolla. Tallenna ruutujen tyypit ja paikkasuhteet yhtenä hallikohtaisena pohjana, esimerkiksi tyypitettynä jsonb-rakenteena. ParkingLot pysyy paikan identiteetin ja käyttötilan lähteenä; ruudukko ei saa sisältää erillistä kopioitua CurrentStatus-totuutta. Lisää paikan aktiivisuus/poistumismerkintä ja sovita uniikki-indeksit aktiivisiin paikkoihin. Tarkka EF-mappaus päätetään designissa nykyisestä MVP-koodista.

Pohjan tallennus on yksi atominen operaatio: validoi mitat, koordinaatit, ruututyypit, uniikit numerot ja viittaukset. Yksi paikka saa esiintyä yhdessä ruudussa; ruudun paikan on kuuluttava kyseiseen halliin ja oltava aktiivinen. Uudet paikat syntyvät samassa transaktiossa; uuden paikan pysyvä ID palautetaan vastauksessa. Client ei saa keksiä pysyviä ID:itä.

Tallennus sisältää odotetun layout-version. Kahden editorin vanhentunut tallennus palauttaa 409-konfliktin ilman toisen muutosten ylikirjoitusta. Käyttötilan ja paikan poistamisen kilpailu suojataan samalla halli-ennen-paikkaa-lukitusjärjestyksellä kuin simulaatio; tarkistus ennen transaktiota ei riitä. Virheessä koko muutos peruuntuu ja UI tarjoaa tilanteen uudelleenlatauksen.

Ehdotettu API-sopimus (sovita MVP:n nykyisiin DTO:ihin säilyttäen merkitys):

| Operaatio | Tarkoitus |
| --- | --- |
| POST /api/garages | Luo halli, perustiedot, sijainti ja oletushinnasto. |
| PUT /api/garages/{id} | Muuttaa nimeä, osoitetta ja sijaintia; ei hinnastoa tai käyttötilaa. |
| GET /api/garages | Hallien listaus sisältää sijainnin ja paikkatilojen yhteenvetoluvut. |
| GET /api/garages/{id}/layout | Pohja, versio ja paikkojen ajantasaiset tilat/tunnisteet. |
| PUT /api/garages/{id}/layout | Koko pohjan atominen tallennus odotetulla versiolla. |

Säilytä MVP:n ProblemDetails + code -käytäntö; invalid input 400, tuntematon halli/paikka 404, vanhentunut versio tai käytössä olevan paikan poisto 409. Hallin kokopoisto ei kuulu v2:een.

Migraatio säilyttää nykyisen hallin, paikkojen ID:t/numerot, Visit/Parking-historian ja hinnaston. Alusta olemassa oleville aktiivisille paikoille deterministinen pohja numeron/ID:n järjestyksessä, enintään 50 ruutua/rivi. MVP:n viisi paikkaa voi olla yhdellä rivillä. Älä resetoi tai seedaa nykyisiä rivejä uudelleen. Poikkeava yli 2500 paikan lähtödata raportoidaan, ei typistetä.

## Reaaliaikainen käyttötilanne (SSE)

V2 käyttää Server-Sent Events -yhteyttä. Selain lähettää simulaatiokomennot edelleen tavallisina HTTP-pyyntöinä; SSE välittää palvelimelta tiedon tallennetusta muutoksesta karttaan, listaan ja avoimeen hallinäkymään. SignalR, viestivälittäjä tai erillinen palvelu eivät ole tarpeen.

- Yksi jaettu EventSource-yhteys sovellusta/selaintabia kohden: GET /api/garages/events, Content-Type text/event-stream. Reitin toteutus ei muuta offline OpenAPI-generoinnin sivuvaikutuksettomuutta.
- Lähetä garage-changed vasta onnistuneen tietokantatransaktion commitin jälkeen. Payload sisältää garageId:n ja muutoksen lajin (occupancy/layout/garage). Ei rekisterinumeroita tai käyntien henkilötietoja.
- Tapahtuma on invalidointiviesti, ei laskurille lisättävä delta. Client hakee ajantasaisen halliyhteenvedon ja tarvittaessa avoimen hallin paikkatilat/pohjan. Karttamerkki, lista ja avoin paikkanäkymä päivittyvät samasta totuudesta. Duplikaatti ei kaksinkertaista lukuja.
- Päivitys käynnistyy heti tapahtumasta; lyhyen tapahtumapurkauksen haut saa yhdistää. Vanhentunut/rinnakkainen HTTP-vastaus ei saa korvata uudempaa tilaa. Tilamuutos ei saa hävittää käyttäjän lomakesyötettä, valittua paikkaa tai tallentamatonta editoriluonnosta. Editorille näytetään tarvittaessa pohjan muutoksen ilmoitus ja tallennuksen concurrency-check säilyy.
- Yhteyden avautuessa ja jokaisen uudelleenyhdistämisen jälkeen hae uusi täydellinen tilannekuva: katkon aikana tapahtuneita muutoksia ei tarvitse toistaa tapahtumahistoriasta. Kytke tapahtumakuuntelu ennen alkuhaun valmistumista ja käsittele sen aikana tuleva invalidointi, jotta aloituksen kilpailu ei hukkaa muutosta.
- Näytä Reaaliaikainen / Yhteys katkennut, yhdistetään… / Tilanne voi olla vanhentunut. Säilytä viimeisin data katkon aikana. EventSource yrittää uudelleen; Päivitä-painike toimii varavaihtoehtona. Ei pakollista automaattipollausta tai kestävää tapahtumalokia.
- Toteuta kevyt heartbeat, yhteyden sulkemisen peruutus, tilausten siivous ja rajattu lähetyspuskuri. Hidas/katkennut client ei saa estää simulaatiotransaktiota tai kasvattaa muistia rajattomasti. Tuotannon HTTP-proxy ei saa puskuroida streamia; tarvittavat asetukset dokumentoidaan.
- Julkaise myös paikkamäärän/pohjan/hallin perustietojen tallennuksesta, jotta kartan kokonaismäärä ja hallilista päivittyvät. Pelkkä Visitin sisään-/ulosajo ei muuta varattujen paikkojen määrää.

Pakolliset testit ovat palveluriippumattomia: paikallinen tapahtumajulkaisija/testitupla, API:n stream-kontrakti sekä clientin invalidointi-, duplikaatti-, vanhentuneen vastauksen ja reconnect-resync-testit. Testaa rollbackin julkaisemattomuus ja tilausten siivous. Ei oikean PostgreSQL:n, Dockerin tai selaimen vaatimusta. Kahden oikean selaimen ja tuotantoproxyn läpi tehtävä reaaliaikaisuustarkistus jää myöhemmäksi ihmisen ympäristöhyväksynnäksi.

## Hyväksyntä

- Luo toinen halli, valitse osoite/piste, maalaa ajorata ja paikkoja, tallenna ja lataa uudelleen.
- Hallit ovat erillisiä: numerot voivat toistua eri halleissa, paikka-/käyntiviittaukset eivät ylitä hallirajaa.
- Simuloi käyttötila myös ilman autoa; kartan luvut ja paikkanäkymän värit vastaavat backendin samoja tiloja.
- Testaa Unknown erillisenä lukuna ja nollapaikkainen halli (0/0 varattu).
- Säilytä identiteetti siirrossa; estä käytössä olevan paikan poisto ja aktiivisen paikan leikkaaminen pienennyksessä.
- Testaa editorin stale-version- ja poisto/käyttöönotto-konfliktipolut paikallisesti ja tarkista tuotannon transaktioratkaisu offline. Oikean PostgreSQL:n kilpailut jäävät myöhemmäksi. Historiaviittausten säilyminen katetaan paikallisella palvelu-/mallitestillä.
- Tarkista migraatio/SQL ja historiaviittaukset offline MVP-lähtömallilla, myös aktiivisella Parkingilla. Oikea tietokanta-ajo myöhemmin.
- Karttamerkin klikkaus avaa suoraan oikean hallin. Puuttuva sijainti/kartan verkkovirhe ei estä listan tai simulaation käyttöä.
- Pakolliset backend-testit/build, frontend lint/typecheck/build ja offline OpenAPI. Docker/SPA-refresh oikeassa selaimessa ovat myöhemmän ihmisen tarkistuksia.

Ei ennakkovarauksia, monikerroksisuutta, opastusalgoritmia, oikeita antureita, geokoodausta tai tuotannon pääsynhallintaa. Suorittamatta jäänyt ympäristö-/selainnäyttö raportoidaan rehellisesti eikä merkitä hyväksytyksi.
