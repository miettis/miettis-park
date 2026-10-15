<script setup lang="ts">
import type { AvailabilityDto } from '../types/garage'

defineProps<{ availability: AvailabilityDto }>()
</script>

<template>
  <section class="stats" aria-label="Hallin saatavuus">
    <article class="stat-card stat-primary">
      <span>Vapaita paikkoja</span>
      <strong>{{ availability.freeLots }}</strong>
      <span>{{ availability.isFull ? 'Halli on täynnä' : 'Tilaa pysäköidä' }}</span>
    </article>
    <article class="stat-card">
      <span>Varattuja paikkoja</span>
      <strong>{{ availability.occupiedLots }}</strong>
      <span>Paikkahavaintojen perusteella</span>
    </article>
    <article class="stat-card">
      <span>Ei tiedossa</span>
      <strong>{{ availability.unknownLots }}</strong>
      <span>Paikan tila tuntematon</span>
    </article>
    <article class="stat-card">
      <span>Aktiivisia hallikäyntejä</span>
      <strong>{{ availability.activeVisits }}</strong>
      <span>Ei päätellä paikkamäärästä</span>
    </article>
  </section>
</template>
