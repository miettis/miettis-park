<script setup lang="ts">
import { useRoute } from 'vue-router'
import AppSidebar from './AppSidebar.vue'
import { ref, toRef, watch, computed, nextTick, onBeforeUnmount } from 'vue'
import GarageBasicSettings from './GarageBasicSettings.vue'
import GarageLayoutEditor from './GarageLayoutEditor.vue'
import GarageVisitLog from './GarageVisitLog.vue'
import GaragePricing from './GaragePricing.vue'
import GarageLayoutView from './GarageLayoutView.vue'
import AvailabilitySummary from '../components/AvailabilitySummary.vue'
import GatePanel from '../components/GatePanel.vue'
import ImageEntryPanel from '../components/ImageEntryPanel.vue'
import LotCard from '../components/LotCard.vue'
import EventTimeInput from '../components/EventTimeInput.vue'
import { formatUtcDateTime, toEventTime } from '../utils/eventTime'
import { useGarageDashboard } from '../composables/useGarageDashboard'

const props = defineProps<{ garageId: number | null }>()
const selectedLotId = ref<number | null>(null)
const lotDialog = ref<HTMLDialogElement | null>(null)
const lotModalOpen = ref(false)
const selectionNotice = ref('')
const eventTime = ref('')
const eventTimeResult = computed(() => toEventTime(eventTime.value))
const eventTimeValid = computed(() => eventTimeResult.value.error === null)
const { garages, layout, fresh, selectedGarage, availability, visits, receipt, notice, loading, submitting, initialized, error, refresh, enterGarage, enterGarageFromImage, exitGarage, enterLot, exitLot } = useGarageDashboard(undefined, toRef(props, 'garageId'))

function submitEntry(licenseNumber: string): void {
  if (!eventTimeValid.value) return
  const occurredAt = eventTimeResult.value.occurredAt
  void enterGarage(licenseNumber, occurredAt)
}

async function submitImage(file: File): Promise<void> {
  if (!eventTimeValid.value) return
  const occurredAt = eventTimeResult.value.occurredAt
  const garage = props.garageId
  if (await enterGarageFromImage(file, occurredAt) && garage === props.garageId) imageResetKey.value++
}

function submitExit(licenseNumber: string): void {
  if (!eventTimeValid.value) return
  const occurredAt = eventTimeResult.value.occurredAt
  void exitGarage(licenseNumber, occurredAt)
}

function submitLotEntry(lotId: number, visitId: number | null): void {
  if (!eventTimeValid.value) return
  const occurredAt = eventTimeResult.value.occurredAt
  void enterLot(lotId, visitId, occurredAt)
}

function submitLotExit(lotId: number): void {
  if (!eventTimeValid.value) return
  const occurredAt = eventTimeResult.value.occurredAt
  void exitLot(lotId, occurredAt)
}
watch(() => props.garageId, () => { selectedLotId.value = null; selectionNotice.value = ''; toast.value = ''; imageResetKey.value++ })
watch(layout, value => {
  if (selectedLotId.value !== null && !value?.lots.some(lot => lot.id === selectedLotId.value)) {
    selectedLotId.value = null; selectionNotice.value = 'Valittu paikka poistui pohjasta.'
  }
})
const selectedLot = computed(() => availability.value?.lots.find(lot => lot.id === selectedLotId.value) ?? null)
const route = useRoute()
const activePage = ref(route?.query?.tab === 'settings' ? 'settings' : 'overview')
const settingsPage = ref(['details', 'layout', 'pricing'].includes(String(route?.query?.section)) ? String(route?.query?.section) : 'details')
const settingsVisited = ref(activePage.value === 'settings')
const imageResetKey = ref(0)
const toast = ref('')
const toastError = ref(false)
let toastTimer: ReturnType<typeof setTimeout> | undefined
watch(notice, value => {
  if (!value || (value.kind === 'error' && value.code !== 'VISIT_ALREADY_PARKED')) return
  toastError.value = value.kind === 'error'
  toast.value = value.message
  clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toast.value = '' }, 7000)
})
onBeforeUnmount(() => clearTimeout(toastTimer))
watch(activePage, value => { if (value === 'settings') settingsVisited.value = true })
const tabs = [{ id: 'overview', label: 'Tilannekuva' }, { id: 'simulation', label: 'Simulaatio' }, { id: 'log', label: 'Loki' }, { id: 'settings', label: 'Asetukset' }]

