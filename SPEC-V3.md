# V3 – rekisterin tunnistus autokuvasta ja tapahtumakohtainen simulaatioaika

14.10.2026. Täydentää [SPEC.md](SPEC.md) ja [SPEC-V2.md](SPEC-V2.md). Työpaketit: [BACKLOG-V3.md](BACKLOG-V3.md). Tämä on suunnitelma, ei toteutus tai PullPotentialiin luotu backlog.

## Sovittu tavoite

1. Käyttäjä lähettää kuvan, jossa näkyy auto ja sen rekisterikilpi. Kun yksi rekisteri tunnistetaan yksiselitteisesti, järjestelmä kirjaa auton suoraan sisään nykyisen sisäänajopalvelun kautta. Ei rekisteriehdotuksen manuaalista hyväksyntävaihetta.
2. Simulaatiossa käyttäjä voi asettaa ajan kullekin sisäänajolle, paikalle ajamiselle, paikalta poistumiselle ja ulosajolle. Eri pysäköintikestoja voidaan kokeilla odottamatta oikean ajan kulumista.

V3 säilyttää .NET/EF/PostgreSQL/Nuxt-pohjan, MapLibre GL JS + OpenFreeMap -kartan, SSE:n, yhden kerroksen sekä erillisen /admin-osion ilman rooleja/kirjautumista. Visit/Parking pysyvät itsenäisinä; rekisterin tunnistus ei valitse tai varaa parkkipaikkaa. Täysi halli ei edelleenkään estä sisäänajohavainnon kirjaamista.

## Hyväksyntä ilman ulkoisia riippuvuuksia

Agentti toteuttaa ja testaa ominaisuudet ilman oikeaa tunnistuspalvelua, API-avainta, PostgreSQL-palvelinta, Dockeria tai selainajuria. Käytä paikallisia service/API-/DOM-testejä, testikelloa, tunnistuspalvelun testituplaa ja stubattua HTTP-vastausta. Normaali build/test ei kutsu maksullista palvelua tai tarvitse salaisia asetuksia.

Tuotannon tunnistusadapteri ja sen virheenkäsittely toteutetaan aidosti. Testituplaa ei käytetä onnistumisen teeskentelyyn runtime-ympäristössä. Ilman konfiguroitua palvelua kuvatoiminto näyttää Palvelu ei ole käytössä; käsin tehtävä simulaatio toimii edelleen. Oikean palvelun tunnistustarkkuus, oikeat kuvat, selain, PostgreSQL ja konttiajo ovat myöhempää ihmisen ympäristöhyväksyntää, eivät agenttityön valmistumisportteja. Kirjaa ne ajamattomiksi.

## 1. Tapahtumakohtainen aika

### UI

Hallin simulaationäkymässä on selkeä **Tapahtuman aika** -kenttä. Se koskee seuraavaksi lähetettävää komentoa, ei muuta koko sovelluksen kelloa. Käyttäjä voi vaihtaa ajan ennen jokaista toimintoa, myös kuvasta tehtävää sisäänajoa.

- Oletus on palvelimen nykyhetki: kenttä tyhjänä käyttää TimeProviderin aikaa. Käyttäjä voi ottaa käyttöön oman päivämäärän/kellonajan sekuntitarkkuudella.
- Näytä käytetty aikavyöhyke/UTC-offset. UI muuntaa paikallisen syötteen yksiselitteiseksi DateTimeOffset-arvoksi; backendille ISO 8601 offsetilla, tallennus UTC:na. Älä tulkitse offsetitonta tekstiä palvelimen paikallisajaksi.
- Näytä ennen toimintoa valittu aika ja vastauksen jälkeen toteutunut tapahtuma-aika. Tarjoa Nykyhetki sekä +10 min, +1 h ja +3 h -pikatoiminnot. Pikatoiminnot muuttavat vain kenttää, eivät lähetä tapahtumaa.
- Lomakkeen aika säilyy SSE-päivityksissä ja käyttötilan päivittyessä. Lähettämisen ajan kenttä lukitaan; kuvapyynnön aika otetaan talteen lähetyshetkellä, ei AI-vastauksen saapuessa.
- Kuitti käyttää Visitin tallennettua StartAt/EndAt-kestoa, ei selaimen seinäkelloa. Aktiivisen käynnin kesto voidaan näyttää valitun simulaatioajan suhteen selvästi nimettynä esikatseluna; negatiivista kestoa ei esitetä todellisena pysäköintiaikana.

Sama kenttä palvelee kaikkia neljää tapahtumaa: EnterParkingGarage, EnterParkingLot, LeaveParkingLot ja LeaveParkingGarage. Hallin perustietojen/pohjan muokkaus ei saa laskutukseen liittyvää tapahtuma-aikaa.

