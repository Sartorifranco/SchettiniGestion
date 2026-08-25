const STORAGE_KEY = "schpos-license-panel-v2";
const DEFAULT_API_BASE = "https://licencias.schpos.com.ar";

const GRUPO_TITULOS = {
  lite_base: "Paquete Lite (base)",
  modulo_adicional: "Módulos adicionales",
  extra_unico: "Extras únicos",
  abono_mensual: "Abonos mensuales"
};

const $ = (id) => document.getElementById(id);

let historialCache = [];
let auditoriaCache = [];
let clientesCache = [];
let modulosCatalog = [];
let syncingDates = false;
let selectedClienteId = null;
let sessionUser = null;

function loadConfig() {
  try { return JSON.parse(localStorage.getItem(STORAGE_KEY) || "{}"); }
  catch { return {}; }
}

function saveConfig(partial) {
  const prev = loadConfig();
  // Migración: nunca persistir API keys
  const { adminApiKey, ...safePrev } = prev;
  const cfg = { ...safePrev, ...partial };
  delete cfg.adminApiKey;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(cfg));
  return cfg;
}

function getApiBase() {
  return (loadConfig().apiBaseUrl || DEFAULT_API_BASE).replace(/\/+$/, "");
}

function getUserIdentifier() {
  return sessionUser || loadConfig().userIdentifier || "";
}

function showToast(message) {
  $("toastBody").textContent = message;
  bootstrap.Toast.getOrCreateInstance($("toast")).show();
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
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
  else if (key === "vencida" || key === "revocada") cls = "badge-estado-vencida";
  return `<span class="badge ${cls}">${escapeHtml(estado || "—")}</span>`;
}

function toLocalIsoDate(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function todayIso() {
  return toLocalIsoDate(new Date());
}

function tomorrowIso() {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return toLocalIsoDate(d);
}

function addDaysIso(days) {
  const d = new Date();
  d.setDate(d.getDate() + Math.max(1, Number(days) || 365));
  return toLocalIsoDate(d);
}

function diffDaysFromToday(isoDate) {
  const target = new Date(isoDate + "T00:00:00");
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((target - today) / 86400000);
}

function normalizeCuit(value) {
  return (value || "").replace(/\D/g, "");
}

function isValidCuit(value) {
  const cuit = normalizeCuit(value);
  return cuit.length === 11;
}

function formatDateTime(value) {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString("es-AR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function showApp(user) {
  sessionUser = user;
  $("loginGate").classList.add("app-hidden");
  $("appShell").classList.remove("app-hidden");
  if ($("chipUsuario")) $("chipUsuario").textContent = user;
  if ($("loginUser")) $("loginUser").value = user;
  if ($("userIdentifier")) $("userIdentifier").value = user;
}

function showLogin(message) {
  sessionUser = null;
  $("appShell").classList.add("app-hidden");
  $("loginGate").classList.remove("app-hidden");
  const err = $("loginError");
  if (message) {
    err.textContent = message;
    err.classList.add("show");
  } else {
    err.textContent = "";
    err.classList.remove("show");
  }
}

async function apiFetch(path, options = {}) {
  const response = await fetch(`${getApiBase()}${path}`, {
    ...options,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    }
  });

  let payload = null;
  const text = await response.text();
  if (text) {
    try { payload = JSON.parse(text); }
    catch { payload = { raw: text }; }
  }

  if (response.status === 401 && !path.startsWith("/api/auth/")) {
    showLogin("Sesión expirada. Volvé a ingresar.");
    throw new Error("Sesión expirada.");
  }

  if (!response.ok) {
    console.error("[SCHPOS API]", path, response.status, text);
    const msg = payload?.error
      || payload?.detail
      || payload?.title
      || payload?.raw
      || `HTTP ${response.status}`;
    throw new Error(msg);
  }

  if (response.status === 204) return null;
  return payload;
}

async function tryRestoreSession() {
  try {
    const me = await apiFetch("/api/auth/me");
    if (me?.authenticated && me.userIdentifier) {
      showApp(me.userIdentifier);
      return true;
    }
  } catch {
    /* sin sesión */
  }
  return false;
}

async function onLoginSubmit(e) {
  e.preventDefault();
  const user = ($("loginUser").value || "").trim();
  const key = ($("loginApiKey").value || "").trim();
  const err = $("loginError");
  err.classList.remove("show");

  if (!user || !key) {
    err.textContent = "Completá identificador y API key.";
    err.classList.add("show");
    return;
  }

  try {
    $("btnLogin").disabled = true;
    await fetch(`${getApiBase()}/api/auth/login`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ adminApiKey: key, userIdentifier: user })
    }).then(async (res) => {
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data.error || "No se pudo iniciar sesión.");
      return data;
    });

    saveConfig({ userIdentifier: user });
    $("loginApiKey").value = "";
    showApp(user);
    await afterLoginLoad();
  } catch (ex) {
    err.textContent = ex.message || "Login fallido.";
    err.classList.add("show");
  } finally {
    $("btnLogin").disabled = false;
  }
}

