# Perkembangan Choco AI di ASUS

**Pemilik proyek:** The Choco Studios (TCS)  
**Status:** Pengembangan awal — fondasi perangkat dan runtime lokal sudah berhasil diuji; sistem AI belum siap digunakan pelanggan atau publik.  
**Repositori:** chocoanakbaik/chocobot-guardian  
**Dokumen utama:** catatan perkembangan, keputusan, hasil pengujian, dan pekerjaan berikutnya.

---

## Tujuan proyek

Membangun **Choco AI**, kecerdasan internal yang hanya melayani ekosistem The Choco Studios. Choco AI bukan asisten pengetahuan umum.

Choco AI ditargetkan untuk:
- Memahami identitas, produk, layanan, dokumentasi, dan operasional TCS.
- Menjawab berdasarkan sumber resmi TCS yang tersedia dan terverifikasi.
- Menolak pertanyaan di luar cakupan TCS.
- Mengakui ketika informasi TCS belum tersedia, bukan mengarang jawaban.
- Menjalankan tugas TCS yang diizinkan melalui sistem aksi terstruktur.
- Menyimpan pengetahuan dan pengalaman operasional secara terkelola.
- Dikelola melalui antarmuka grafis dari Toshiba, sementara ASUS menjadi node pemrosesan lokal.
- Memiliki lingkungan privat internal dan, setelah lulus evaluasi keamanan serta kualitas, kemungkinan layanan publik dengan akses yang dibatasi.

## Arsitektur target

| Komponen | Tanggung jawab |
|---|---|
| Choco AI Core | Menjalankan model bahasa lokal di ASUS. |
| TCS Knowledge Engine | Mengindeks dokumentasi dan sumber resmi TCS untuk pencarian berbasis sumber (RAG). |
| TCS Scope Guard | Membatasi pertanyaan dan jawaban pada ekosistem TCS. |
| Response Validator | Memeriksa dukungan sumber dan mencegah jawaban yang tidak berdasar. |
| TCS Memory Manager | Mengelola working memory, permanent knowledge, dan experience memory. |
| TCS Experience Engine | Mencatat tugas, tindakan, hasil, kegagalan, dan prosedur yang sudah diverifikasi (learning by doing). |
| TCS Action Engine | Menjalankan aksi terbatas melalui daftar perintah yang diizinkan, dengan konfirmasi untuk tindakan berisiko. |
| TCS Remote Agent | Menghubungkan kontrol desktop ASUS dengan sesi pengguna Windows yang benar. |
| TCS Control Center | Dashboard grafis di Toshiba untuk melihat status dan mengendalikan ASUS serta Choco AI. |
| TCS Public Gateway | Gerbang publik terpisah yang hanya dapat mengakses informasi TCS yang memang boleh dipublikasikan. |

**Prinsip penting:** pembaruan memori bukan berarti bobot model otomatis dilatih ulang. Pengetahuan diperbarui melalui sumber terverifikasi dan indeks; pengalaman disimpan sebagai catatan yang dapat dipanggil kembali. Perubahan ke data resmi harus memiliki sumber, versi, waktu, dan jejak perubahan.

## Lingkungan perangkat yang sudah diuji

### ASUS — node lokal
- Prosesor: AMD A4-9125.
- RAM terpasang yang terdeteksi Windows: 3.968 MB (sekitar 4 GB).
- Sistem: Windows 10, 64-bit.
- Peran: node pemrosesan AI lokal dan remote agent.
- Direktori kerja awal: %USERPROFILE%\TCS\ChocoAI\.

### Toshiba — pusat kendali
- Peran: perangkat operator untuk mengelola ASUS melalui SSH dan, pada tahap berikutnya, TCS Control Center.
- Kedua laptop berhasil berkomunikasi melalui jaringan Wi-Fi lokal yang sama.

Alamat IP lokal, nama perangkat Windows, nama akun, kata sandi, fingerprint SSH, dan informasi jaringan pribadi sengaja tidak dicatat di repositori.