### API ja palvelut

Lisää näiden komentojen DTO:ihin valinnainen occurredAt: DateTimeOffset?. Myös paikan vapautus voi vastaanottaa JSON-bodyn; nykyinen ilman bodya toimiva kutsu säilyy yhteensopivana. Kuvauploadissa sama tieto on multipart-kenttä.

Palvelu ratkaisee yhden effectiveOccurredAt-arvon komennon alussa: occurredAt tai TimeProvider.GetUtcNow(). Kuvapyynnössä arvo ratkaistaan ennen ulkoista kutsua. Välitä se eksplisiittisesti domain-palveluille; älä muuta globaalia TimeProvideria tai palvelimen aikaa. Rinnakkaisten käyttäjien tapahtuma-ajat eivät saa vuotaa toisiinsa.

Tämä muuttaa MVP:n aiempaa sääntöä Asiakas ei määrää laskutusaikaa **vain simulaatiotilassa**. Lisää yksi palvelimen Simulation:Enabled-asetus, demossa true. Kun se on false, käyttäjän lähettämä occurredAt hylätään selkeällä 400 SIMULATION_TIME_DISABLED -vastauksella; ilman aikaa toimiva normaali polku käyttää palvelimen nykyhetkeä. Tämä asetus ei ole käyttöoikeusrooli eikä korvaa tuotannon pääsynhallintaa.

- Menneet ja tulevat ajat sallitaan simulaatiossa; nykyhetkeen nähden asetettua keinotekoista rajaa ei tarvita.
- StartAt, EndAt ja Interrupted-sulkemisen ClosedAt syntyvät effectiveOccurredAt-ajasta. DuplicateEntryWindow lasketaan samasta tapahtuma-ajasta suhteessa alkuperäiseen Visit.StartAt:iin, ei käsittelyn kestosta.
- Muutoksen tekninen tallennus-/lokitusaika, HTTP-timeout, SSE-heartbeat ja palvelun timeout käyttävät edelleen oikeaa kelloa. Erota tapahtuma-aika käsittelyajasta; älä vääristä lokien/auditin aikaleimoja.
- Olemassa olevat käynnit/historia eivät muutu jälkikäteen. Tämä ei ole tapahtumahistorian replay-, takautuvan korjauksen tai aikamatkan tilakone.

### Aikajärjestys ja invariantit

Komento sovelletaan nykyiseen aktiiviseen tilaan. Hylkää aikaisempi tapahtuma, jos se olisi ristiriidassa saman auton/hallin tai saman paikan jo kirjatun aikajärjestyksen kanssa. Aikavirhe palauttaa 409 EVENT_TIME_CONFLICT, kertoo relevantin aikarajan eikä muuta rivejä tai hintaa.

- Visitin ulosajo/keskeytys ei voi olla ennen sen StartAt-aikaa. Uusi saman hallin/normalisoidun rekisterin sisäänajo ei voi sijoittua ennen aiemman kirjatun käynnin loppua/sulkemista tai nykyisen käynnin alkua.
- Parkingin päättyminen ei voi olla ennen sen alkua; uusi käyttö ei voi alkaa ennen saman paikan viimeisimmän käytön loppua/sulkemista.
- Valinnainen VisitId on edelleen nykytilassa aktiivinen ja samasta hallista. Siihen yhdistetty paikan käyttö ei voi alkaa ennen Visit.StartAt-aikaa. Myöhemmin suljettua Visitiä ei avata takautuvasti antamalla vanhaa aikaa.
- Paikan käyttö ja Visitin sulkeminen pysyvät erillisinä. Paikan vapautus ei edellytä aktiivista Visitiä eikä muuta laskutusta; käynnin ulosajo ei vapauta paikkaa.
- Yhtäsuuret peräkkäiset ajat sallitaan; nollakesto maksaa 0. Saman aktiivisen käynnin lähellä toistuva sisäänajo palauttaa Duplicate-tuloksen muuttamatta StartAt-aikaa.
- Ei hallinlaajuista pakkoa kasvaville ajoille: eri autojen ja eri paikkojen testit voivat käyttää omia aikojaan. Samanaikaiset pyynnöt säilyttävät nykyisen transaktio-/lukitusjärjestyksen.

Tarkista aikajärjestys samassa transaktiossa tilamuutoksen kanssa. Hinnoittelun tick-tarkkuuden unit-testit säilyvät; käyttöliittymässä ei tarvita tick-editoria.

## 2. Rekisterikilven tunnistus autokuvasta

### Palveluvalinta ja rajapinta