async function onLogout() {
  try {
    await fetch(`${getApiBase()}/api/auth/logout`, {
      method: "POST",
      credentials: "include"
    });
  } catch { /* ignore */ }
  showLogin();
}

async function afterLoginLoad() {
  await Promise.all([
    loadModulosCatalog().catch(() => {}),
    loadDashboard().catch(() => {})
  ]);
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
  $("dias").value = Math.max(1, diffDaysFromToday($("fechaVencimiento").value));
  syncingDates = false;
}

function validateGenerateForm() {
  const razon = $("razonSocial").value.trim();
  const cuit = $("cuit").value.trim();
  const hwid = $("hardwareId").value.trim();
  const fecha = $("fechaVencimiento").value;

  if (!razon) return "La Razón Social es obligatoria.";
  if (!cuit) return "El CUIT es obligatorio.";
  if (!isValidCuit(cuit)) return "El CUIT debe tener 11 dígitos.";
  if (!hwid) return "El Hardware ID es obligatorio.";
  if (!fecha) return "La fecha de vencimiento es obligatoria.";
  if (fecha <= todayIso()) return "La fecha de vencimiento debe ser futura.";

  const plan = $("plan").value;
  if (plan === "custom" && getSelectedModulos().length === 0) {
    return "Seleccioná al menos un módulo o cambiá el plan base.";
  }

  return null;
}

function showGenerateError(message) {
  $("resultadoVacio").classList.add("d-none");
  $("resultadoOk").classList.add("d-none");
  $("resultadoError").textContent = message;
  $("resultadoError").classList.remove("d-none");
}

function showGenerateSuccess(data, formSnapshot) {
  $("resultadoVacio").classList.add("d-none");
  $("resultadoError").classList.add("d-none");
  $("resultadoOk").classList.remove("d-none");
  $("resultadoMensaje").textContent = "Licencia generada y guardada en el servidor.";
  $("licenseKey").value = data.licenseKey || "";
  $("resCliente").textContent = formSnapshot.razonSocial;
  $("resHwid").textContent = formSnapshot.hardwareId;
  $("resPlan").textContent = (data.plan || formSnapshot.plan || "").toUpperCase();
  $("resVence").textContent = `${formatDate(data.fechaVencimiento)} (${diffDaysFromToday(String(data.fechaVencimiento).slice(0, 10))} días)`;
  $("resModulos").textContent = data.modulosResumen || (data.modulos || []).join(", ");
  $("resMontoLicencia").textContent = formatMoney(formSnapshot.montoLicencia);
  $("resAbonoMensual").textContent = formatMoney(formSnapshot.abonoMensual);
}

function getSelectedModulos() {
  return [...document.querySelectorAll(".modulo-check:checked")].map((el) => el.value);
}

function renderModulosPanel() {
  const panel = $("panelModulos");
  panel.innerHTML = "";
  const grupos = [...new Set(modulosCatalog.map((m) => m.grupo))];

  for (const grupo of grupos) {
    const mods = modulosCatalog.filter((m) => m.grupo === grupo);
    if (!mods.length) continue;

    const title = document.createElement("div");
    title.className = "fw-semibold mt-2 mb-1";
    title.textContent = GRUPO_TITULOS[grupo] || grupo;
    panel.appendChild(title);

    const row = document.createElement("div");
    row.className = "row g-2";
    for (const mod of mods) {
      const col = document.createElement("div");
      col.className = "col-md-6";
      const titulo = mod.esAbonoMensual ? `${mod.nombre} (abono)` : mod.nombre;
      const desc = (mod.descripcion || "").trim();
      col.innerHTML = `
        <div class="form-check">
          <input class="form-check-input modulo-check" type="checkbox" value="${escapeHtml(mod.codigo)}" id="mod_${escapeHtml(mod.codigo)}">
          <label class="form-check-label" for="mod_${escapeHtml(mod.codigo)}"${desc ? ` title="${escapeHtml(desc)}"` : ""}>
            ${escapeHtml(titulo)}
            ${desc ? `<div class="small text-secondary mt-1">${escapeHtml(desc)}</div>` : ""}
          </label>
        </div>`;
      row.appendChild(col);
    }
    panel.appendChild(row);
  }

  onPlanChange();
}

