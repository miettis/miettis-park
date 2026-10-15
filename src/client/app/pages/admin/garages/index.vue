<script setup lang="ts">
import AppSidebar from '../../../components/AppSidebar.vue'
import { useGarageAdmin } from '../../../composables/useGarageAdmin'

const { garages, loading, initialized, error, refresh } = useGarageAdmin()
</script>

<template>
  <div class="app-shell">
    <AppSidebar :garages="garages" />
    <main id="main-content" class="main-content">
      <header class="topbar"><div class="breadcrumb">Miettis Park <span>/</span> <strong>Halliylläpito</strong></div><div class="actions"><button class="secondary-button" :disabled="loading" @click="refresh">Päivitä lista</button><a class="primary-button" href="/admin/garages/new">Lisää halli</a></div></header>
      <div class="page-content">
        <section class="intro"><div><p class="eyebrow">HALLINTO</p><h1>Hallit</h1><p>Avaa hallin tiedot, tilannekuva ja asetukset.</p></div></section>
        <div v-if="loading && !initialized" class="state-panel" role="status">Haetaan halleja…</div>
        <div v-if="error" class="state-panel state-error" role="alert"><strong>Hallien haku epäonnistui</strong><p>{{ error }}</p><button class="secondary-button" @click="refresh">Yritä uudelleen</button></div>
        <section v-if="initialized && !error && garages.length === 0" class="state-panel" role="status">Halleja ei löytynyt. <a href="/admin/garages/new">Luo ensimmäinen halli</a>.</section>
        <section v-if="garages.length > 0" class="garage-list" aria-label="Halliluettelo">
          <article v-for="garage in garages" :key="garage.id" class="garage-card">
            <div><p class="eyebrow">HALLI #{{ garage.id }}</p><h2>{{ garage.name }}</h2><p>{{ garage.address || 'Osoitetta ei tiedossa' }}</p><p class="muted">{{ garage.capacity }} paikkaa · vapaita {{ garage.freeLots }} · käytössä {{ garage.occupiedLots }} · ei tiedossa {{ garage.unknownLots }}</p><p v-if="garage.latitude === null || garage.longitude === null" class="muted">Sijaintikoordinaatteja ei ole määritetty.</p></div>
            <div class="garage-actions"><a :href="`/garages/${garage.id}`">Hallin tiedot</a></div>
          </article>
        </section>
        <footer class="footer">Miettis Park · Halliylläpito</footer>
      </div>
    </main>
  </div>
</template>

<style scoped>
.actions, .garage-actions { display: flex; align-items: center; gap: 10px; }
.actions a, .garage-actions a { display: inline-flex; min-height: 40px; align-items: center; padding: 0 13px; border: 1px solid var(--line); border-radius: 8px; color: var(--green); font-weight: 650; text-decoration: none; }
.actions .primary-button { border-color: var(--green); color: white; }
.garage-list { display: grid; gap: 12px; }
.garage-card { display: flex; align-items: center; justify-content: space-between; gap: 20px; padding: 22px; border: 1px solid var(--line); border-radius: 13px; background: white; }
.garage-card h2 { margin: 0; }
.garage-card p:not(.eyebrow) { margin: 7px 0; }
.muted { color: var(--muted); font-size: 13px; }
@media (max-width: 680px) { .garage-card { align-items: flex-start; flex-direction: column; } .actions { gap: 5px; } }
</style>
