# TCS Control Center — Kontrak Remote Agent

**Status:** kontrak rancangan; implementasi agent belum tersedia. Tombol UI tetap nonaktif sampai agent diuji.

## Jalur koneksi

1. Remote Agent berjalan di sesi pengguna Windows ASUS.
2. Agent hanya bind ke loopback ASUS: \`127.0.0.1:8765\`.
3. Toshiba membuat SSH local port-forward dari \`127.0.0.1:8765\` Toshiba menuju \`127.0.0.1:8765\` ASUS.
4. Dashboard di Toshiba memanggil endpoint loopback Toshiba; tidak ada port agent yang diekspos langsung ke Wi-Fi atau internet.
5. Setiap request dilindungi bearer token yang dibuat ketika agent dipasang. Token tidak boleh disimpan di Git dan tidak boleh ditampilkan dalam log.

Skema tunnel konseptual:

\`\`\`text
Toshiba Browser
    |
    | HTTP loopback + bearer token
    v
Toshiba 127.0.0.1:8765
    |
    | SSH encrypted port-forward
    v
ASUS 127.0.0.1:8765
    |
    v
TCS Remote Agent (interactive Windows session)
\`\`\`

## API yang direncanakan

| Method | Path | Fungsi |
|---|---|---|
| GET | \`/health\` | Pemeriksaan hidup agent dan status autentikasi. |
| GET | \`/api/status\` | Host, OS, CPU, memori, dan status runtime. |
| GET | \`/api/windows\` | Daftar jendela top-level yang terlihat. |
| POST | \`/api/actions/screen/off\` | Mematikan monitor pada sesi desktop ASUS. |
| POST | \`/api/actions/screen/on\` | Meminta monitor aktif kembali. |
| POST | \`/api/actions/windows/list\` | Mengambil daftar window yang terlihat. |
| POST | \`/api/actions/windows/close-active\` | Mengirim WM_CLOSE ke jendela aktif, setelah konfirmasi UI. |
| POST | \`/api/actions/windows/minimize-active\` | Meminimalkan jendela aktif. |
| POST | \`/api/actions/windows/maximize-active\` | Memaksimalkan jendela aktif. |
| POST | \`/api/actions/system/refresh\` | Memperbarui snapshot status sistem. |

Aksi arbitrary shell, PowerShell, file browsing, shutdown, dan restart **tidak** menjadi bagian API awal. Aksi berisiko harus didesain terpisah dengan autentikasi ulang dan konfirmasi.

## Skema status yang diharapkan

\`\`\`json
{
  "ok": true,
  "host": {
    "hostname": "DESKTOP-...",
    "os": "Windows 10"
  },
  "hardware": {
    "cpu": { "name": "AMD A4-9125", "loadPercent": 12 },
    "memory": { "totalMB": 3968, "availableMB": 1307, "usedPercent": 67 }
  },
  "services": {
    "sshd": "running",
    "agent": "running",
    "aiServer": "stopped"
  },
  "timestamp": "2026-11-26T00:00:00Z"
}
\`\`\`

Nilai pada contoh hanya format, bukan data aktual.

## Aturan keamanan

- Bind hanya ke loopback pada ASUS; jangan gunakan \`0.0.0.0\`.
- Port 8765 hanya diteruskan melalui SSH.
- Token acak dan tidak berada di repository.
- Terapkan CORS untuk origin dashboard yang ditentukan; jangan menggunakan wildcard.
- Validasi method, path, body, dan ukuran request.
- Jangan menyediakan endpoint eksekusi shell generik.
- Batasi aksi GUI pada daftar allowlist.
- Catat aksi tanpa merekam token atau password.
- SSH tetap menggunakan autentikasi OpenSSH; jangan membuka port 22 ke internet.
- Public Choco AI Gateway tidak boleh mendapat akses ke Remote Agent.

## Batasan Windows GUI

OpenSSH menjalankan proses pada sesi non-interaktif yang terpisah dari desktop. Karena itu, daftar window, fokus window, tutup window, dan kontrol monitor harus dilakukan oleh agent yang berjalan pada sesi desktop pengguna. Menjalankan fungsi GUI langsung dari sesi SSH saja tidak cukup.

## Status integrasi UI

- [x] UI menyediakan kolom alamat agent dan token.
- [x] UI membatasi alamat agent ke loopback port 8765.
- [x] UI menggunakan allowlist endpoint dan tidak membentuk command shell bebas.
- [x] Tombol aksi nonaktif saat agent belum terhubung.
- [x] Token hanya disimpan di memori halaman, bukan localStorage.
- [ ] Implementasikan agent PowerShell/.NET untuk Windows 10.
- [ ] Buat installer agent dan tugas otomatis saat user login.
- [ ] Terapkan URL ACL atau listener loopback yang tidak memerlukan hak admin berlebih.
- [ ] Implementasikan autentikasi, CORS, dan validasi input di agent.
- [ ] Buat skrip pembuka tunnel di Toshiba.
- [ ] Jalankan uji fungsi nyata pada ASUS: status, list window, monitor off/on, dan close active.
- [ ] Lakukan audit keamanan sebelum dipakai pada jaringan bersama.
