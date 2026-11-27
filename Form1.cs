using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.Face;

namespace YMH219_FaceRecognition
{
    public partial class Form1 : Form
    {
        private VideoCapture? _capture;
        private CascadeClassifier? _faceCascade;
        private LBPHFaceRecognizer? _recognizer;
        private Dictionary<int, string> _labelMap = new Dictionary<int, string>();

        private System.Windows.Forms.Timer _frameTimer = null!;
        private PictureBox pbKamera = null!;
        private TextBox txtKisiAdi = null!;
        private Button btnKameraAc = null!;
        private Button btnKameraKapat = null!;
        private Button btnYuzKaydet = null!;
        private Button btnEgit = null!;
        private Label lblDurum = null!;

        private Mat? _sonKare;
        private Rect[] _sonYuzler = Array.Empty<Rect>();
        private int _kayitSayaci = 0;

        private readonly string _dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dataset");
        private readonly string _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trainer.yml");

        public Form1()
        {
            ArayuzuOlustur();
            KlasorVeModelHazirla();
        }

        private void ArayuzuOlustur()
        {
            Text = "YMH 219 - OpenCV Yüz Algılama ve Tanıma";
            Width = 920;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            FormClosing += Form1_FormClosing;

            lblDurum = new Label
            {
                Text = "Durum: Hazır (Kamera kapalı)",
                Left = 20,
                Top = 12,
                Width = 860,
                Height = 24,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.DimGray
            };

            pbKamera = new PictureBox
            {
                Left = 20,
                Top = 44,
                Width = 640,
                Height = 480,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Black
            };

            GroupBox grpKontrol = new GroupBox
            {
                Text = "Kontroller",
                Left = 675,
                Top = 44,
                Width = 210,
                Height = 480
            };

            btnKameraAc = new Button { Text = "Kamerayı Aç", Left = 15, Top = 30, Width = 180, Height = 36 };
            btnKameraKapat = new Button { Text = "Kamerayı Kapat", Left = 15, Top = 75, Width = 180, Height = 36, Enabled = false };

            Label lblKisi = new Label { Text = "Kişi Adı:", Left = 15, Top = 135, Width = 180 };
            txtKisiAdi = new TextBox { Text = "Kisi1", Left = 15, Top = 158, Width = 180, Height = 28 };

            btnYuzKaydet = new Button { Text = "Yüz Kaydet", Left = 15, Top = 198, Width = 180, Height = 36, Enabled = false };
            btnEgit = new Button { Text = "Modeli Eğit", Left = 15, Top = 244, Width = 180, Height = 36 };

            Label lblNot = new Label
            {
                Text = "1. Kamerayı Aç\n2. Kişi adını yaz\n3. Yüz Kaydet ile birkaç kare al\n4. Modeli Eğit ile tanımayı aktif et",
                Left = 15,
                Top = 300,
                Width = 180,
                Height = 120,
                ForeColor = Color.DimGray
            };

            btnKameraAc.Click += BtnKameraAc_Click;
            btnKameraKapat.Click += BtnKameraKapat_Click;
            btnYuzKaydet.Click += BtnYuzKaydet_Click;
            btnEgit.Click += (s, e) => ModeliEgit();

            grpKontrol.Controls.AddRange(new Control[]
            {
                btnKameraAc, btnKameraKapat, lblKisi, txtKisiAdi, btnYuzKaydet, btnEgit, lblNot
            });

            Controls.AddRange(new Control[] { lblDurum, pbKamera, grpKontrol });

            _frameTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _frameTimer.Tick += FrameTimer_Tick;
        }

        private void KlasorVeModelHazirla()
        {
            Directory.CreateDirectory(_dataDir);

            string? cascadeYolu = CascadeDosyasiniBul();
            if (cascadeYolu != null)
            {
                _faceCascade = new CascadeClassifier(cascadeYolu);
            }
            else
            {
                DurumGuncelle("Uyarı: haarcascade_frontalface_default.xml bulunamadı (cascades klasörüne ekleyin).", Color.DarkRed);
            }

            ModeliEgit(sessiz: true);
        }

        private string? CascadeDosyasiniBul()
        {
            string[] adaylar =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cascades", "haarcascade_frontalface_default.xml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "haarcascade_frontalface_default.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "cascades", "haarcascade_frontalface_default.xml")
            };

            return adaylar.FirstOrDefault(File.Exists);
        }