function navigateTabs(event: KeyboardEvent): void {
  const target = event.target as HTMLButtonElement
  const buttons = Array.from(target.parentElement?.querySelectorAll<HTMLButtonElement>('[role="tab"]') ?? [])
  const index = buttons.indexOf(target)
  let next: number
  if (event.key === 'ArrowRight') next = (index + 1) % buttons.length
  else if (event.key === 'ArrowLeft') next = (index - 1 + buttons.length) % buttons.length
  else if (event.key === 'Home') next = 0
  else if (event.key === 'End') next = buttons.length - 1
  else return
  event.preventDefault()
  buttons[next]?.click()
  buttons[next]?.focus()
}

function closeLot(): void {
  lotDialog.value?.close?.()
  lotDialog.value?.removeAttribute('open')
  lotModalOpen.value = false
}

watch(selectedLot, lot => { if (!lot) closeLot() })
watch(activePage, page => { if (page !== 'simulation') closeLot() })

async function selectLot(id: number) {
  selectedLotId.value = id
  selectionNotice.value = `Paikka ${selectedLot.value?.number} valittu.`
  toast.value = ''
  lotModalOpen.value = true
  await nextTick()
  if (lotDialog.value && !lotDialog.value.open) {
    if (typeof lotDialog.value.showModal === 'function') lotDialog.value.showModal()
    else lotDialog.value.setAttribute('open', '')
  }
}

function timeLabel(value: string): string {
  return new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value))
}

function durationLabel(value: string): string {
  const match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)(?:\.\d+)?$/.exec(value)
  if (!match) {
    return value
  }
  const hours = Number(match[1] ?? 0) * 24 + Number(match[2])
  const minutes = Number(match[3])
  const seconds = Number(match[4])
  return hours ? `${hours} h ${minutes} min ${seconds} s` : minutes ? `${minutes} min ${seconds} s` : `${seconds} s`
}
function money(value: number, currency: string): string {
  return new Intl.NumberFormat('fi-FI', { style: 'currency', currency }).format(value)
}
</script>