Ensimmäinen adapteri käyttää **OpenRouteria ja kuvasyötettä tukevaa mallia**. Yksi provider riittää; Azure-adapteria, automaattista provider-valintaa tai kilventunnistusmallin omaa hostausta ei toteuteta. Pidä integraatio ILicensePlateReader-rajapinnan takana.

Malli ja API-avain asetetaan backendin konfiguraatioon/ympäristöön (esim. PlateRecognition:Model ja PlateRecognition:ApiKey); avainta ei lähetetä selaimeen tai tallenneta repoon. Valitse testiä varten eksplisiittisesti yksi kuvia ja rakenteista vastausta tukeva mallin tunniste; mallia ei arvata eikä vaihdeta automaattisesti. Puuttuva konfiguraatio on toiminnon hallittu tila, ei startupin, OpenAPI-generoinnin tai muun simulaation este.

Backend lähettää kuvan tunnistuspalvelulle ja pyytää rajatun rakenteisen vastauksen: vain kuvasta luettavissa olevat rekisteriehdokkaat, ei arvattua puuttuvaa merkkiä. Kuva on dataa; siinä oleva teksti ei ole palvelun ohje. Käytä lyhyttä vastausrajaa ja rajattua timeoutia; ei työkaluja, verkkohakua, keskusteluhistoriaa tai rajatonta retrytä. Providerin JSON/schema-tuki ja kuvan lähetys varmistetaan toteutushetken dokumentaatiosta.

Adapteri validoi vastauksen rakenteen, normalisoi rekisterin nykyisellä Trim + invariant uppercase -säännöllä, hylkää tyhjän/yli 20-merkkisen tuloksen ja yhdistää identtiset ehdokkaat. Se ei poista välimerkkejä, pakota suomalaista rekisteriformaattia tai korjaa O/0/I/1-merkkejä arvaamalla. Mallin ilmoittama confidence ei ole kalibroitu varmuusmittari eikä automaattisen sisäänajon yksinomainen peruste.

### Käyttäjän polku

Hallin simulaatiossa on käsinsyötön rinnalla **Sisäänajo kuvasta**:

1. Valitse kuva tiedostovalitsimella tai raahaa se kuvan lähetysalueelle (drag and drop). Kuvassa näkyy yksi auto ja luettava kilpi. Molemmat tavat käyttävät samaa tiedostovalidointia ja näyttävät paikallisen esikatselun sekä valitun tapahtuma-ajan. Lähetysalue ilmaisee raahauksen kohdistumisen; tiedoston valinta toimii myös näppäimistöllä. Kuvan valinta tai pudotus ei vielä lähetä pyyntöä: lähetys alkaa Lue kilpi ja aja sisään -painikkeesta.
2. Paina Lue kilpi ja aja sisään. Näytä käsittelytila ja estä tahaton rinnakkaislähetys.
3. Kun löytyy yksi yksiselitteinen kelvollinen rekisteri, backend kutsuu nykyistä EnterParkingGarage-palvelua automaattisesti. **Ei erillistä vahvistusnappia tai muokattavaa ehdotusta ennen sisäänajoa.**
4. UI näyttää tunnistetun rekisterin, käytetyn ajan, VisitId:n ja Entry/Duplicate/RecoveredMissingExit-tuloksen. Päivitä käyntinäkymä ja tarvittavat yhteenvedot; paikkojen käyttötila ei muutu tästä.

Automaattisen sisäänajon sääntö: yksi uniikki, kelvollinen ja adapterin mukaan luettavissa oleva rekisteri. Jos tulos on tyhjä, epäselvä, rakenteellisesti virheellinen tai sisältää useita eri rekistereitä, **ei kirjata käyntiä eikä arvata oikeaa autoa**. Näytä selkeä viesti ja tarjoa uusi kuva tai nykyinen käsin tehtävä sisäänajo. Useiden kilpien valintakäyttöliittymää ei tarvita. Tämä yhden auton rajaus on v3:n oletus.

Kuvatoiminto koskee tässä versiossa sisäänajoa. Ulosajo toimii nykyisellä rekisterikentällä ja valittavalla ajalla. Ei jatkuvaa kamerakuvaa, videota, automaattista paikan yhdistämistä tai kuvan jälkikäteistä historian muokkausta. Kilven rajauslaatikko ei ole vaatimus.

### API, tiedostot ja virheet

Ehdotettu endpoint: POST /api/garages/{garageId}/entries/from-image, multipart/form-data: image ja valinnainen occurredAt. Onnistuminen palauttaa saman sisäänajotuloksen kuin käsin tehtävä komento sekä recognizedLicenseNumber/effectiveOccurredAt; ei pelkkää irtonaista tunnistusehdotusta.

