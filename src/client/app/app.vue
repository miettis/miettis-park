<template>
  <main>
    <div class="logo" aria-hidden="true">P</div>
    <p class="eyebrow">MIETTIS PARK</p>
    <h1>Pohja on valmis.</h1>
    <p class="description">Pysäköintihallisimulaation toiminnallisuus toteutetaan seuraavaksi.</p>
  </main>
</template>

<style>
:root {
  font-family: system-ui, sans-serif;
  color: #17362e;
  background: #f5f6f2;
}

body {
  margin: 0;
}

main {
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 24px;
  box-sizing: border-box;
  text-align: center;
}

.logo {
  width: 56px;
  height: 56px;
  display: grid;
  place-items: center;
  border-radius: 14px;
  background: #176d50;
  color: white;
  font-size: 36px;
  font-weight: 700;
}

.eyebrow {
  font-size: 11px;
  letter-spacing: 0.2em;
  margin-top: 26px;
}

h1 {
  font-size: clamp(28px, 6vw, 40px);
  letter-spacing: -0.04em;
  margin: 8px 0 12px;
}

.description {
  max-width: 360px;
  color: #69776f;
  line-height: 1.6;
}
</style>
