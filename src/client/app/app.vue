<template>
  <NuxtPage />
</template>

<style>
:root {
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  color: #17362e;
  background: #f5f6f2;
  font-synthesis: none;
  text-rendering: optimizeLegibility;
  -webkit-font-smoothing: antialiased;
  --ink: #17362e;
  --muted: #69776f;
  --green: #176d50;
  --mint: #e7f3eb;
  --paper: #f5f6f2;
  --line: #dfe5dc;
  --amber: #976417;
  --red: #ad4540;
  --blue: #47718c;
}

* {
  box-sizing: border-box;
}

body {
  min-width: 320px;
  margin: 0;
  background: var(--paper);
}

button,
input,
select {
  font: inherit;
}

button:focus-visible,
a:focus-visible,
input:focus-visible,
select:focus-visible {
  outline: 3px solid #70ae91;
  outline-offset: 2px;
}

.app-shell {
  min-height: 100vh;
}

.sidebar {
  position: fixed;
  z-index: 2;
  inset: 0 auto 0 0;
  display: flex;
  width: 242px;
  flex-direction: column;
  padding: 28px 18px 20px;
  border-right: 1px solid var(--line);
  background: #fff;
}

.brand {
  display: flex;
  align-items: center;
  gap: 11px;
  color: var(--ink);
  font-size: 16px;
  font-weight: 750;
  text-decoration: none;
}

.logo {
  display: grid;
  width: 38px;
  height: 38px;
  place-items: center;
  border-radius: 12px;
  background: var(--green);
  color: white;
  font-size: 24px;
  font-weight: 800;
}

.sidebar-caption {
  margin: 10px 0 34px 49px;
  color: var(--muted);
  font-size: 12px;
}

.sidebar nav {
  display: grid;
  gap: 7px;
}

.sidebar nav button,
.sidebar nav a {
  display: flex;
  min-height: 43px;
  align-items: center;
  gap: 11px;
  padding: 0 12px;
  border: 0;
  border-radius: 9px;
  background: transparent;
  color: #526259;
  cursor: pointer;
  text-align: left;
}

.sidebar nav button[aria-current="page"],
.sidebar nav a[aria-current="page"] {
  background: var(--mint);
  color: var(--green);
  font-weight: 700;
}

.sidebar-bottom {
  display: flex;
  align-items: center;
  gap: 9px;
  margin-top: auto;
  padding: 12px;
  color: var(--muted);
  font-size: 12px;
}

.connection-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--green);
}

.main-content {
  min-height: 100vh;
  margin-left: 242px;
}

.topbar {
  display: flex;
  height: 65px;
  align-items: center;
  justify-content: space-between;
  padding: 0 38px;
  border-bottom: 1px solid var(--line);
  background: rgb(255 255 255 / 78%);
}

.breadcrumb {
  color: var(--muted);
  font-size: 13px;
}

.breadcrumb span {
  margin: 0 8px;
  color: #aab4ad;
}

.breadcrumb strong {
  color: var(--ink);
}

.refresh-button,
.secondary-button,
.primary-button {
  min-height: 40px;
  padding: 0 15px;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 650;
}

.refresh-button,
.secondary-button {
  border: 1px solid var(--line);
  background: white;
  color: var(--ink);
}

.refresh-button:disabled,
.primary-button:disabled,
.secondary-button:disabled {
  cursor: wait;
  opacity: .65;
}

.page-content {
  width: min(1280px, 100%);
  margin: 0 auto;
  padding: 35px 38px 20px;
}

.intro {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 24px;
}

.eyebrow {
  margin: 0 0 7px;
  color: var(--green);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: .16em;
  text-transform: uppercase;
}

.intro h1 {
  margin: 0;
  font-size: clamp(25px, 3vw, 34px);
  letter-spacing: -.045em;
}

.intro p:not(.eyebrow) {
  margin: 8px 0 0;
  color: var(--muted);
}

