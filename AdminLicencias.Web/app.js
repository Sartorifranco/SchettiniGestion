const STORAGE_KEY = "schpos-license-panel-v1";
const DEFAULT_API_BASE = "https://licencias.schpos.com.ar";

const $ = (id) => document.getElementById(id);

let historialCache = [];
let syncingDates = false;

function loadConfig() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) || "{}");
  } catch {
    return {};
  }
}

function saveConfig(partial) {
  const cfg = { ...loadConfig(), ...partial };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(cfg));
  return cfg;
}

function getApiBase() {
  const cfg = loadConfig();
  return (cfg.apiBaseUrl || DEFAULT_API_BASE).replace(/\/+$/, "");
}

function getAdminApiKey() {
  return loadConfig().adminApiKey || "";
}

function showToast(message) {
  $("toastBody").textContent = message;
  bootstrap.Toast.getOrCreateInstance($("toast")).show();
}

function formatDate(value) {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleDateString("es-AR");
}

function formatMoney(value) {
  return new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
    maximumFractionDigits: 0
  }).format(Number(value) || 0);
}

function badgeEstado(estado) {
  const key = (estado || "").toLowerCase();
  let cls = "bg-secondary";
  if (key === "activa") cls = "badge-estado-activa";
  else if (key === "porvencer" || key === "por_vencer") cls = "badge-estado-porvencer";
  else if (key === "vencida") cls = "badge-estado-vencida";
  return `<span class="badge ${cls}">${estado || "—"}</span>`;
}

function tomorrowIso() {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

function addDaysIso(days) {
  const d = new Date();
  d.setDate(d.getDate() + Math.max(1, Number(days) || 365));
  return d.toISOString().slice(0, 10);
}

function diffDaysFromToday(isoDate) {
  const target = new Date(isoDate + "T00:00:00");
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((target - today) / 86400000);
}

function initDateFields() {
  $("fechaVencimiento").min = tomorrowIso();
  $("fechaVencimiento").value = addDaysIso(365);
}

function syncFromDias() {
  if (syncingDates) return;
  syncingDates = true;
  $("fechaVencimiento").value = addDaysIso($("dias").value);
  syncingDates = false;
}

function syncFromFecha() {
  if (syncingDates) return;
  syncingDates = true;
  const dias = diffDaysFromToday($("fechaVencimiento").value);
  $("dias").value = Math.max(1, dias);
  syncingDates = false;
}

async function apiFetch(path, options = {}) {
  const apiKey = getAdminApiKey();
  if (!apiKey) {
    throw new Error("Configurá tu API Key de administración en el engranaje superior.");
  }

  const headers = {
    "Content-Type": "application/json",
    "X-Api-Key": apiKey,
    ...(options.headers || {})
  };

  const response = await fetch(`${getApiBase()}${path}`, {
    ...options,
    headers
  });

  let payload = null;
  const text = await response.text();
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = { raw: text };
    }
  }

  if (!response.ok) {
    const msg = payload?.error || payload?.title || payload?.raw || `HTTP ${response.status}`;
    throw new Error(msg);
  }

  return payload;
}

function validateGenerateForm() {
  const hwid = $("hardwareId").value.trim();
  const cuit = $("cuit").value.trim();
  const razon = $("razonSocial").value.trim();
  const fecha = $("fechaVencimiento").value;

  if (!razon) return "La Razón Social es obligatoria.";
  if (!cuit) return "El CUIT es obligatorio.";
  if (!hwid) return "El Hardware ID es obligatorio.";
  if (!fecha) return "La fecha de vencimiento es obligatoria.";
  if (fecha <= new Date().toISOString().slice(0, 10)) {
    return "La fecha de vencimiento debe ser futura.";
  }
  return null;
}

function showGenerateError(message) {
  $("resultadoVacio").classList.add("d-none");
  $("resultadoOk").classList.add("d-none");
  $("resultadoError").textContent = message;
  $("resultadoError").classList.remove("d-none");
}

function showGenerateSuccess(data) {
  $("resultadoVacio").classList.add("d-none");
  $("resultadoError").classList.add("d-none");
  $("resultadoOk").classList.remove("d-none");
  $("resultadoMensaje").textContent = "Licencia generada correctamente.";
  $("licenseKey").value = data.licenseKey || "";
  $("resPlan").textContent = (data.plan || "").toUpperCase();
  $("resVence").textContent = formatDate(data.fechaVencimiento);
  $("resModulos").textContent = data.modulosResumen || (data.modulos || []).join(", ");
}