## Yang sudah dikerjakan dan diverifikasi

### 1. Koneksi Toshiba ke ASUS
- [x] OpenSSH Server dipasang di ASUS.
- [x] Layanan sshd berhasil dijalankan.
- [x] Startup layanan SSH diatur ke otomatis.
- [x] Pemulihan layanan SSH diatur untuk mencoba restart setelah kegagalan.
- [x] Aturan Windows Firewall dibuat untuk koneksi SSH dari Toshiba pada jaringan lokal.
- [x] Koneksi ping IPv4 Toshiba ke ASUS berhasil: 4 paket diterima dari 4 paket (0% loss).
- [x] Resolusi nama perangkat ASUS berhasil diuji menggunakan IPv4.
- [x] Login SSH dari Toshiba ke akun Windows ASUS berhasil.
- [x] Perintah yang diketik dari sesi SSH terbukti dieksekusi di ASUS.

**Catatan:** resolusi nama perangkat pernah memilih alamat IPv6 link-local dan ping IPv6 tidak mendapat balasan. Pengujian IPv4 berhasil. Aturan firewall yang dibuat masih terbatas pada alamat lokal Toshiba saat pengujian; ketahanan ketika DHCP mengganti alamat perlu dibenahi. SSH hanya ditujukan untuk jaringan privat tepercaya dan tidak boleh dibuka langsung ke internet.

### 2. Konfigurasi daya ASUS
- [x] Sleep otomatis ketika menggunakan daya AC dinonaktifkan.
- [x] Hibernate otomatis ketika menggunakan daya AC dinonaktifkan.
- [x] Waktu mati layar ketika menggunakan daya AC diatur menjadi satu menit.
- [x] Layar ASUS berhasil mati sementara Windows tetap dapat diakses melalui SSH.

**Belum diverifikasi:** perilaku setelah listrik padam, setelah restart Windows, saat Wi-Fi putus dan tersambung kembali, serta pemulihan otomatis setelah seluruh perangkat dimulai ulang. Pengaturan ini tidak membuat laptop kebal dari mati listrik, panas berlebih, kerusakan, atau gangguan jaringan. Suhu dan ventilasi tetap harus dipantau.

### 3. Runtime AI lokal
- [x] WinGet terdeteksi, tetapi pembaruan sumber paket dibatalkan pada 0%; instalasi runtime kemudian dilakukan melalui arsip rilis GitHub.
- [x] Paket llama.cpp Windows CPU x64 rilis/build b12760 diunduh dan diekstrak.
- [x] llama-cli.exe dan llama-server.exe ditemukan.
- [x] llama-cli.exe --version berjalan dan melaporkan build b12760-d9dae0344, Windows x86_64.

Lokasi runtime awal: %USERPROFILE%\TCS\ChocoAI\bin\.

### 4. Model lokal pertama
- [x] Model GGUF Qwen2.5-0.5B-Instruct Q3_K_M diunduh.
- [x] Ukuran yang diperiksa: 355.466.560 byte.
- [x] SHA-256 hasil pemeriksaan cocok dengan hash berkas yang ditetapkan saat pengunduhan: b8cd24438ac853e76935965776bd974730cedbee06b2982a0e584c38e9ef7762.
- [x] Model berhasil dimuat dan dijalankan dengan llama.cpp pada CPU ASUS.
- [x] Uji awal menghasilkan kecepatan generasi sekitar 6–7 token/detik pada konfigurasi pengujian.

Lokasi model awal: %USERPROFILE%\TCS\ChocoAI\models\Qwen2.5-0.5B-Instruct-Q3_K_M.gguf.

Sumber unduhan model yang digunakan pada tahap uji adalah repositori GGUF di Hugging Face. Asal-usul berkas, lisensi model dasar, lisensi konversi/kuantisasi, dan ketentuan redistribusi wajib ditinjau kembali sebelum model atau layanan dipublikasikan.