<template>
  <div class="app-shell">
    <AppSidebar :fresh="fresh" :garages="garages" />
    <div v-if="toast && !lotModalOpen" class="success-toast" :class="{ 'error-toast': toastError }" :role="toastError ? 'alert' : 'status'" :aria-live="toastError ? 'assertive' : 'polite'"><span>{{ toast }}</span><button type="button" aria-label="Sulje ilmoitus" @click="toast = ''">×</button></div>

    <main id="main-content" class="main-content">
      <header class="topbar">
        <div class="breadcrumb">Miettis Park <span aria-hidden="true">/</span> <strong>{{ selectedGarage?.name ?? 'Hallin tiedot' }}</strong></div>
        <button class="refresh-button" type="button" :disabled="loading || submitting" @click="refresh">
          {{ loading ? 'Päivitetään…' : '↻  Päivitä tilanne' }}
        </button>
      </header>

      <div class="page-content">
        <section class="intro">
          <div>
            <h1>{{ selectedGarage?.name ?? 'Hallin tiedot' }}</h1>
          </div>
        </section>

        <div v-if="loading && !initialized" class="state-panel" role="status">
          Haetaan hallin tietoja…
        </div>
        <div v-if="error" class="state-panel state-error" role="alert">
          <strong>Tietojen haku epäonnistui</strong>
          <p>{{ error }}</p>
          <button class="secondary-button" type="button" @click="refresh">Yritä uudelleen</button>
        </div>
        <div v-if="initialized && !selectedGarage" class="state-panel" role="status">
          Halleja ei löytynyt.
        </div>
        <template v-if="availability">
          <div class="detail-tabs" role="tablist" aria-label="Hallin tiedot" @keydown="navigateTabs"><button v-for="tab in tabs" :id="`tab-${tab.id}`" :key="tab.id" type="button" role="tab" :tabindex="activePage === tab.id ? 0 : -1" :aria-selected="activePage === tab.id" :aria-controls="`panel-${tab.id}`" @click="activePage = tab.id">{{ tab.label }}</button></div>
          <section v-if="activePage === 'overview'" id="panel-overview" role="tabpanel" aria-labelledby="tab-overview" class="panel"><h2>Paikkoja yhteensä {{ availability.capacity }}</h2><AvailabilitySummary :availability="availability" /><GarageLayoutView v-if="layout" :layout="layout" /></section>
          <GarageVisitLog v-if="activePage === 'log' && props.garageId" :garage-id="props.garageId" />
          <section v-if="settingsVisited && props.garageId" v-show="activePage === 'settings'" id="panel-settings" role="tabpanel" aria-labelledby="tab-settings">
            <div class="detail-tabs" role="tablist" aria-label="Asetukset" @keydown="navigateTabs"><button v-for="tab in [{ id: 'details', label: 'Perustiedot' }, { id: 'layout', label: 'Pohja' }, { id: 'pricing', label: 'Hinnasto' }]" :id="`settings-tab-${tab.id}`" :key="tab.id" type="button" role="tab" :aria-controls="`settings-panel-${tab.id}`" :tabindex="settingsPage === tab.id ? 0 : -1" :aria-selected="settingsPage === tab.id" @click="settingsPage = tab.id">{{ tab.label }}</button></div>
            <div :id="`settings-panel-${settingsPage}`" role="tabpanel" :aria-labelledby="`settings-tab-${settingsPage}`"><KeepAlive><GarageBasicSettings v-if="settingsPage === 'details'" :key="`details-${props.garageId}`" :garage-id="props.garageId" embedded /><GarageLayoutEditor v-else-if="settingsPage === 'layout'" :key="`layout-${props.garageId}`" :garage-id="props.garageId" embedded /><GaragePricing v-else :key="`pricing-${props.garageId}`" :garage-id="props.garageId" /></KeepAlive></div>
          </section>
          <section v-show="activePage === 'simulation'" id="panel-simulation" role="tabpanel" aria-labelledby="tab-simulation">
          <EventTimeInput v-model="eventTime" :disabled="submitting" />
          <div v-if="notice && notice.code !== 'VISIT_ALREADY_PARKED' && (notice.kind === 'error' || notice.kind === 'warning')" class="notice" :class="`notice-${notice.kind}`" :role="'alert'" aria-live="polite">
            <span class="notice-icon" aria-hidden="true">!</span>
            <div>
              <strong>{{ notice.kind === 'error' ? 'Toimintoa ei voitu suorittaa' : notice.kind === 'warning' ? 'Käynnin palautus vaatii huomiota' : 'Tilannepäivitys' }}</strong>
              <p>{{ notice.message }}</p>
              <p v-if="notice.minimumOccurredAt">Aikaisintaan {{ formatUtcDateTime(notice.minimumOccurredAt) }} ({{ notice.minimumOccurredAt }}).</p>
              <code v-if="notice.code">{{ notice.code }}</code>
              <p v-if="notice.gateOpened === false" class="service-note">Porttia ei avattu.</p>
              <p v-if="notice.requiresCustomerService" class="service-note">Ota yhteys asiakaspalveluun.</p>
            </div>
            <button type="button" class="dismiss-button" aria-label="Sulje ilmoitus" @click="notice = null">×</button>
          </div>

          <p>{{ selectedGarage?.address }}</p>

          <section  class="content-grid">
            <div>
              <GatePanel
                :key="props.garageId ?? 'invalid'"
                :submitting="submitting || loading"
                :full="availability.isFull"
                :event-time-valid="eventTimeValid"
                @enter="submitEntry"
                @exit="submitExit"
              />
              <ImageEntryPanel :key="props.garageId ?? 'invalid'" :reset-key="imageResetKey" :occurred-at="eventTimeResult.occurredAt" :submitting="submitting || loading" :event-time-valid="eventTimeValid" @submit="submitImage" />
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

          <section  class="panel lots-panel" aria-labelledby="lots-heading">
            <div class="panel-heading">
              <div><p class="eyebrow">02 / PAIKKAHAVAINNOT</p><h2 id="lots-heading">Paikat ja itsenäiset havainnot.</h2></div>
              <span class="tag">Valitse paikka</span>
            </div>
            <p class="muted">Voit kirjata käytön ilman autoa tai hallikäyntiä. Paikanvaihto tehdään vapauttamalla ensin vanha paikka.</p>
            <GarageLayoutView v-if="layout" :layout="layout" :selected-lot-id="selectedLotId" @select="selectLot" /><p v-if="selectionNotice" role="status">{{ selectionNotice }}</p><dialog ref="lotDialog" class="lot-modal" aria-labelledby="lot-modal-heading" @close="lotModalOpen = false" @cancel="lotModalOpen = false" @click="event => { if (event.target === lotDialog) closeLot() }">
              <header class="lot-modal-header"><h2 id="lot-modal-heading">Paikka {{ selectedLot?.number }}</h2><button type="button" class="secondary-button" aria-label="Sulje paikan toiminnot" @click="closeLot">Sulje</button></header>
              <p>Tapahtuma-aika: {{ eventTimeResult.occurredAt ? formatUtcDateTime(eventTimeResult.occurredAt) : 'Palvelimen nykyhetki' }}</p>
              <p v-if="!eventTimeValid" role="alert">Korjaa tapahtuma-aika simulaation asetuksista ennen paikkatoimintoa.</p>
              <LotCard v-if="lotModalOpen && selectedLot" :key="selectedLot.id" :lot="selectedLot" :visits="visits" :submitting="submitting || loading" :event-time-valid="eventTimeValid" @enter="submitLotEntry" @exit="submitLotExit" />
              <div v-if="toast && lotModalOpen" class="success-toast" :class="{ 'error-toast': toastError }" :role="toastError ? 'alert' : 'status'" :aria-live="toastError ? 'assertive' : 'polite'"><span>{{ toast }}</span><button type="button" aria-label="Sulje ilmoitus" @click="toast = ''">×</button></div>
              <div v-if="notice?.kind === 'error' && notice.code !== 'VISIT_ALREADY_PARKED'" class="notice notice-error" role="alert"><p>{{ notice.message }}</p><p v-if="notice.minimumOccurredAt">Aikaisintaan {{ formatUtcDateTime(notice.minimumOccurredAt) }}.</p><code>{{ notice.code }}</code></div>
            </dialog>
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
                <div><dt>Tapahtuma-aika</dt><dd>{{ timeLabel(receipt.effectiveOccurredAt) }}</dd></div>
                <div><dt>Hallikäynnin kesto</dt><dd>{{ durationLabel(receipt.duration) }}</dd></div>
              </dl>
              <div class="receipt-total"><span>Yhteensä</span><strong>{{ money(receipt.cost, receipt.currency) }}</strong></div>
            </div>
          </section>
          </section>
        </template>

        <footer class="footer"><span>Miettis Park · Pysäköintisimulaatio</span><span>Tiedot haetaan palvelimelta</span></footer>
      </div>
    </main>
  </div>
