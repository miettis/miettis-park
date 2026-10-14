# V2-käyttöliittymäluonnokset

Avaa [index.html](index.html) suoraan selaimessa. Ei buildia, palvelinta, kartta-API:a, verkkoyhteyttä tai ulkoisia kirjastoja. Tämä on [SPEC-V2.md](../../SPEC-V2.md):n suunnitteluprototyyppi, ei Nuxt-sovellus tai backend-toteutus.

## Näkymät

- [Hallit kartalla](index.html#map): varattu/yhteensä, Unknown erikseen, haku ja listavaihtoehto. Karttamerkki avaa suoraan hallin.
- [Paikkanäkymä](index.html#garage/1): värit, paikan valinta ja itsenäiset paikkahavainnot. Hallikäyntien määrä ei määrää paikkojen tilaa.
- [Ylläpito](index.html#admin): hallilista ja linkit perustietoihin/pohjaan.
- [Uusi halli](index.html#form/new): nimi, osoite ja kartalta valittava havainnepiste.
- [Hallin perustiedot](index.html#form/1).
- [Maalauseditori](index.html#edit/1): ajorata, paikka, pyyhekumi; maalaa vetämällä tai aktivoi ruutu näppäimistöllä. Valitse/siirrä säilyttää paikan tunnisteen ja tilan. Numeron muokkaus, koon muutos, tallennus/peruutus.

## Kokeiltavat polut

1. Avaa halli karttamerkistä. Valitse vapaa paikka ja simuloi käyttö; paluu kartalle näyttää uuden luvun. Rekisteri on valinnainen lisätieto.
2. Vaihda paikka tuntemattomaksi; Unknown ei kasvata varattujen määrää. Ulosajohavainto muuttaa vain hallikäyntien lukumäärää.
3. Ylläpito → Uusi halli → täytä tiedot ja napsauta karttaa → luo → maalaa paikkoja ja ajorataa → tallenna → kartalle.
4. Editorissa käytössä/Unknown-paikan pyyhkiminen estetään. Vapaan paikan pyyhkiminen pienentää kapasiteettia tallennettaessa. Pohjan pienennys ei leikkaa paikkoja.
5. Valitse/siirrä: klikkaa paikkaa, sitten tyhjää ruutua. Tunniste, numero ja tila säilyvät.
6. Simuloi karttavirhe; hallilista toimii edelleen. Neljännellä esimerkkihallilla ei ole sijaintia tai paikkoja.

Kartta on itse piirretty havainne, ei oikea Helsingin kartta. Koordinaatit ovat esimerkkilaskentaa. Tiedot säilyvät vain avoimen sivun muistissa; reload nollaa ne. Ei oikeaa tallennusta, transaktioita, kilpailusuojausta tai migraatioita. Hallikäynnit ovat tässä vain lukumääräsimulaatio: portin ja kuitin tarkempi mock on [MVP-kokoelmassa](../index.html#gate).

Ylläpito erotellaan omaksi osioksi; rooleja tai kirjautumista ei ole. Oikean sovelluksen hyväksyntää ei voi päätellä tämän mockin toiminnasta.

## Tarkistukset

JavaScriptin syntaksi ja rajatut toimintopolut tarkistettu Node/DOM-stub-ajolla: node ui-mocks/v2/verify.cjs repojuuresta. Headless Chromella tarkistettu desktop-kartta ja editori: [kartta](preview-map.png), [editori](preview-editor.png). Stub-testi ei korvaa kaikkien hiiri-/kosketus-/näppäimistöpolkujen selaintestausta.

## Reaaliaikaisuuden simulaatio

Kartta ja paikkanäkymä saavat simuloidun muutoksen kuuden sekunnin välein. Simuloi muutos lähettää muutoksen heti; Katkaise yhteys pysäyttää sen ja Yhdistä uudelleen päivittää tilannekuvan. Editorin tai lomakkeen sisältöä ei korvata taustapäivityksillä. Tämä on paikallinen SSE-käyttäytymisen havainne, ei oikea EventSource/backend-yhteys; kaikki esimerkkidata nollautuu reloadissa.