function setModulosChecked(codigos) {
  const set = new Set((codigos || []).map((c) => c.toUpperCase()));
  document.querySelectorAll(".modulo-check").forEach((el) => {
    el.checked = set.has(el.value.toUpperCase());
  });
}

function applyPresetLite() {
  const lite = modulosCatalog.filter((m) => m.incluidoEnLite).map((m) => m.codigo);
  setModulosChecked(lite);
}

function applyPresetPro() {
  const pro = modulosCatalog
    .filter((m) => m.grupo === "lite_base" || m.grupo === "modulo_adicional")
    .map((m) => m.codigo);
  setModulosChecked(pro);
}

function clearModulos() {
  document.querySelectorAll(".modulo-check").forEach((el) => { el.checked = false; });
}

function onPlanChange() {
  const plan = $("plan").value;
  if (plan === "pro") applyPresetPro();
  else if (plan === "lite") applyPresetLite();
  // custom: sin cambios, el usuario elige manualmente
}

async function loadModulosCatalog() {
  try {
    modulosCatalog = await apiFetch("/api/licenses/modules");
    renderModulosPanel();
    $("modulosImplicitos").textContent =
      "Los módulos implícitos del sistema se agregan automáticamente en el servidor.";
  } catch (err) {
    $("panelModulos").innerHTML = `<div class="text-danger">${escapeHtml(err.message)}</div>`;
  }
}

function fillClienteSelect(selectedId = null) {
  const select = $("clienteSelect");
  const current = selectedId || select.value;
  select.innerHTML = '<option value="">— Cliente nuevo / manual —</option>';
  for (const c of clientesCache) {
    const opt = document.createElement("option");
    opt.value = c.id;
    opt.textContent = `${c.razonSocial} (${c.cuit || "sin CUIT"})`;
    select.appendChild(opt);
  }
  if (current) select.value = current;
}

function onClienteSelectChange() {
  const id = $("clienteSelect").value;
  selectedClienteId = id || null;
  if (!id) return;

  const cliente = clientesCache.find((c) => c.id === id);
  if (!cliente) return;

  $("razonSocial").value = cliente.razonSocial || "";
  $("cuit").value = cliente.cuit || "";
  if (cliente.ultimoHwid) {
    $("hardwareId").value = cliente.ultimoHwid.trim().toUpperCase();
  }
}

async function loadClientes() {
  clientesCache = await apiFetch("/api/licenses/clients");
  fillClienteSelect(selectedClienteId);
  renderClientesTable(clientesCache);
  $("clientesResumen").textContent = `${clientesCache.length} cliente(s) activo(s)`;
}

