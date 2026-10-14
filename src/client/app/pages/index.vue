<script setup lang="ts">
import { ref } from 'vue'
import AvailabilitySummary from '../components/AvailabilitySummary.vue'
import GatePanel from '../components/GatePanel.vue'
import LotCard from '../components/LotCard.vue'
import { useGarageDashboard } from '../composables/useGarageDashboard'

const { selectedGarage, availability, visits, receipt, notice, loading, submitting, initialized, error, refresh, enterGarage, exitGarage, enterLot, exitLot } = useGarageDashboard()
const activePage = ref<'gate' | 'lots'>('gate')
const title = {
  gate: 'Tervetuloa. Aja peremmälle.',
  lots: 'Tilanne näkyy paikalla.'
}
const descriptions = {
  gate: 'Kirjaa hallikäynti ja simuloi paikkojen käyttöä erikseen.',
  lots: 'Paikkahavainnot ovat itsenäisiä, ja auton tunnistaminen on valinnaista.'
}

function timeLabel(value: string): string {
  return new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}

function durationLabel(value: string): string {
  const match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)(?:\.\d+)?$/.exec(value)
  if (!match) {
    return value
  }
  const hours = Number(match[1] ?? 0) * 24 + Number(match[2])
  const minutes = Number(match[3])
  const seconds = Number(match[4])
  return hours ? `${hours} h ${minutes} min` : minutes ? `${minutes} min` : `${seconds} s`
}
function money(value: number, currency: string): string {
  return new Intl.NumberFormat('fi-FI', { style: 'currency', currency }).format(value)
}
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar">
      <a class="brand" href="#main-content" aria-label="Miettis Park etusivu" @click="activePage = 'gate'">
        <span class="logo" aria-hidden="true">P</span>
        <span>Miettis Park</span>
      </a>
      <p class="sidebar-caption">Tilaa saapua.</p>
      <nav aria-label="Päänavigaatio">
        <button type="button" :aria-current="activePage === 'gate' ? 'page' : undefined" @click="activePage = 'gate'">
          <span aria-hidden="true">↗</span> Porttisimulaatio
        </button>
        <button type="button" :aria-current="activePage === 'lots' ? 'page' : undefined" @click="activePage = 'lots'">
          <span aria-hidden="true">▦</span> Paikkatilanne
        </button>
        <a href="/admin">▦ Hallinta</a>
      </nav>
      <div class="sidebar-bottom">
        <span class="connection-dot" aria-hidden="true" />
        Paikallinen simulaatio
      </div>
    </aside>

    <main id="main-content" class="main-content">
      <header class="topbar">
        <div class="breadcrumb">Miettis Park <span aria-hidden="true">/</span> <strong>{{ activePage === 'gate' ? 'Porttisimulaatio' : 'Paikkatilanne' }}</strong></div>
        <button class="refresh-button" type="button" :disabled="loading || submitting" @click="refresh">
          {{ loading ? 'Päivitetään…' : '↻  Päivitä tilanne' }}
        </button>
      </header>

      <div class="page-content">
        <section class="intro">
          <div>
            <p class="eyebrow">{{ selectedGarage?.name ?? 'Pysäköintihalli' }}</p>
            <h1>{{ title[activePage] }}</h1>
            <p>{{ descriptions[activePage] }}</p>
          </div>
          <span class="garage-badge">{{ selectedGarage?.name ?? 'Hallin tilanne' }}</span>
        </section>

        <div v-if="loading && !initialized" class="state-panel" role="status">
          Haetaan hallin tietoja…
        </div>
        <div v-else-if="error" class="state-panel state-error" role="alert">
          <strong>Tietojen haku epäonnistui</strong>
          <p>{{ error }}</p>
          <button class="secondary-button" type="button" @click="refresh">Yritä uudelleen</button>
        </div>
        <div v-else-if="initialized && !selectedGarage" class="state-panel" role="status">
          Halleja ei löytynyt.
        </div>
        <template v-else-if="availability">
          <div v-if="notice" class="notice" :class="`notice-${notice.kind}`" :role="notice.kind === 'error' || notice.kind === 'warning' ? 'alert' : 'status'" aria-live="polite">
            <span class="notice-icon" aria-hidden="true">{{ notice.kind === 'error' || notice.kind === 'warning' ? '!' : notice.kind === 'success' ? '✓' : 'i' }}</span>
            <div>
              <strong>{{ notice.kind === 'error' ? 'Toimintoa ei voitu suorittaa' : notice.kind === 'warning' ? 'Käynnin palautus vaatii huomiota' : 'Tilannepäivitys' }}</strong>
              <p>{{ notice.message }}</p>
              <code v-if="notice.code">{{ notice.code }}</code>
              <p v-if="notice.gateOpened === false" class="service-note">Porttia ei avattu.</p>
              <p v-if="notice.requiresCustomerService" class="service-note">Ota yhteys asiakaspalveluun.</p>
            </div>
            <button type="button" class="dismiss-button" aria-label="Sulje ilmoitus" @click="notice = null">×</button>
          </div>

          <AvailabilitySummary :availability="availability" />

          <section v-if="activePage === 'gate'" class="content-grid">
            <div>
              <GatePanel
                :submitting="submitting || loading"
                :full="availability.isFull"
                @enter="enterGarage"
                @exit="exitGarage"
              />
              <section class="panel visit-panel" aria-labelledby="visits-heading">
                <div class="panel-heading">
                  <div><p class="eyebrow">AKTIIVISET</p><h2 id="visits-heading">Hallikäynnit</h2></div>
                  <span class="count-pill">{{ visits.length }}</span>
                </div>
                <p v-if="visits.length === 0" class="muted">Aktiivisia hallikäyntejä ei ole.</p>
                <ul v-else class="visit-list">
                  <li v-for="visit in visits" :key="visit.visitId">
                    <span class="license-chip">{{ visit.licenseNumber }}</span>
                    <span>{{ timeLabel(visit.startAt) }}</span>
                    <span>{{ visit.lotNumber ? `Paikka ${String(visit.lotNumber).padStart(2, '0')}` : 'Paikka ei tiedossa' }}</span>
                  </li>
                </ul>
              </section>
            </div>
            <aside class="info-panel">
              <span class="info-icon" aria-hidden="true">i</span>
              <div><strong>Kaksi erillistä havaintoa</strong><p>Hallikäynnin kirjaaminen ei muuta paikkojen tilaa. Ulosajo ei vapauta paikkaa automaattisesti.</p></div>
            </aside>
          </section>

          <section v-else-if="activePage === 'lots'" class="panel lots-panel" aria-labelledby="lots-heading">
            <div class="panel-heading">
              <div><p class="eyebrow">02 / PAIKKAHAVAINNOT</p><h2 id="lots-heading">Viisi paikkaa. Itsenäiset havainnot.</h2></div>
              <span class="tag">Valitse vapaa paikka</span>
            </div>
            <p class="muted">Voit kirjata käytön ilman autoa tai hallikäyntiä. Paikanvaihto tehdään vapauttamalla ensin vanha paikka.</p>
            <div class="lot-grid">
              <LotCard
                v-for="lot in availability.lots"
                :key="lot.id"
                :lot="lot"
                :visits="visits"
                :submitting="submitting || loading"
                @enter="enterLot"
                @exit="exitLot"
              />
            </div>
          </section>

          <section v-if="receipt" class="receipt-panel" aria-labelledby="receipt-heading">
            <div class="receipt-mark" aria-hidden="true">✓</div>
            <div class="receipt-content">
              <p class="eyebrow">PALVELIMEN KUITTI</p>
              <h2 id="receipt-heading">Hyvää matkaa!</h2>
              <p>Hallikäynti on päättynyt. Ulosajo ei muuta paikkahavaintoja.</p>
              <dl class="receipt-details">
                <div><dt>Rekisterinumero</dt><dd>{{ receipt.licenseNumber }}</dd></div>
                <div><dt>Saapuminen</dt><dd>{{ timeLabel(receipt.startAt) }}</dd></div>
                <div><dt>Poistuminen</dt><dd>{{ timeLabel(receipt.endAt) }}</dd></div>
                <div><dt>Hallikäynnin kesto</dt><dd>{{ durationLabel(receipt.duration) }}</dd></div>
              </dl>
              <div class="receipt-total"><span>Yhteensä</span><strong>{{ money(receipt.cost, receipt.currency) }}</strong></div>
            </div>
          </section>
        </template>

        <footer class="footer"><span>Miettis Park · Pysäköintisimulaatio</span><span>Tiedot haetaan palvelimelta</span></footer>
      </div>
    </main>
  </div>
</template>