async function onGenerateSubmit(event) {
  event.preventDefault();

  const error = validateGenerateForm();
  if (error) {
    showGenerateError(error);
    return;
  }

  $("btnGenerar").disabled = true;
  $("spinnerGenerar").classList.remove("d-none");
  showGenerateError("");

  const body = {
    hardwareId: $("hardwareId").value.trim().toUpperCase(),
    cuit: $("cuit").value.trim(),
    razonSocial: $("razonSocial").value.trim(),
    plan: $("plan").value,
    fechaVencimiento: $("fechaVencimiento").value,
    montoVenta: Number($("monto").value) || 0,
    metodoPago: $("metodoPago").value,
    versionSchpos: $("versionSchpos").value.trim() || "2.0.8",
    esRenovacion: $("esRenovacion").checked,
    observaciones: $("observaciones").value.trim()
  };

  try {
    const data = await apiFetch("/api/licenses/generate", {
      method: "POST",
      body: JSON.stringify(body)
    });
    showGenerateSuccess(data);
    showToast("Licencia generada.");
  } catch (err) {
    showGenerateError(err.message || "Error al generar la licencia.");
  } finally {
    $("btnGenerar").disabled = false;
    $("spinnerGenerar").classList.add("d-none");
  }
}

function renderHistorial(rows) {
  const tbody = $("historialBody");
  tbody.innerHTML = "";

  if (!rows.length) {
    tbody.innerHTML = '<tr><td colspan="8" class="text-secondary text-center py-4">Sin registros.</td></tr>';
    $("historialResumen").textContent = "0 registro(s)";
    return;
  }

  let total = 0;
  for (const row of rows) {
    total += Number(row.montoVenta) || 0;
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${row.razonSocial || "—"}</td>
      <td class="font-monospace">${row.cuit || "—"}</td>
      <td>${(row.plan || "").toUpperCase()}</td>
      <td>${formatDate(row.fechaEmision)}</td>
      <td>${formatDate(row.fechaVencimiento)}</td>
      <td>${badgeEstado(row.estado)}</td>
      <td>${formatMoney(row.montoVenta)}</td>
      <td class="font-monospace small">${row.hwid || "—"}</td>
    `;
    tbody.appendChild(tr);
  }

  $("historialResumen").textContent =
    `${rows.length} registro(s) | Total facturado en filtro: ${formatMoney(total)}`;
}

function applyHistorialFilters() {
  const q = $("filtroTexto").value.trim().toLowerCase();
  const estado = $("filtroEstado").value;

  const filtered = historialCache.filter((row) => {
    const matchText = !q || [
      row.razonSocial,
      row.cuit,
      row.hwid
    ].some((v) => (v || "").toLowerCase().includes(q));

    const matchEstado = !estado || (row.estado || "") === estado;
    return matchText && matchEstado;
  });

  renderHistorial(filtered);
}

async function loadHistorial() {
  $("historialBody").innerHTML =
    '<tr><td colspan="8" class="text-secondary text-center py-4">Cargando…</td></tr>';

  try {
    historialCache = await apiFetch("/api/licenses/history");
    applyHistorialFilters();
    showToast("Historial actualizado.");
  } catch (err) {
    $("historialBody").innerHTML =
      `<tr><td colspan="8" class="text-danger text-center py-4">${err.message}</td></tr>`;
    $("historialResumen").textContent = "";
  }
}

function initConfigModal() {
  const cfg = loadConfig();
  $("apiBaseUrl").value = cfg.apiBaseUrl || DEFAULT_API_BASE;
  $("adminApiKey").value = cfg.adminApiKey || "";
}

function onSaveConfig() {
  saveConfig({
    apiBaseUrl: $("apiBaseUrl").value.trim() || DEFAULT_API_BASE,
    adminApiKey: $("adminApiKey").value.trim()
  });
  bootstrap.Modal.getInstance($("configModal")).hide();
  showToast("Configuración guardada.");
}

document.addEventListener("DOMContentLoaded", () => {
  initDateFields();
  initConfigModal();

  $("dias").addEventListener("input", syncFromDias);
  $("fechaVencimiento").addEventListener("change", syncFromFecha);
  $("formGenerar").addEventListener("submit", onGenerateSubmit);
  $("btnCopiar").addEventListener("click", async () => {
    const key = $("licenseKey").value;
    if (!key) return;
    await navigator.clipboard.writeText(key);
    showToast("Clave copiada al portapapeles.");
  });
  $("btnRefrescarHistorial").addEventListener("click", loadHistorial);
  $("filtroTexto").addEventListener("input", applyHistorialFilters);
  $("filtroEstado").addEventListener("change", applyHistorialFilters);
  $("btnGuardarConfig").addEventListener("click", onSaveConfig);
  $("tab-historial").addEventListener("shown.bs.tab", () => {
    if (!historialCache.length) loadHistorial();
  });

  if (!getAdminApiKey()) {
    setTimeout(() => {
      bootstrap.Modal.getOrCreateInstance($("configModal")).show();
    }, 400);
  }
});