function renderClientesTable(rows) {
  const q = ($("filtroClientes")?.value || "").trim().toLowerCase();
  const filtered = rows.filter((c) => !q || [
    c.razonSocial, c.cuit, c.ciudad, c.contacto, c.ipServidor
  ].some((v) => (v || "").toLowerCase().includes(q)));

  const tbody = $("clientesBody");
  tbody.innerHTML = "";
  if (!filtered.length) {
    tbody.innerHTML = '<tr><td colspan="8" class="text-secondary text-center py-4">Sin clientes.</td></tr>';
    return;
  }

  for (const c of filtered) {
    const ipLabel = c.ipServidor
      ? `${c.ipServidor}${c.puertoServidor ? `:${c.puertoServidor}` : ""}`
      : "—";
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${escapeHtml(c.razonSocial)}</td>
      <td class="font-monospace">${escapeHtml(c.cuit || "—")}</td>
      <td>${escapeHtml(c.ciudad || "—")}</td>
      <td>${escapeHtml(c.contacto || "—")}</td>
      <td class="font-monospace small">${escapeHtml(ipLabel)}</td>
      <td class="font-monospace small">${escapeHtml(c.ultimoHwid || "—")}</td>
      <td>${badgeEstado(c.ultimoEstado)}</td>
      <td class="text-end">
        <div class="btn-group btn-group-sm">
          <button type="button" class="btn btn-outline-primary btn-usar-cliente" data-id="${c.id}">Usar</button>
          <button type="button" class="btn btn-outline-secondary btn-editar-cliente" data-id="${c.id}">Editar</button>
          <button type="button" class="btn btn-outline-danger btn-eliminar-cliente" data-id="${c.id}">Eliminar</button>
        </div>
      </td>`;
    tbody.appendChild(tr);
  }

  tbody.querySelectorAll(".btn-usar-cliente").forEach((btn) => {
    btn.addEventListener("click", () => {
      $("clienteSelect").value = btn.dataset.id;
      onClienteSelectChange();
      bootstrap.Tab.getOrCreateInstance($("tab-generar")).show();
      showToast("Cliente cargado en el formulario de licencia.");
    });
  });

  tbody.querySelectorAll(".btn-editar-cliente").forEach((btn) => {
    btn.addEventListener("click", () => openClienteModal(btn.dataset.id));
  });

  tbody.querySelectorAll(".btn-eliminar-cliente").forEach((btn) => {
    btn.addEventListener("click", () => {
      const cliente = clientesCache.find((c) => c.id === btn.dataset.id);
      deleteCliente(btn.dataset.id, cliente?.razonSocial);
    });
  });
}

function clearClienteFormError() {
  $("clienteFormError").classList.add("d-none");
  $("clienteFormError").textContent = "";
}

function showClienteFormError(message) {
  $("clienteFormError").textContent = message;
  $("clienteFormError").classList.remove("d-none");
}

function resetClienteForm() {
  $("formCliente").reset();
  $("clienteEditId").value = "";
  $("cliPuerto").value = "1433";
  $("cliPuestos").value = "1";
  $("cliActivo").checked = true;
  clearClienteFormError();
}

function fillClienteForm(data) {
  $("clienteEditId").value = data.id || "";
  $("cliRazonSocial").value = data.razonSocial || "";
  $("cliCuit").value = data.cuit || "";
  $("cliContacto").value = data.contacto || "";
  $("cliTelefono").value = data.telefono || "";
  $("cliEmail").value = data.email || "";
  $("cliCanal").value = data.canalContacto || "WhatsApp";
  $("cliCiudad").value = data.ciudad || "";
  $("cliProvincia").value = data.provincia || "";
  $("cliIpServidor").value = data.ipServidor || "";
  $("cliPuerto").value = data.puertoServidor || 1433;
  $("cliPuestos").value = data.cantidadPuestos || 1;
  $("cliNotas").value = data.notas || "";
  $("cliActivo").checked = data.activo !== false;
}

function readClienteForm() {
  return {
    razonSocial: $("cliRazonSocial").value.trim(),
    cuit: normalizeCuit($("cliCuit").value),
    contacto: $("cliContacto").value.trim(),
    telefono: $("cliTelefono").value.trim(),
    email: $("cliEmail").value.trim(),
    ciudad: $("cliCiudad").value.trim(),
    provincia: $("cliProvincia").value.trim(),
    ipServidor: $("cliIpServidor").value.trim(),
    puertoServidor: Number($("cliPuerto").value) || 1433,
    cantidadPuestos: Number($("cliPuestos").value) || 1,
    canalContacto: $("cliCanal").value,
    notas: $("cliNotas").value.trim(),
    activo: $("cliActivo").checked
  };
}

function validateClienteForm(body) {
  if (!body.razonSocial) return "La Razón Social es obligatoria.";
  if (body.cuit && !isValidCuit(body.cuit)) return "El CUIT debe tener 11 dígitos.";
  if (body.puertoServidor < 1 || body.puertoServidor > 65535) return "Puerto inválido.";
  if (body.cantidadPuestos < 1) return "Cantidad de puestos inválida.";
  return null;
}

async function openClienteModal(clienteId = null) {
  resetClienteForm();
  $("clienteModalTitle").textContent = clienteId ? "Editar cliente" : "Nuevo cliente";

  if (clienteId) {
    const data = await apiFetch(`/api/licenses/clients/${clienteId}`);
    fillClienteForm(data);
  }

  bootstrap.Modal.getOrCreateInstance($("clienteModal")).show();
}

async function saveCliente(event) {
  event.preventDefault();
  clearClienteFormError();

  const body = readClienteForm();
  const error = validateClienteForm(body);
  if (error) {
    showClienteFormError(error);
    return;
  }

  const id = $("clienteEditId").value;
  $("btnGuardarCliente").disabled = true;

  try {
    if (id) {
      await apiFetch(`/api/licenses/clients/${id}`, {
        method: "PUT",
        body: JSON.stringify(body)
      });
      showToast("Cliente actualizado.");
    } else {
      await apiFetch("/api/licenses/clients", {
        method: "POST",
        body: JSON.stringify(body)
      });
      showToast("Cliente creado.");
    }

    bootstrap.Modal.getInstance($("clienteModal")).hide();
    await loadClientes();
  } catch (err) {
    showClienteFormError(err.message || "Error al guardar el cliente.");
  } finally {
    $("btnGuardarCliente").disabled = false;
  }
}

async function deleteCliente(id, nombre) {
  const label = nombre || "este cliente";
  if (!confirm(`¿Eliminar ${label}? También se borrarán sus licencias del historial.`)) return;

  try {
    await apiFetch(`/api/licenses/clients/${id}`, { method: "DELETE" });
    showToast("Cliente eliminado.");
    if ($("clienteSelect").value === id) {
      $("clienteSelect").value = "";
      selectedClienteId = null;
    }
    await loadClientes();
    historialCache = [];
  } catch (err) {
    showToast(err.message || "Error al eliminar.");
  }
}

async function revokeLicencia(licenciaId, clienteNombre) {
  const label = clienteNombre || "esta licencia";
  if (!confirm(`¿Revocar la licencia de ${label}? Quedará vencida desde hoy.`)) return;

  try {
    await apiFetch("/api/licenses/revoke", {
      method: "POST",
      body: JSON.stringify({ licenciaId })
    });
    showToast("Licencia revocada.");
    await loadHistorial();
    await loadClientes();
    await loadDashboard().catch(() => {});
  } catch (err) {
    showToast(err.message || "Error al revocar.");
  }
}

async function loadDashboard() {
  const data = await apiFetch("/api/licenses/dashboard");
  $("kpiActivos").textContent = data.clientesActivos ?? "0";
  $("kpiPorVencer").textContent = data.clientesPorVencer ?? "0";
  $("kpiVencidos").textContent = data.clientesVencidos ?? "0";
  $("kpiTotal").textContent = data.totalClientes ?? "0";
  $("kpiInstalacionesTotal").textContent = formatMoney(data.ingresosInstalacionesTotal);
  $("kpiInstalacionesMes").textContent = formatMoney(data.ingresosInstalacionesMes);
  $("kpiInstalacionesAnio").textContent = formatMoney(data.ingresosInstalacionesAnio);
  $("kpiAbonosRecurrentes").textContent = formatMoney(data.ingresoRecurrenteAbonos);

  const tbody = $("dashboardProximosBody");
  tbody.innerHTML = "";
  const rows = data.proximosAVencer || [];
  if (!rows.length) {
    tbody.innerHTML = '<tr><td colspan="6" class="text-secondary text-center py-3">Sin clientes próximos a vencer.</td></tr>';
    return;
  }

  for (const row of rows) {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${escapeHtml(row.razonSocial)}</td>
      <td class="font-monospace">${escapeHtml(row.cuit || "—")}</td>
      <td>${escapeHtml(row.ciudad || "—")}</td>
      <td>${formatDate(row.vencimiento)}</td>
      <td>${row.diasRestantes ?? "—"}</td>
      <td>${badgeEstado(row.estado)}</td>`;
    tbody.appendChild(tr);
  }
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

  const planValue = $("plan").value;
  const formSnapshot = {
    razonSocial: $("razonSocial").value.trim(),
    hardwareId: $("hardwareId").value.trim().toUpperCase(),
    plan: planValue === "custom" ? "lite" : planValue,
    montoLicencia: Number($("montoLicencia").value) || 0,
    abonoMensual: Number($("abonoMensual").value) || 0
  };

  const body = {
    hardwareId: formSnapshot.hardwareId,
    cuit: normalizeCuit($("cuit").value),
    razonSocial: formSnapshot.razonSocial,
    plan: formSnapshot.plan,
    fechaVencimiento: $("fechaVencimiento").value,
    montoLicencia: formSnapshot.montoLicencia,
    abonoMensual: formSnapshot.abonoMensual,
    versionSchpos: $("versionSchpos").value.trim() || "2.4.0",
    esRenovacion: $("esRenovacion").checked,
    observaciones: $("observaciones").value.trim()
  };

  if (selectedClienteId) body.clienteId = selectedClienteId;
  if (planValue === "custom") body.modulos = getSelectedModulos();

  try {
    const data = await apiFetch("/api/licenses/generate", {
      method: "POST",
      body: JSON.stringify(body)
    });
    showGenerateSuccess(data, formSnapshot);
    showToast("Licencia generada.");
    await loadClientes();
    historialCache = [];
  } catch (err) {
    showGenerateError(err.message || "Error al generar la licencia.");
  } finally {
    $("btnGenerar").disabled = false;
    $("spinnerGenerar").classList.add("d-none");
  }
}

