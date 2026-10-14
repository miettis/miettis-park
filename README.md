# Miettis Park

.NET-, PostgreSQL- ja Vue/Nuxt-pohjainen pysäköintihallisimulaatio rekrytoinnin ennakkotehtävään.

- [SPEC.md](SPEC.md): toiminnallinen ja tekninen sopimus sekä hyväksyntä.
- [STARTING-POINT.md](STARTING-POINT.md): käsin toteutettava lähtöpohja, joka ei kuulu agenttibacklogiin.
- [BACKLOG.md](BACKLOG.md): PullPotentialin työpaketit, riippuvuudet ja ajotapa.
- [AI-USAGE.md](AI-USAGE.md): toteutunut AI:n käyttö ja täydennettävä luovutusyhteenveto.

Tila 14.10.2026: suunnitelma on kirjoitettu. Sovelluskoodia, lähtöpohjaa tai PullPotentialin tietokantaan vietävää backlogia ei ole tässä työvaiheessa toteutettu. Käynnistys- ja testikomennot lisätään toteutuneen projektipohjan perusteella.

Tuotannossa yksi Docker-image palvelee sekä .NET API:n että Nuxtin staattisen frontendin. PostgreSQL on erillinen palvelu. Pakollinen lopputulos on porttisimulaatio; hallintanäkymä on valinnainen.
