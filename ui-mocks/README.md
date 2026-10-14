# Käyttöliittymäluonnokset

Avaa `index.html` suoraan selaimessa. Ei buildia, asennusta, verkkoyhteyttä tai ulkoisia kirjastoja/fontteja.

## Kokoelma

- Porttisimulaatio: saapuminen, poistuminen, aktiiviset käynnit ja paikat.
- Paikkatilanne: itsenäiset käyttö-/vapautushavainnot, valinnainen hallikäyntiin yhdistäminen.
- Hallinta: lukeva yhteenveto, myös tunnistamattomat pysäköinnit.
- Kuitti: kesto ja hinta hallikäynnistä.
- Tilaviestit: onnistuminen, täysi halli, Duplicate, puuttuva poistuminen, asiakaspalvelutarve, paikkakonflikti ja yhteysvirhe.

Yläpalkin tilanteet vaihtavat esimerkkidataa. Painikkeet ja lomakkeet ovat toimivia mockup-toimintoja. Päivitä siirtää demoajan minuutilla; esimerkkitilanteen vaihtaminen palauttaa lähtödatan. Paikan käyttö ei tarvitse rekisteriä, täysi halli ei estä sisäänajohavaintoa ja auton ulosajo ei vapauta paikkaa. Mobiilileveys käyttää samaa sisältöä pinottuna.

Tämä on suunnitteluprototyyppi, ei sovelluksen toteutus. Tila on vain muistissa, reload nollaa mockupin. Ei API:a, pysyvää tallennusta, oikeaa porttia/maksamista tai tuotannon transaktio-/virheenkäsittelyä. AI tuotti HTML/CSS/JavaScript-luonnoksen käyttöliittymän arviointia varten. Julkaisuun käytetään varsinaista .NET/Nuxt-toteutusta.


## Suorat näkymälinkit

- [Porttisimulaatio](index.html#gate)
- [Paikkatilanne](index.html#lots)
- [Hallinta](index.html#admin)
- [Kuitti](index.html#receipt)
- [Tilaviestien kokoelma](index.html#states)
- [Täyden hallin tilanne](index.html?scenario=full#gate)
- [Puuttuva sisäänajo](index.html?scenario=error#gate)

[Desktop-esikatselu](preview.png). Mockupin JavaScriptin toimintopolut on tarkistettu erillisellä DOM-stub-testillä; tämä ei ole varsinaisen sovelluksen testinäyttöä.
