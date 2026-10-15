<script setup lang="ts">
import AppSidebar from '../../components/AppSidebar.vue'
import AvailabilitySummary from '../../components/AvailabilitySummary.vue'
import { elapsedLabel, timeLabel, useAdminDashboard } from '../../composables/useAdminDashboard'

const { garages, selectedGarage, availability, parkings, visits, updatedAt, loading, initialized, error, refresh } = useAdminDashboard()
</script>

<template>
  <div class="app-shell">
    <AppSidebar :garages="garages" />
    <main id="main-content" class="main-content">
      <header class="topbar">
        <div class="breadcrumb">Miettis Park <span aria-hidden="true">/</span> <strong>Hallinta</strong></div>
        <button class="refresh-button" type="button" :disabled="loading" @click="refresh">
          {{ loading ? 'Päivitetään…' : '↻ Päivitä tilanne' }}
        </button>
      </header>
      <div class="page-content">
        <section class="intro">
          <div>
            <p class="eyebrow">{{ selectedGarage?.name ?? 'Pysäköintihalli' }}</p>
            <h1>Yksi halli. Koko tilanne.</h1>
            <p>Lukeva yhteenveto paikkahavainnoista ja hallikäynneistä.</p>
          </div>
        </section>
        <div v-if="loading" class="state-panel" role="status">
          {{ initialized ? 'Päivitetään hallin tietoja… Edellinen tilanne ei ole ajantasainen.' : 'Haetaan hallin tietoja…' }}
        </div>
        <div v-else-if="error" class="state-panel state-error" role="alert">
          <strong>Tietojen haku epäonnistui</strong>
          <p>{{ error }} Tilannetta ei voida näyttää ajantasaisena.</p>
          <button class="secondary-button" type="button" @click="refresh">Yritä uudelleen</button>
        </div>
        <div v-else-if="initialized && !selectedGarage" class="state-panel" role="status">Halleja ei löytynyt.</div>
        <template v-else-if="availability && updatedAt">
          <p role="status">Päivitetty selaimen ajassa: <time :datetime="updatedAt.toISOString()">{{ timeLabel(updatedAt.toISOString()) }}</time></p>
          <AvailabilitySummary :availability="availability" />
          <aside class="info-panel">
            <div>
              <strong>Kaksi erillistä havaintoa</strong>
              <p>Hallikäynnin päättyminen tai keskeytys ei vapauta paikkaa. Pysäköinnin rekisteriyhteys voi olla historiallinen. Puuttuvasta yhteydestä ei päätellä auton fyysistä sijaintia.</p>
              <p>Endpointit ovat erillisiä tilannekuvia, eivät atominen kokonaiskuva.</p>
            </div>
          </aside>
          <section class="panel admin-panel" aria-labelledby="parkings-heading">
            <div class="panel-heading"><h2 id="parkings-heading">Aktiiviset pysäköinnit</h2></div>
            <p class="muted">Kulunut aika on suuntaa-antava selaimen näyttötieto päivityshetkellä, ei laskutusperuste. Laskutus perustuu palvelimen hallikäyntiin ja kuittiin.</p>
            <p v-if="parkings.length === 0" role="status">Aktiivisia pysäköintejä ei ole.</p>
            <div v-else class="admin-table-scroll" role="region" aria-label="Aktiivisten pysäköintien taulukko, vieritettävä" tabindex="0">
              <table>
                <caption>Aktiiviset pysäköinnit · myös tunnistamattomat</caption>
                <thead><tr><th scope="col">Paikka</th><th scope="col">Rekisteri</th><th scope="col">Pysäköinnin alku</th><th scope="col">Kulunut aika päivityshetkellä</th></tr></thead>
                <tbody>
                  <tr v-for="parking in parkings" :key="parking.parkingId">
                    <th scope="row">Paikka {{ parking.lotNumber }}</th>
                    <td>{{ parking.licenseNumber ?? 'Ei tiedossa' }}</td>
                    <td><time v-if="timeLabel(parking.startAt) !== 'Ei tiedossa'" :datetime="parking.startAt">{{ timeLabel(parking.startAt) }}</time><span v-else>Ei tiedossa</span></td>
                    <td>{{ elapsedLabel(parking.startAt, updatedAt) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
          <section class="panel admin-panel" aria-labelledby="visits-heading">
            <div class="panel-heading"><h2 id="visits-heading">Aktiiviset hallikäynnit</h2></div>
            <p v-if="visits.length === 0" role="status">Aktiivisia hallikäyntejä ei ole.</p>
            <div v-else class="admin-table-scroll" role="region" aria-label="Aktiivisten hallikäyntien taulukko, vieritettävä" tabindex="0">
              <table>
                <caption>Aktiiviset hallikäynnit · erillään pysäköinneistä</caption>
                <thead><tr><th scope="col">Rekisteri</th><th scope="col">Hallikäynnin alku</th><th scope="col">Yhdistetty paikka</th></tr></thead>
                <tbody>
                  <tr v-for="visit in visits" :key="visit.visitId">
                    <th scope="row">{{ visit.licenseNumber }}</th>
                    <td><time v-if="timeLabel(visit.startAt) !== 'Ei tiedossa'" :datetime="visit.startAt">{{ timeLabel(visit.startAt) }}</time><span v-else>Ei tiedossa</span></td>
                    <td>{{ visit.lotNumber === null ? 'Paikka ei tiedossa' : `Paikka ${visit.lotNumber}` }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
        </template>
        <footer class="footer"><span>Miettis Park · Pysäköintisimulaatio</span><span>Ei kirjoittavia hallintatoimintoja</span></footer>
      </div>
    </main>
  </div>
</template>