- Salli JPEG/PNG, enintään 8 MiB. Validoi koko sekä tiedoston todellinen kuvaformaatti, ei vain tiedostonimi/MIME. Tyhjä, rikkinäinen tai muu formaatti hylätään ennen provider-kutsua. Kuvankäsittelylle rajatut resurssit; ei SVG:tä, mielivaltaista URL-hakua tai pysyvää upload-kansiota.
- Kuvaa käsitellään vain pyynnön ajan ja lähetetään ulkoiselle palvelulle. UI kertoo tämän ennen lähetystä. Älä tallenna kuvaa, base64-sisältöä, API-avainta tai kokonaista provider-vastausta lokeihin. Ulkoisen palvelun omaa säilytyskäytäntöä ei väitetä nollaksi; se dokumentoidaan valitun mallin/providerin mukaan oikean käytön yhteydessä.
- Validoi halli ja simulaatioajan muoto/asetusoikeus ennen maksullista kutsua. Tarkista sisäänajon nykytila/aikajärjestys atomisesti tunnistuksen jälkeen; HTTP/provider-kutsua ei pidetä tietokantatransaktion tai lukon sisällä.
- Tuntematon halli 404; virheellinen kuva/aika 400, liian suuri tiedosto 413; ei kilpeä/epäselvä/useita ehdokkaita 422 selkeillä koodeilla. Provider puuttuu 503, timeout 504 ja virheellinen/upstream-vastaus 502. Palauta nykyistä ProblemDetails-käytäntöä vastaava palaute ilman upstream-salaisuuksia.
- Providerin 402/429/5xx käsitellään hallitusti. Ei automaattista maksullista uudelleenyrityssilmukkaa. Mikään tunnistusvirhe ei kirjoita Visitiä tai Parkingia.
- Normaali Duplicate/recovery-logiikka säilyy myös kuvatunnistuksen jälkeisessä sisäänajossa. Kaksi rinnakkaista kuva-/käsinpyyntöä eivät saa luoda kahta aktiivista Visitiä. Uutta yleistä request-id-palvelua ei lisätä tässä vaiheessa; tietokantainvariantit säilyvät.

## 3. Hyväksyntä

- Tapahtuma-ajan oletus, explicit offset, puuttuva/virheellinen arvo, Simulation:Enabled=false sekä kahden rinnakkaisen komennon erilliset ajat.
- Käynti 0/10 min/3 h/3 h + 1 s/4 h; oikea tallennettu hinta ja ajat ilman odottamista. Duplicate-raja testikellolla myös tarkalla rajalla ja +1 tick palvelutasolla.
- Takaperoinen ulosajo, paikan vapautus, uusi saman rekisterin/paikan käyttö ja Visitin alkua edeltävä linkitys hylätään atomisesti. Eri autojen ajat eivät rajoita toisiaan.
- Tunnistuksen testitupla palauttaa yhden rekisterin: yksi kutsu kirjaa sisään ilman hyväksyntää, käyttää ennen tunnistusta ratkaistua aikaa ja nykyistä sisäänajopalvelua. Myös Duplicate/recovery ja aikakonflikti testataan.
- Ei kilpeä, epäselvä tai kaksi eri rekisteriä, rikkinäinen JSON, rikkinäinen kuva, liian suuri tiedosto, puuttuva konfiguraatio ja providerin virheet: ei käyntiä eikä hintaa; käsinpolku säilyy.
- Kuvan tiedostovalinta ja raahaus lähetysalueelle, esikatselu/käsittely/palaute, tapahtuma-ajan kenttä/pikatoiminnot ja SSE-päivityksen aikainen syötteen säilyminen katetaan paikallisilla DOM-/API-testeillä.
- Normaali palveluriippumaton testiajo ei tarvitse provider-avainta, maksullista kutsua, oikeaa PostgreSQL:ää, Dockeria tai selainta. Mallin tarkkuutta ei väitetä validoiduksi testituplalla.
- Nykyisen revision backend-build/backend-tests/frontend-checks ja offline OpenAPI/migraatio-SQL/mallin tarkistus. Tarkemmat runtime-/kuva-/selain-/konttikokeet dokumentoidaan myöhemmiksi ihmisen tarkistuksiksi.

## Rajauksen ulkopuolella

Manuaalinen hyväksyntä onnistuneelle yksiselitteiselle kuvatunnistukselle, usean auton valintatyökalu, videokamera, kilven rajauseditori, kuvan pysyvä arkistointi, OCR-mallin koulutus/hostaus, Azure-adapteri, automaattinen provider-fallback, takautuva historiankorjaus, käyttöoikeusroolit ja tuotannon automaattisen portin turvallisuushyväksyntä. Kuvamalli voi erehtyä: demo ei takaa virheetöntä fyysisen portin automaatiota.
