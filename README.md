# PilGöstergesi

![License](https://img.shields.io/badge/license-MIT-green)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6)
![Release](https://img.shields.io/github/v/release/migren1/PilGostergesi)
![Downloads](https://img.shields.io/github/downloads/migren1/PilGostergesi/total)

Kablosuz kulaklık ve mouse'ların pil seviyesini **Windows görev çubuğunun üzerinde** (gizli taşma menüsünde değil) gösteren küçük bir araç. Her cihaz için simge + yüzde çizer; şarj olurken yeşil, %15 altında kırmızı olur.

![Görev çubuğu](docs/onizleme.png)

## ⬇️ İndir
**[➡️ PilGostergesi-Setup-1.0.0.exe indir](https://github.com/migren1/PilGostergesi/releases/download/v1.0.0/PilGostergesi-Setup-1.0.0.exe)** &nbsp;·&nbsp; [tüm sürümler](https://github.com/migren1/PilGostergesi/releases/latest)

İndir → çalıştır → kur. Program Files'a kurulur, "Programlar ekle/kaldır"da görünür. Kurulumda **"Windows açılışında başlat"** seçeneği varsayılan işaretli gelir.

> İmzasız olduğu için SmartScreen uyarısı çıkabilir: **Daha fazla bilgi → Yine de çalıştır**.

## Özellikler
- Görev çubuğuna gömülü, şeffaf widget (Shell_TrayWnd'ye bağlı katmanlı pencere)
- Sol tık menüsü: cihaz gizle/göster, şimdi yenile, Windows ile başlat, çıkış
- 10 saniyede bir ve cihaz takılınca otomatik yenileme
- Tek dosya, bağımlılık yok (sadece .NET Framework — Windows 10/11'de hazır gelir)
- Yönetici gerektirmez

## Desteklenen cihazlar
| Cihaz | Yöntem |
|---|---|
| HyperX Cloud III S kablosuz kulaklık | Vendor HID (rapor 0x0C) |
| ATK / VXE NK kablosuz mouse (NANO dongle) | ATK BITMOUSE (FF05, rapor 0x08) |
| Logitech kablosuz mouse (G Pro X Superlight vb.) | HID++ 2.0 (feature 0x1004 / 0x1000 / 0x1001) |

Yeni cihaz eklemek için `Driver` sınıfından türetip `Poller.Drivers`'a eklemek yeterli.

## Kurulum / Çalıştırma
1. `derle.bat` ile derle (Windows'taki `csc.exe`, derleme bağımlılığı yok), ya da hazır `PilGostergesi.exe`'yi çalıştır.
2. Cihaz görünmüyorsa teşhis için: `PilGostergesi.exe --test` → `test.txt` üretir.

## Nasıl çalışır
Pil değerleri, her cihazın kendi kablosuz alıcısına/dongle'ına doğrudan **salt-okunur HID** sorgularıyla okunur; hiçbir cihaza yazma yapılmaz, hiçbir veri dışarı gönderilmez. Protokol baytları açık kaynaklı projelerden referans alınarak sıfırdan yazılmıştır.

## Lisans
MIT
