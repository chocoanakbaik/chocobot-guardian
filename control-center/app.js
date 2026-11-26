(function () {
  "use strict";

  var apiBaseInput = document.getElementById("api-base");
  var tokenInput = document.getElementById("api-token");
  var connectButton = document.getElementById("connect-agent");
  var connectLabel = document.getElementById("connection-label");
  var connectDot = document.getElementById("connection-dot");
  var logRoot = document.getElementById("activity-log");
  var connected = false;
  var apiBase = "http://127.0.0.1:8765";
  var token = "";

  function log(message, type) {
    var row = document.createElement("div");
    var time = document.createElement("time");
    var text = document.createElement("span");
    row.className = "log-entry";
    time.textContent = new Date().toLocaleTimeString("id-ID", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
    text.textContent = message;
    if (type === "error") text.style.color = "#d18b82";
    row.appendChild(time);
    row.appendChild(text);
    logRoot.prepend(row);
  }

  function setConnected(value, label) {
    connected = value;
    connectLabel.textContent = label || (value ? "ASUS terhubung" : "ASUS belum terhubung");
    connectDot.className = "status-dot " + (value ? "online" : "offline");
    document.querySelectorAll(".action-button").forEach(function (button) {
      button.disabled = !value;
    });
    if (!value) {
      document.getElementById("metric-host").textContent = "Belum diketahui";
      document.getElementById("metric-os").textContent = "Menunggu koneksi agent";
    }
  }

  function request(path, method, body) {
    if (!connected && path !== "/health") return Promise.reject(new Error("ASUS belum terhubung."));
    var headers = { "Accept": "application/json" };
    if (token) headers.Authorization = "Bearer " + token;
    if (body !== undefined) headers["Content-Type"] = "application/json";
    return fetch(apiBase + path, {
      method: method || "GET",
      headers: headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      cache: "no-store"
    }).then(function (response) {
      return response.text().then(function (text) {
        var data = {};
        try { data = text ? JSON.parse(text) : {}; } catch (_) { throw new Error("Respons agent bukan JSON yang valid."); }
        if (!response.ok) throw new Error(data.error || ("HTTP " + response.status));
        return data;
      });
    });
  }

  function refreshStatus() {
    return request("/api/status", "GET").then(function (data) {
      var host = data.host || {};
      var hardware = data.hardware || {};
      var cpu = hardware.cpu || {};
      var memory = hardware.memory || {};
      var services = data.services || {};
      document.getElementById("metric-host").textContent = host.hostname || "ASUS";
      document.getElementById("metric-os").textContent = host.os || "Windows";
      document.getElementById("metric-cpu").textContent = cpu.loadPercent == null ? "--" : cpu.loadPercent + "%";
      document.getElementById("metric-cpu-name").textContent = cpu.name || "CPU";
      document.getElementById("metric-memory").textContent = memory.availableMB == null ? "--" : Math.round(memory.availableMB) + " MB tersedia";
      document.getElementById("metric-memory-note").textContent = memory.totalMB == null ? "RAM" : Math.round(memory.totalMB) + " MB total";
      document.getElementById("metric-ai").textContent = services.aiServer === "running" ? "Aktif" : "Belum aktif";
      document.getElementById("metric-ai-note").textContent = services.aiServer === "running" ? "Runtime Choco AI berjalan" : "Runtime belum berjalan";
      log("Status ASUS berhasil diperbarui.", "success");
      return data;
    });
  }

  function connect() {
    apiBase = apiBaseInput.value.replace(/\/+$/, "");
    token = tokenInput.value.trim();
    if (!/^http:\/\/(127\.0\.0\.1|localhost):8765$/.test(apiBase)) {
      log("Alamat agent harus menggunakan loopback port 8765 melalui SSH tunnel.", "error");
      return;
    }
    if (!token) {
      log("Masukkan token akses agent terlebih dahulu.", "error");
      tokenInput.focus();
      return;
    }
    request("/health", "GET").then(function (data) {
      if (data.authRequired && !token) throw new Error("Agent meminta token akses.");
      connected = true;
      setConnected(true, "ASUS terhubung melalui tunnel");
      return refreshStatus();
    }).then(function () {
      log("Koneksi remote agent siap.", "success");
    }).catch(function (error) {
      setConnected(false, "ASUS belum terhubung");
      log("Gagal menghubungkan: " + error.message, "error");
    });
  }

  function runAction(action, confirmMessage) {
    if (!connected) return;
    if (confirmMessage && !window.confirm(confirmMessage)) return;
    request("/api/actions/" + action, "POST", {}).then(function (data) {
      log(data.message || ("Aksi " + action + " berhasil dikirim."), "success");
      if (action === "windows/list") renderWindows(data.items || []);
      if (action === "system/refresh") refreshStatus();
    }).catch(function (error) {
      log("Aksi gagal: " + error.message, "error");
    });
  }

  function renderWindows(items) {
    var root = document.getElementById("window-list");
    root.textContent = "";
    if (!items.length) {
      var empty = document.createElement("span");
      empty.className = "muted";
      empty.textContent = "Tidak ada jendela aplikasi yang terlihat.";
      root.appendChild(empty);
      return;
    }
    items.forEach(function (item) {
      var row = document.createElement("div");
      var title = document.createElement("span");
      var process = document.createElement("small");
      row.className = "window-item";
      title.textContent = item.title || "(tanpa judul)";
      process.textContent = item.process || "";
      row.appendChild(title);
      row.appendChild(process);
      root.appendChild(row);
    });
  }

  connectButton.addEventListener("click", connect);
  document.getElementById("disconnect").addEventListener("click", function () {
    setConnected(false, "Koneksi diputus");
    token = "";
    tokenInput.value = "";
    log("Token dihapus dari sesi browser dan koneksi diputus.");
  });
  document.getElementById("refresh-status").addEventListener("click", function () {
    if (connected) refreshStatus().catch(function (error) { log(error.message, "error"); });
    else log("Hubungkan ASUS terlebih dahulu.", "error");
  });
  document.getElementById("list-windows").addEventListener("click", function () { runAction("windows/list"); });
  document.getElementById("check-system").addEventListener("click", function () { runAction("system/refresh"); });
  document.getElementById("check-ram").addEventListener("click", function () {
    if (connected) refreshStatus().then(function (data) {
      var m = data.hardware && data.hardware.memory ? data.hardware.memory : {};
      log("RAM tersedia: " + (m.availableMB == null ? "tidak tersedia" : Math.round(m.availableMB) + " MB"), "success");
    }).catch(function (error) { log(error.message, "error"); });
  });
  document.querySelectorAll("[data-action]").forEach(function (button) {
    button.addEventListener("click", function () {
      var action = button.getAttribute("data-action");
      var message = action === "windows/close-active" ? "Tutup jendela aplikasi yang sedang aktif di ASUS?" : null;
      runAction(action, message);
    });
  });
  document.getElementById("clear-log").addEventListener("click", function () {
    logRoot.textContent = "";
    log("Tampilan aktivitas dibersihkan.");
  });

  setConnected(false);
  log("Control Center tahap awal dimuat. Remote Agent ASUS belum dipasang.");
})();