function getFilteredHistorial() {
  const q = $("filtroTexto").value.trim().toLowerCase();
  const estado = $("filtroEstado").value;
  const desde = $("filtroDesde").value;
  const hasta = $("filtroHasta").value;

  return historialCache.filter((row) => {
    const matchText = !q || [row.razonSocial, row.cuit, row.hwid]
      .some((v) => (v || "").toLowerCase().includes(q));
    const matchEstado = !estado || (row.estado || "") === estado;

    const emision = String(row.fechaEmision || "").slice(0, 10);
    const matchDesde = !desde || emision >= desde;
    const matchHasta = !hasta || emision <= hasta;

    return matchText && matchEstado && matchDesde && matchHasta;
  });
}

function renderHistorial(rows) {
  const tbody = $("historialBody");
  tbody.innerHTML = "";

  if (!rows.length) {
    tbody.innerHTML = '<tr><td colspan="13" class="text-secondary text-center py-4">Sin registros.</td></tr>';
    $("historialResumen").textContent = "0 registro(s)";
    return;
  }

  let totalLicencia = 0;
  let totalAbono = 0;
  for (const row of rows) {
    totalLicencia += Number(row.montoLicencia) || 0;
    totalAbono += Number(row.abonoMensual) || 0;
    const puedeRevocar = row.estado === "Activa" || row.estado === "PorVencer";
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${escapeHtml(row.razonSocial || "—")}</td>
      <td class="font-monospace">${escapeHtml(row.cuit || "—")}</td>
      <td>${escapeHtml(row.versionSchpos || "—")}</td>
      <td>${formatDate(row.fechaEmision)}</td>
      <td>${formatDate(row.fechaVencimiento)}</td>
      <td>${row.diasRestantes ?? "—"}</td>
      <td>${badgeEstado(row.estado)}</td>
      <td>${formatMoney(row.montoLicencia)}</td>
      <td>${formatMoney(row.abonoMensual)}</td>
      <td class="font-monospace small">${escapeHtml(row.hwid || "—")}</td>
      <td>${row.esRenovacion ? "Sí" : "No"}</td>
      <td class="small">${escapeHtml(row.modulosResumen || "—")}</td>
      <td class="text-end">
        ${puedeRevocar
          ? `<button type="button" class="btn btn-outline-danger btn-sm btn-revocar-licencia" data-id="${row.licenciaId}" data-cliente="${escapeHtml(row.razonSocial || "")}">Revocar</button>`
          : `<span class="text-secondary small">—</span>`}
      </td>`;
    tbody.appendChild(tr);
  }

  tbody.querySelectorAll(".btn-revocar-licencia").forEach((btn) => {
    btn.addEventListener("click", () => revokeLicencia(btn.dataset.id, btn.dataset.cliente));
  });

  $("historialResumen").textContent =
    `${rows.length} registro(s) | Instalaciones en filtro: ${formatMoney(totalLicencia)} | Abonos/mes en filtro: ${formatMoney(totalAbono)}`;
}

function applyHistorialFilters() {
  renderHistorial(getFilteredHistorial());
}

async function loadHistorial() {
  $("historialBody").innerHTML =
    '<tr><td colspan="13" class="text-secondary text-center py-4">Cargando…</td></tr>';
  historialCache = await apiFetch("/api/licenses/history");
  applyHistorialFilters();
  showToast("Historial actualizado.");
}

function csvEscape(value) {
  return `"${String(value ?? "").replace(/"/g, '""')}"`;
}