</template>

<style scoped>
.lot-modal { width: min(480px, calc(100vw - 32px)); max-height: calc(100dvh - 48px); overflow: auto; padding: 24px; border: 1px solid var(--line); border-radius: 14px; background: white; color: var(--ink); box-shadow: 0 18px 60px #0004; }
.lot-modal::backdrop { background: #10251baa; }
.lot-modal-header { display: flex; justify-content: space-between; align-items: center; gap: 16px; }
.lot-modal-header h2 { margin: 0; }
.lot-modal .success-toast { position: sticky; top: 0; margin-top: 16px; max-width: 100%; }
.detail-tabs { display: flex; flex-wrap: wrap; gap: 8px; margin: 0 0 22px; }
.detail-tabs button { padding: 12px 20px; border: 1px solid var(--line); border-radius: 8px; background: white; color: var(--ink); cursor: pointer; }
.detail-tabs button[aria-selected=true] { background: var(--green); color: white; }
.success-toast { position: fixed; top: 20px; right: 20px; max-width: min(520px, calc(100vw - 40px)); z-index: 1000; display: flex; gap: 16px; align-items: center; padding: 18px; background: #176d50; color: white; border-radius: 12px; box-shadow: 0 8px 28px #0003; }
.error-toast { background: #ad4540; }
.success-toast button { border: 0; background: transparent; color: white; font-size: 24px; cursor: pointer; }
</style>