### 5. Hasil evaluasi perilaku awal
- [x] System prompt pembatasan TCS berhasil dimuat oleh runtime.
- [x] Pengujian percakapan dasar dilakukan.
- [ ] Identitas Choco AI belum konsisten.
- [ ] Model pernah menolak menjawab pertanyaan tentang identitasnya sendiri.
- [ ] Model pernah memberikan deskripsi TCS yang tidak akurat/tidak bersumber.
- [ ] Scope restriction belum dapat diandalkan hanya dengan system prompt.
- [ ] Model belum terhubung ke Knowledge Base resmi TCS.
- [ ] Model belum layak untuk layanan pelanggan atau publik.

**Kesimpulan pengujian:** mesin inferensi lokal berfungsi, tetapi kualitas jawaban dan pembatasan cakupan belum memenuhi standar. Jangan menganggap Choco AI sudah siap dipakai hanya karena model berhasil menghasilkan teks.

## Yang akan dikerjakan selanjutnya

### Tahap 1 — TCS Control Center
- [x] Membuat cangkang dashboard grafis responsif di `control-center/` dengan identitas visual TCS.
- [x] Membuat klien UI awal untuk koneksi agent, ringkasan perangkat, daftar window, dan perintah bahasa Indonesia berbasis allowlist.
- [x] Mendokumentasikan kontrak API dan pola loopback melalui SSH tunnel di `docs/CONTROL-CENTER-API.md`.
- [ ] Mengimplementasikan Remote Agent Windows yang berjalan pada sesi desktop interaktif.
- [ ] Mengamankan listener agent dengan token lokal, CORS terbatas, dan validasi request.
- [ ] Membuat skrip pembuka SSH tunnel dan dashboard di Toshiba.
- [ ] Menjalankan uji fungsi nyata pada ASUS untuk status, daftar window, layar mati/nyala, dan tutup window aktif.
- [ ] Menampilkan status disk, suhu jika sensor tersedia, dan pemantauan layanan secara akurat.

**Status implementasi tahap ini:** UI dan kontrak API sudah ditulis di branch `feature/tcs-control-center`, tetapi belum dijalankan pada Toshiba dan belum terhubung ke Remote Agent. Tombol kontrol tetap nonaktif saat agent tidak tersedia; tidak ada klaim bahwa kontrol GUI sudah berfungsi.

### Tahap 2 — Layanan Choco AI lokal
- [ ] Menguji llama-server.exe pada konfigurasi memori konservatif.
- [ ] Mengatur batas konteks, jumlah proses paralel, ukuran batch, dan logging untuk RAM 4 GB.
- [ ] Menyediakan API lokal yang tidak terbuka ke jaringan publik secara bawaan.
- [ ] Membuat startup dan shutdown layanan yang aman.
- [ ] Mengukur penggunaan RAM, CPU, latensi, stabilitas, dan suhu pada beban berulang.
- [ ] Menilai apakah model 0.5B cukup untuk tugas TCS; bila tidak, pertimbangkan model atau strategi yang lebih sesuai tanpa mengasumsikan perangkat mampu menjalankannya.

### Tahap 3 — TCS Identity dan Scope Guard
- [ ] Menulis identitas resmi Choco AI, peran, gaya komunikasi, dan batas kewenangan.
- [ ] Menetapkan TCS sebagai satu-satunya cakupan pengetahuan.
- [ ] Menolak pertanyaan umum di luar ekosistem TCS.
- [ ] Memisahkan fakta yang bersumber, kesimpulan, dan informasi yang belum diketahui.
- [ ] Melakukan pemeriksaan jawaban sebelum dikirim ke pengguna.
- [ ] Membuat kumpulan uji pertanyaan dalam cakupan dan di luar cakupan.