.garage-badge,
.tag,
.count-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--line);
  border-radius: 999px;
  background: white;
  color: var(--muted);
  font-size: 11px;
  font-weight: 700;
}

.garage-badge {
  padding: 8px 13px;
}

.stats {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 13px;
  margin-bottom: 19px;
}

.stat-card {
  display: flex;
  min-height: 118px;
  flex-direction: column;
  justify-content: center;
  gap: 5px;
  padding: 16px 18px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: white;
}

.stat-card > span:first-child {
  color: var(--muted);
  font-size: 12px;
}

.stat-card strong {
  font-size: 27px;
  letter-spacing: -.04em;
}

.stat-card strong small {
  color: #91a098;
  font-size: 15px;
  font-weight: 500;
}

.stat-card > span:last-child {
  color: #839087;
  font-size: 11px;
}

.stat-primary {
  border-color: #c8e1d1;
  background: #f0f8f2;
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.55fr) minmax(220px, .8fr);
  align-items: start;
  gap: 16px;
}

.panel,
.info-panel,
.state-panel {
  border: 1px solid var(--line);
  border-radius: 13px;
  background: white;
}

.panel {
  padding: 22px;
}

.panel-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.panel-heading h2,
.receipt-panel h2 {
  margin: 0;
  font-size: 19px;
  letter-spacing: -.025em;
}

.panel-heading .eyebrow {
  margin-bottom: 5px;
}

.panel > .muted,
.panel-heading + .muted {
  margin: 0 0 17px;
}

.muted {
  color: var(--muted);
  font-size: 13px;
  line-height: 1.6;
}

.full-message {
  margin: 15px 0;
  padding: 11px 13px;
  border: 1px solid #f0dfb7;
  border-radius: 9px;
  background: #fff8e8;
  color: var(--amber);
  font-size: 13px;
}

.segmented {
  display: flex;
  width: fit-content;
  gap: 3px;
  margin: 20px 0 16px;
  padding: 4px;
  border-radius: 9px;
  background: #f0f2ef;
}

.segmented button {
  min-height: 35px;
  padding: 0 13px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  font-size: 12px;
}

.segmented button.active {
  background: white;
  color: var(--green);
  box-shadow: 0 1px 4px rgb(30 55 42 / 12%);
  font-weight: 700;
}

.gate-form {
  display: grid;
  grid-template-columns: 1fr auto;
  align-items: end;
  gap: 8px 12px;
}

.gate-form label,
.lot-entry-form label {
  grid-column: 1 / -1;
  font-size: 12px;
  font-weight: 700;
}

.gate-form input,
.lot-entry-form select {
  width: 100%;
  min-height: 43px;
  padding: 0 12px;
  border: 1px solid #d6ded7;
  border-radius: 8px;
  background: white;
  color: var(--ink);
}

.primary-button {
  border: 1px solid var(--green);
  background: var(--green);
  color: white;
}

.gate-form .primary-button {
  min-height: 43px;
}

.visit-panel {
  margin-top: 16px;
}

.count-pill {
  min-width: 27px;
  height: 27px;
  padding: 0 8px;
  background: #f4f6f3;
}

.visit-list {
  display: grid;
  margin: 0;
  padding: 0;
  list-style: none;
}

.visit-list li {
  display: grid;
  grid-template-columns: minmax(82px, .8fr) 1fr 1fr;
  align-items: center;
  gap: 10px;
  padding: 11px 0;
  border-top: 1px solid #edf0ed;
  color: var(--muted);
  font-size: 12px;
}

.license-chip {
  width: fit-content;
  padding: 4px 7px;
  border-radius: 5px;
  background: #f0f3ef;
  color: var(--ink);
  font-weight: 750;
}

.info-panel {
  display: flex;
  gap: 12px;
  padding: 17px;
  background: #f8faf7;
}

.info-panel strong {
  font-size: 13px;
}

.info-panel p {
  margin: 6px 0 0;
  color: var(--muted);
  font-size: 12px;
  line-height: 1.6;
}