        private void BtnKameraAc_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_faceCascade == null || _faceCascade.Empty())
                {
                    MessageBox.Show(
                        "haarcascade_frontalface_default.xml dosyası bulunamadı.\nProje klasöründeki 'cascades' klasörüne ekleyip tekrar deneyin.",
                        "Eksik Dosya",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                _capture = new VideoCapture(0);
                if (!_capture.IsOpened())
                {
                    MessageBox.Show("Kamera açılamadı. Bağlantıyı veya Windows kamera izinlerini kontrol edin.", "Kamera Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _frameTimer.Start();
                btnKameraAc.Enabled = false;
                btnKameraKapat.Enabled = true;
                btnYuzKaydet.Enabled = true;
                DurumGuncelle("Durum: Kamera açık — Yüz algılama aktif", Color.DarkGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kamera başlatılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnKameraKapat_Click(object? sender, EventArgs e)
        {
            KamerayiDurdur();
            DurumGuncelle("Durum: Kamera kapatıldı", Color.DimGray);
        }

        private void FrameTimer_Tick(object? sender, EventArgs e)
        {
            if (_capture == null || !_capture.IsOpened() || _faceCascade == null || _faceCascade.Empty())
                return;

            using var frame = new Mat();
            if (!_capture.Read(frame) || frame.Empty())
                return;

            _sonKare?.Dispose();
            _sonKare = frame.Clone();

            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            Rect[] yuzler = _faceCascade.DetectMultiScale(
                gray,
                scaleFactor: 1.15,
                minNeighbors: 5,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(80, 80));

            _sonYuzler = yuzler;

            foreach (Rect rect in yuzler)
            {
                string etiket = "Yüz Algılandı";
                Scalar renk = Scalar.LimeGreen;

                if (_recognizer != null && _labelMap.Count > 0)
                {
                    using var yuzGri = new Mat(gray, rect);
                    using var resizeYuz = new Mat();
                    Cv2.Resize(yuzGri, resizeYuz, new OpenCvSharp.Size(200, 200));

                    _recognizer.Predict(resizeYuz, out int labelId, out double confidence);
                    if (confidence < 85 && _labelMap.TryGetValue(labelId, out string? kisiAdi))
                    {
                        etiket = $"{kisiAdi} ({confidence:F0})";
                    }
                    else
                    {
                        etiket = $"Bilinmeyen ({confidence:F0})";
                        renk = Scalar.Orange;
                    }
                }

                Cv2.Rectangle(frame, rect, renk, 2);
                Cv2.PutText(
                    frame,
                    etiket,
                    new OpenCvSharp.Point(rect.X, Math.Max(22, rect.Y - 8)),
                    HersheyFonts.HersheySimplex,
                    0.65,
                    renk,
                    2);
            }

            Image? onceki = pbKamera.Image;
            pbKamera.Image = BitmapConverter.ToBitmap(frame);
            onceki?.Dispose();
        }

        private void BtnYuzKaydet_Click(object? sender, EventArgs e)
        {
            if (_sonKare == null || _sonKare.Empty() || _sonYuzler.Length == 0)
            {
                MessageBox.Show("Kamerada algılanan bir yüz bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string kisiAdi = string.IsNullOrWhiteSpace(txtKisiAdi.Text) ? "Kisi1" : txtKisiAdi.Text.Trim();
            string kisiKlasoru = Path.Combine(_dataDir, kisiAdi);
            Directory.CreateDirectory(kisiKlasoru);

            using var gray = new Mat();
            Cv2.CvtColor(_sonKare, gray, ColorConversionCodes.BGR2GRAY);

            foreach (Rect rect in _sonYuzler)
            {
                _kayitSayaci++;
                using var yuzGri = new Mat(gray, rect);
                using var resizeYuz = new Mat();
                Cv2.Resize(yuzGri, resizeYuz, new OpenCvSharp.Size(200, 200));

                string dosyaYolu = Path.Combine(kisiKlasoru, $"face_{_kayitSayaci}.png");
                Cv2.ImWrite(dosyaYolu, resizeYuz);
            }

            DurumGuncelle($"Durum: '{kisiAdi}' için yüz kaydedildi (Toplam kare: {_kayitSayaci}).", Color.DarkBlue);
        }

        private void ModeliEgit(bool sessiz = false)
        {
            try
            {
                var gorseller = new List<Mat>();
                var etiketler = new List<int>();
                _labelMap.Clear();

                int siradakiId = 0;
                foreach (string kisiKlasoru in Directory.GetDirectories(_dataDir).OrderBy(d => d))
                {
                    string kisiAdi = Path.GetFileName(kisiKlasoru);
                    var dosyalar = Directory.GetFiles(kisiKlasoru, "*.*")
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (dosyalar.Count == 0) continue;

                    siradakiId++;
                    _labelMap[siradakiId] = kisiAdi;

                    foreach (string dosya in dosyalar)
                    {
                        Mat gri = Cv2.ImRead(dosya, ImreadModes.Grayscale);
                        if (gri.Empty())
                        {
                            gri.Dispose();
                            continue;
                        }

                        var yenidenBoyut = new Mat();
                        Cv2.Resize(gri, yenidenBoyut, new OpenCvSharp.Size(200, 200));
                        gri.Dispose();

                        gorseller.Add(yenidenBoyut);
                        etiketler.Add(siradakiId);
                    }
                }

                if (gorseller.Count == 0)
                {
                    if (!sessiz)
                        MessageBox.Show("Eğitim için kayıtlı yüz görüntüsü bulunamadı. Önce 'Yüz Kaydet' ile fotoğraf ekleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _recognizer?.Dispose();
                _recognizer = LBPHFaceRecognizer.Create();
                _recognizer.Train(gorseller, etiketler);
                _recognizer.Save(_modelPath);

                foreach (var m in gorseller) m.Dispose();

                DurumGuncelle($"Durum: Model eğitildi ({_labelMap.Count} kişi, {etiketler.Count} örnek).", Color.DarkGreen);
                if (!sessiz)
                    MessageBox.Show("Yüz tanıma modeli başarıyla eğitildi.", "Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Model eğitilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void KamerayiDurdur()
        {
            _frameTimer.Stop();
            if (_capture != null)
            {
                if (_capture.IsOpened())
                    _capture.Release();
                _capture.Dispose();
                _capture = null;
            }
            btnKameraAc.Enabled = true;
            btnKameraKapat.Enabled = false;
            btnYuzKaydet.Enabled = false;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            KamerayiDurdur();
            _sonKare?.Dispose();
            _faceCascade?.Dispose();
            _recognizer?.Dispose();
            pbKamera.Image?.Dispose();
        }

        private void DurumGuncelle(string metin, Color renk)
        {
            lblDurum.Text = metin;
            lblDurum.ForeColor = renk;
        }
    }
}