### Tahap 4 — TCS Knowledge Engine
- [ ] Menginventarisasi repositori GitHub TCS yang diizinkan.
- [ ] Menginventarisasi website resmi, halaman layanan, dokumentasi, SOP, dan dokumen internal yang boleh digunakan.
- [ ] Menetapkan klasifikasi publik, internal, terbatas, dan rahasia.
- [ ] Mengimpor dokumen dengan sumber, versi, tanggal pembaruan, dan status otoritas.
- [ ] Membuat indeks pencarian lokal; mulai dari pendekatan ringan yang sesuai RAM, lalu evaluasi pencarian semantik.
- [ ] Membuat sinkronisasi terjadwal atau berbasis perubahan dari sumber resmi yang diizinkan.
- [ ] Menyediakan mekanisme penghapusan, koreksi, dan versi lama agar informasi usang tidak terus dipakai.
- [ ] Memastikan Choco AI tidak menjawab sebagai fakta jika sumber yang cukup tidak ditemukan.

### Tahap 5 — Memory dan Learning by Doing
- [ ] **Working Memory:** konteks tugas yang sedang berlangsung.
- [ ] **Permanent Knowledge:** fakta resmi TCS yang memiliki sumber dan versi.
- [ ] **Experience Memory:** tugas, tindakan, hasil, kesalahan, dan prosedur yang sudah diverifikasi.
- [ ] Mencatat hasil pekerjaan secara otomatis sebagai kandidat pengalaman.
- [ ] Menandai pengalaman berhasil, gagal, belum diverifikasi, atau perlu persetujuan.
- [ ] Menggunakan pengalaman terverifikasi untuk membantu pekerjaan serupa berikutnya.
- [ ] Mencegah pengalaman atau percakapan mengubah fakta resmi tanpa otorisasi.
- [ ] Menyediakan audit log dan kemampuan memperbaiki/menghapus memori yang keliru.

**Definisi learning by doing:** sistem belajar dari tindakan dan hasil yang dicatat serta dievaluasi. Ini bukan berarti model otomatis melatih ulang bobotnya setiap kali melakukan pekerjaan.

### Tahap 6 — Action Engine dan integrasi TCS
- [ ] Membuat katalog tindakan yang diperbolehkan.
- [ ] Menghubungkan perintah bahasa Indonesia ke tindakan yang tervalidasi.
- [ ] Meminta konfirmasi untuk tindakan yang mengganggu pekerjaan, mengubah data, atau mematikan perangkat.
- [ ] Mengintegrasikan sumber TCS satu per satu setelah izin dan struktur datanya diperiksa.
- [ ] Menyediakan jejak siapa/apa yang memicu tindakan dan hasilnya.

### Tahap 7 — Pemisahan private dan public
- [ ] Choco AI Private untuk administrasi internal dan data sesuai hak akses.
- [ ] Choco AI Public hanya untuk sumber TCS yang disetujui untuk publikasi.
- [ ] Public Gateway tidak memiliki akses ke Remote Command, kredensial, data pelanggan privat, atau dokumen internal.
- [ ] Menggunakan autentikasi, pembatasan permintaan, logging, dan perlindungan penyalahgunaan.
- [ ] Tidak mengekspos SSH atau API model ASUS langsung ke internet.
- [ ] Menguji skenario kebocoran informasi, prompt injection, dan permintaan di luar cakupan.
- [ ] Melakukan uji beban sebelum membuka akses publik.

### Tahap 8 — Evaluasi dan kesiapan rilis
- [ ] Uji akurasi jawaban TCS.
- [ ] Uji penolakan pertanyaan di luar TCS.
- [ ] Uji ketahanan terhadap informasi yang saling bertentangan dan dokumen kedaluwarsa.
- [ ] Uji keamanan akses private/public.
- [ ] Uji pemulihan setelah restart, jaringan putus, dan layanan crash.
- [ ] Uji performa pada perangkat ASUS sebenarnya.
- [ ] Dokumentasikan batas kemampuan dan prosedur pemulihan.
- [ ] Rilis hanya setelah kriteria kualitas, keamanan, dan stabilitas terpenuhi.

## Keputusan teknis yang sudah ditetapkan