.info-icon {
  display: grid;
  width: 22px;
  height: 22px;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 50%;
  background: var(--mint);
  color: var(--green);
  font-weight: 800;
}

.notice {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 17px;
  padding: 13px 15px;
  border: 1px solid #c8e1d1;
  border-radius: 10px;
  background: #f0f8f2;
}

.notice-error {
  border-color: #edcbc7;
  background: #fff3f1;
}

.notice-warning {
  border-color: #e9d7ad;
  background: #fff9e9;
}

.notice-warning .notice-icon {
  background: var(--amber);
}

.notice-icon {
  display: grid;
  width: 23px;
  height: 23px;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 50%;
  background: var(--green);
  color: white;
  font-size: 13px;
  font-weight: 800;
}

.notice-error .notice-icon {
  background: var(--red);
}

.notice strong {
  font-size: 13px;
}

.notice p {
  margin: 3px 0 0;
  color: #58675e;
  font-size: 12px;
}

.notice code {
  display: inline-block;
  margin-top: 6px;
  color: #765d35;
  font-size: 11px;
}

.notice .service-note {
  color: var(--red);
  font-weight: 700;
}

.dismiss-button {
  margin-left: auto;
  border: 0;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  font-size: 20px;
}

.lots-panel {
  padding: 24px;
}

.tag {
  padding: 6px 10px;
  background: #f7f8f5;
}

.lot-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
  margin-top: 21px;
}

.lot-card {
  display: flex;
  min-height: 204px;
  flex-direction: column;
  align-items: flex-start;
  padding: 15px;
  border: 1px solid var(--line);
  border-radius: 11px;
  background: #fbfcfa;
}

.lot-Occupied {
  border-color: #f0dfc5;
  background: #fffaf1;
}

.lot-Unknown {
  border-color: #d5e1e9;
  background: #f6fafc;
}

.lot-number {
  margin-bottom: 11px;
  color: var(--muted);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: .1em;
}

.status-pill {
  padding: 4px 9px;
  border-radius: 99px;
  background: #e5f3e9;
  color: var(--green);
  font-size: 11px;
  font-weight: 750;
}

.status-Occupied {
  background: #fff0d9;
  color: var(--amber);
}

.status-Unknown {
  background: #e8f0f5;
  color: var(--blue);
}

.lot-detail {
  margin: 9px 0 14px;
  color: var(--muted);
  font-size: 11px;
  line-height: 1.5;
}

.lot-action {
  width: 100%;
  min-height: 38px;
  margin-top: auto;
  font-size: 11px;
}

.lot-entry-form {
  display: grid;
  width: 100%;
  gap: 7px;
  margin-top: auto;
}

.lot-entry-form label {
  font-size: 10px;
}

.lot-entry-form label span {
  color: var(--muted);
  font-weight: 400;
}

.lot-entry-form select {
  min-height: 37px;
  font-size: 11px;
}

.lot-form-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.lot-form-actions .primary-button {
  min-height: 34px;
  padding: 0 12px;
  font-size: 11px;
}

.text-button {
  border: 0;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  font-size: 11px;
}

.receipt-panel {
  display: flex;
  gap: 18px;
  margin-top: 18px;
  padding: 23px;
  border: 1px solid #c8e1d1;
  border-radius: 13px;
  background: #f5faf6;
}

.receipt-mark {
  display: grid;
  width: 40px;
  height: 40px;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 50%;
  background: var(--green);
  color: white;
  font-size: 20px;
}

.receipt-content {
  width: 100%;
}

.receipt-content > p:not(.eyebrow) {
  margin: 7px 0 17px;
  color: var(--muted);
  font-size: 12px;
}

.receipt-details {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 13px;
  margin: 0;
}

.receipt-details div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.receipt-details dt,
.receipt-total span {
  color: var(--muted);
  font-size: 11px;
}

