<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import GarageSimulation from '../../components/GarageSimulation.vue'
const route = useRoute()
const id = computed(() => {
  const raw = route.params.id
  const value = typeof raw === 'string' && /^[1-9]\d*$/.test(raw) ? Number(raw) : NaN
  return Number.isSafeInteger(value) ? value : null
})
</script>
<template><GarageSimulation :garage-id="id" /></template>