1. Choco AI hanya melayani ekosistem The Choco Studios; bukan AI pengetahuan umum.
2. Sumber jawaban TCS harus berasal dari sumber TCS yang diizinkan dan dapat ditelusuri.
3. Informasi yang tidak ditemukan harus dinyatakan belum tersedia, bukan ditebak.
4. ASUS menjadi node AI lokal; Toshiba menjadi pusat kendali dan perangkat operator.
5. Windows ASUS dipertahankan pada tahap awal; tidak ada migrasi OS atau perubahan partisi sebagai bagian dari pekerjaan yang sudah selesai.
6. TCS Control Center menjadi antarmuka utama; CMD bukan antarmuka operasional bagi pengguna.
7. Memori resmi, pengalaman, dan konteks kerja dipisahkan.
8. Akses publik dan privat harus dipisahkan secara arsitektural.
9. Tidak ada SSH atau API model yang dibuka langsung ke internet.
10. Status setiap fitur harus dilaporkan berdasarkan pengujian nyata, bukan asumsi.

## Catatan keamanan dan privasi

- Jangan pernah memasukkan kata sandi Windows/SSH, token, private key, API key, cookie, atau isi berkas rahasia ke repositori.
- Jangan mencatat alamat IP lokal, nama akun Windows, fingerprint SSH, atau detail jaringan rumah di dokumen publik.
- Jangan menaruh model GGUF, file runtime, log pribadi, database memori, atau dokumen internal di repository publik.
- Periksa status visibilitas repo sebelum menambahkan dokumen. Dokumen ini hanya memuat ringkasan teknis yang tidak memerlukan kredensial.
- Kredensial runtime dan konfigurasi lokal harus disimpan di perangkat, tidak di Git.

## Status keseluruhan

**Fondasi perangkat dan inferensi lokal: berhasil diuji.**  
**TCS Control Center: cangkang UI dan kontrak API tersedia di branch pengembangan; Remote Agent dan uji perangkat belum selesai.**  
**Identitas, pengetahuan TCS, memori belajar, API layanan penuh, dan publikasi: belum dibangun.**

Tahap kerja berikutnya yang diprioritaskan adalah **TCS Control Center**, kemudian layanan model lokal yang stabil dan TCS Knowledge Engine. Seluruh perkembangan berikutnya harus memperbarui dokumen ini agar repo tetap menjadi catatan sumber utama proyek.


## Catatan perkembangan terbaru — TCS Control Center

Tanggal pembaruan: 26 November 2026.

- Repository telah diperiksa sebelum perubahan; proyek lain seperti TCS-ENGINE tidak digunakan karena memiliki tujuan dan kode sendiri.
- Branch pengembangan dibuat: `feature/tcs-control-center`, berangkat dari `main` pada commit dokumentasi Choco AI.
- Berkas yang ditambahkan di branch:
  - `control-center/index.html` — kerangka antarmuka dashboard.
  - `control-center/styles.css` — gaya responsif gelap dengan aksen champagne gold.
  - `control-center/app.js` — klien awal untuk status, koneksi, daftar window, dan aksi allowlist.
  - `docs/CONTROL-CENTER-API.md` — kontrak agent, endpoint, pola SSH tunnel, dan aturan keamanan.
- Dashboard mengasumsikan agent lokal di loopback Toshiba port 8765 melalui SSH tunnel. Agent belum dibuat; karena itu tombol aksi nonaktif dan dashboard belum boleh dianggap sebagai aplikasi operasional.
- API yang direncanakan tidak menyediakan eksekusi shell bebas. Remote Agent harus berjalan pada sesi desktop interaktif ASUS karena sesi SSH tidak dapat mengelola semua jendela GUI secara langsung.
- Token agent, alamat jaringan privat, password, dan kredensial tidak dimasukkan ke GitHub.

**Langkah berikutnya:** implementasi dan pengujian TCS Remote Agent Windows, lalu skrip pembuka tunnel di Toshiba. Setelah kontrol sistem dasar teruji, sambungkan `llama-server` dan lanjut ke TCS Identity, Scope Guard, serta Knowledge Engine.