.receipt-details dd {
  margin: 0;
  font-size: 13px;
  font-weight: 700;
}

.receipt-total {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 17px;
  padding-top: 14px;
  border-top: 1px solid #dcebe0;
}

.receipt-total strong {
  color: var(--green);
  font-size: 22px;
}

.state-panel {
  padding: 24px;
  color: var(--muted);
}

.state-panel p {
  margin: 7px 0 14px;
}

.state-error {
  border-color: #edcbc7;
  color: var(--red);
}

.footer {
  display: flex;
  justify-content: space-between;
  gap: 15px;
  margin-top: 28px;
  padding: 16px 0 6px;
  border-top: 1px solid var(--line);
  color: #8a968e;
  font-size: 10px;
}

@media (max-width: 900px) {
  .sidebar {
    width: 204px;
    padding-right: 12px;
    padding-left: 12px;
  }

  .main-content {
    margin-left: 204px;
  }

  .topbar {
    padding: 0 24px;
  }

  .page-content {
    padding-right: 24px;
    padding-left: 24px;
  }

  .stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .content-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 620px) {
  .sidebar {
    position: sticky;
    inset: 0 0 auto;
    width: 100%;
    height: auto;
    padding: 11px 14px 8px;
    border-right: 0;
    border-bottom: 1px solid var(--line);
  }

  .brand {
    font-size: 14px;
  }

  .logo {
    width: 31px;
    height: 31px;
    font-size: 20px;
  }

  .sidebar-caption,
  .sidebar-bottom {
    display: none;
  }

  .sidebar nav {
    grid-template-columns: repeat(3, 1fr);
    gap: 4px;
    margin-top: 9px;
  }

  .sidebar nav button,
  .sidebar nav a {
    min-height: 36px;
    justify-content: center;
    gap: 5px;
    padding: 0 5px;
    font-size: 10px;
  }

  .main-content {
    margin-left: 0;
  }

  .topbar {
    height: 52px;
    padding: 0 15px;
  }

  .breadcrumb {
    font-size: 10px;
  }

  .refresh-button {
    min-height: 34px;
    padding: 0 9px;
    font-size: 10px;
  }

  .page-content {
    padding: 23px 14px 16px;
  }

  .intro {
    align-items: flex-start;
  }

  .garage-badge {
    display: none;
  }

  .intro h1 {
    font-size: 27px;
  }

  .intro p:not(.eyebrow) {
    font-size: 12px;
  }

  .stats {
    gap: 8px;
  }

  .stat-card {
    min-height: 102px;
    padding: 12px;
  }

  .stat-card strong {
    font-size: 23px;
  }

  .panel {
    padding: 17px;
  }

  .gate-form {
    grid-template-columns: 1fr;
  }

  .gate-form label {
    grid-column: auto;
  }

  .visit-list li {
    grid-template-columns: 1fr 1fr;
  }

  .visit-list li span:last-child {
    grid-column: 1 / -1;
  }

  .lot-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .receipt-panel {
    gap: 11px;
    padding: 16px;
  }

  .receipt-mark {
    width: 32px;
    height: 32px;
  }

  .receipt-details {
    gap: 11px;
  }

  .footer {
    flex-direction: column;
    gap: 5px;
  }
}

.sidebar nav a {
  text-decoration: none;
}

.admin-panel {
  margin-top: 18px;
}

.admin-table-scroll {
  max-width: 100%;
  overflow-x: auto;
}

.admin-table-scroll:focus-visible {
  outline: 3px solid #70ae91;
  outline-offset: 2px;
}

.admin-table-scroll table {
  width: 100%;
  min-width: 540px;
  border-collapse: collapse;
  text-align: left;
  font-size: 13px;
}

.admin-table-scroll caption {
  padding: 8px 0;
  text-align: left;
  color: var(--muted);
}

.admin-table-scroll th,
.admin-table-scroll td {
  padding: 12px 10px;
  border-bottom: 1px solid var(--line);
}
</style>