function exportHistorialCsv() {
  const rows = getFilteredHistorial();
  if (!rows.length) {
    showToast("No hay filas para exportar.");
    return;
  }

  const header = [
    "Razón Social", "CUIT", "Versión", "Fecha Emisión", "Fecha Vencimiento",
    "Días Restantes", "Estado", "Módulos", "Monto Licencia", "Abono Mensual", "HWID", "Notas"
  ];

  const lines = [header.join(";")];
  for (const row of rows) {
    lines.push([
      csvEscape(row.razonSocial),
      csvEscape(row.cuit),
      csvEscape(row.versionSchpos),
      csvEscape(formatDate(row.fechaEmision)),
      csvEscape(formatDate(row.fechaVencimiento)),
      csvEscape(row.diasRestantes),
      csvEscape(row.estado),
      csvEscape(row.modulosResumen),
      csvEscape(Number(row.montoLicencia || 0).toFixed(2)),
      csvEscape(Number(row.abonoMensual || 0).toFixed(2)),
      csvEscape(row.hwid),
      csvEscape(row.observaciones)
    ].join(";"));
  }

  const blob = new Blob(["\uFEFF" + lines.join("\n")], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `historial_licencias_${todayIso().replace(/-/g, "")}.csv`;
  a.click();
  URL.revokeObjectURL(url);
  showToast("CSV exportado.");
}

function initConfigModal() {
  const cfg = loadConfig();
  $("apiBaseUrl").value = cfg.apiBaseUrl || DEFAULT_API_BASE;
  $("userIdentifier").value = cfg.userIdentifier || sessionUser || "";
  if ($("loginUser") && !$("loginUser").value)
    $("loginUser").value = cfg.userIdentifier || "";
}

function onSaveConfig() {
  saveConfig({
    apiBaseUrl: $("apiBaseUrl").value.trim() || DEFAULT_API_BASE,
    userIdentifier: $("userIdentifier").value.trim() || sessionUser || ""
  });
  bootstrap.Modal.getInstance($("configModal")).hide();
  showToast("Ajustes guardados.");
}

function getFilteredAuditoria() {
  const usuario = $("filtroAuditoriaUsuario").value.trim().toLowerCase();
  const accion = $("filtroAuditoriaAccion").value.trim().toLowerCase();
  const ip = $("filtroAuditoriaIp").value.trim().toLowerCase();

  return auditoriaCache.filter((row) => {
    const matchUsuario = !usuario || (row.usuario || "").toLowerCase().includes(usuario);
    const matchAccion = !accion || (row.accion || "").toLowerCase().includes(accion);
    const matchIp = !ip || (row.ip || "").toLowerCase().includes(ip);
    return matchUsuario && matchAccion && matchIp;
  });
}

function renderAuditoria(rows) {
  const tbody = $("auditoriaBody");
  tbody.innerHTML = "";

  if (!rows.length) {
    const msg = auditoriaCache.length
      ? "Sin registros que coincidan con los filtros."
      : "Sin registros de auditoría.";
    tbody.innerHTML = `<tr><td colspan="5" class="text-secondary text-center py-4">${msg}</td></tr>`;
    $("auditoriaResumen").textContent = auditoriaCache.length
      ? `0 registro(s) mostrado(s) de ${auditoriaCache.length}`
      : "0 registro(s)";
    return;
  }

  for (const row of rows) {
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td class="text-nowrap small">${formatDateTime(row.fecha)}</td>
      <td><span class="badge bg-secondary-subtle text-light fw-normal">${escapeHtml(row.usuario || "Desconocido")}</span></td>
      <td><span class="audit-action">${escapeHtml(row.accion || "—")}</span></td>
      <td class="font-monospace small">${escapeHtml(row.ip || "—")}</td>
      <td class="small text-secondary">${escapeHtml(row.navegador || "—")}</td>`;
    tbody.appendChild(tr);
  }

  const total = auditoriaCache.length;
  $("auditoriaResumen").textContent = rows.length === total
    ? `${rows.length} registro(s) mostrado(s)`
    : `${rows.length} registro(s) mostrado(s) de ${total}`;
}

function applyAuditoriaFilters() {
  renderAuditoria(getFilteredAuditoria());
}

async function loadAuditoria() {
  $("auditoriaBody").innerHTML =
    '<tr><td colspan="5" class="text-secondary text-center py-4">Cargando…</td></tr>';

  try {
    auditoriaCache = await apiFetch("/api/licenses/audit");
    applyAuditoriaFilters();
    showToast("Auditoría actualizada.");
  } catch (err) {
    auditoriaCache = [];
    $("auditoriaBody").innerHTML =
      `<tr><td colspan="5" class="text-danger text-center py-4">${escapeHtml(err.message)}</td></tr>`;
    $("auditoriaResumen").textContent = "";
  }
}

async function loadActualizaciones() {
  const list = $("actualizacionesList");
  if (!list) return;
  list.innerHTML = '<div class="text-secondary text-center py-4">Cargando…</div>';

  try {
    const base = (window.location.origin || "").replace(/\/+$/, "") || getApiBase();
    const url = `${base}/actualizaciones.json?t=${Date.now()}`;
    const res = await fetch(url, { cache: "no-store" });
    if (!res.ok) throw new Error(`No se pudo cargar actualizaciones (${res.status})`);
    const data = await res.json();

    if ($("actVersionActual")) $("actVersionActual").textContent = data.versionActual || "—";
    if ($("actInstalador")) $("actInstalador").textContent = data.instalador || "—";

    const notas = Array.isArray(data.notas) ? data.notas : [];
    if (!notas.length) {
      list.innerHTML = '<div class="text-secondary text-center py-4">Sin notas publicadas.</div>';
      return;
    }

    list.innerHTML = notas
      .map((n) => {
        const items = (n.items || [])
          .map((it) => `<li>${escapeHtml(it)}</li>`)
          .join("");
        return `
          <div class="card card-panel update-card mb-3">
            <div class="card-body">
              <div class="d-flex flex-wrap gap-2 align-items-center mb-2">
                <span class="badge badge-version">v${escapeHtml(n.version || "")}</span>
                <span class="small text-secondary">${escapeHtml(n.fecha || "")}</span>
              </div>
              <h3 class="h6 mb-2">${escapeHtml(n.titulo || "")}</h3>
              <ul class="mb-0 small">${items}</ul>
            </div>
          </div>`;
      })
      .join("");
  } catch (err) {
    list.innerHTML = `<div class="text-danger text-center py-4">${escapeHtml(err.message)}</div>`;
  }
}

function resetGenerateForm() {
  $("formGenerar").reset();
  $("clienteSelect").value = "";
  selectedClienteId = null;
  initDateFields();
  applyPresetLite();
  $("resultadoOk").classList.add("d-none");
  $("resultadoError").classList.add("d-none");
  $("resultadoVacio").classList.remove("d-none");
}

document.addEventListener("DOMContentLoaded", async () => {
  initDateFields();
  initConfigModal();

  $("formLogin")?.addEventListener("submit", onLoginSubmit);
  $("btnLogout")?.addEventListener("click", () => onLogout());

  $("dias").addEventListener("input", syncFromDias);
  $("fechaVencimiento").addEventListener("change", syncFromFecha);
  $("formGenerar").addEventListener("submit", onGenerateSubmit);
  $("btnLimpiarForm").addEventListener("click", (e) => {
    e.preventDefault();
    resetGenerateForm();
  });
  $("clienteSelect").addEventListener("change", onClienteSelectChange);
  $("btnRecargarClientes").addEventListener("click", () => loadClientes().catch((err) => showToast(err.message)));
  $("btnPresetLite").addEventListener("click", applyPresetLite);
  $("btnLimpiarModulos").addEventListener("click", clearModulos);
  $("plan").addEventListener("change", onPlanChange);

  $("btnCopiar").addEventListener("click", async () => {
    const key = $("licenseKey").value;
    if (!key) return;
    await navigator.clipboard.writeText(key);
    showToast("Clave copiada al portapapeles.");
  });

  $("btnRefrescarHistorial").addEventListener("click", () => loadHistorial().catch((err) => showToast(err.message)));
  $("btnExportCsv").addEventListener("click", exportHistorialCsv);
  $("btnLimpiarFiltros").addEventListener("click", () => {
    $("filtroTexto").value = "";
    $("filtroEstado").value = "";
    $("filtroDesde").value = "";
    $("filtroHasta").value = "";
    applyHistorialFilters();
  });
  $("filtroTexto").addEventListener("input", applyHistorialFilters);
  $("filtroEstado").addEventListener("change", applyHistorialFilters);
  $("filtroDesde").addEventListener("change", applyHistorialFilters);
  $("filtroHasta").addEventListener("change", applyHistorialFilters);

  $("btnRefrescarClientes").addEventListener("click", () => loadClientes().catch((err) => showToast(err.message)));
  $("btnNuevoCliente").addEventListener("click", () => openClienteModal().catch((err) => showToast(err.message)));
  $("formCliente").addEventListener("submit", saveCliente);
  $("filtroClientes").addEventListener("input", () => renderClientesTable(clientesCache));

  $("btnRefrescarDashboard").addEventListener("click", () => loadDashboard().catch((err) => showToast(err.message)));
  $("btnGuardarConfig").addEventListener("click", onSaveConfig);

  $("tab-historial").addEventListener("shown.bs.tab", () => {
    if (!historialCache.length) loadHistorial().catch((err) => showToast(err.message));
  });
  $("tab-clientes").addEventListener("shown.bs.tab", () => {
    if (!clientesCache.length) loadClientes().catch((err) => showToast(err.message));
  });
  $("tab-dashboard").addEventListener("shown.bs.tab", () => {
    loadDashboard().catch((err) => showToast(err.message));
  });
  $("tab-auditoria").addEventListener("shown.bs.tab", () => {
    loadAuditoria().catch((err) => showToast(err.message));
  });
  $("btnRefrescarAuditoria").addEventListener("click", () => loadAuditoria().catch((err) => showToast(err.message)));
  $("filtroAuditoriaUsuario").addEventListener("input", applyAuditoriaFilters);
  $("filtroAuditoriaAccion").addEventListener("input", applyAuditoriaFilters);
  $("filtroAuditoriaIp").addEventListener("input", applyAuditoriaFilters);

  $("tab-actualizaciones").addEventListener("shown.bs.tab", () => {
    loadActualizaciones().catch((err) => showToast(err.message));
  });
  $("btnRefrescarActualizaciones").addEventListener("click", () =>
    loadActualizaciones().catch((err) => showToast(err.message))
  );

  // Limpiar API keys viejas del storage
  saveConfig({});

  const ok = await tryRestoreSession();
  if (ok) await afterLoginLoad();
  else showLogin();
});
