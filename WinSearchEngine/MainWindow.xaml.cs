using System;
using System.IO;
using System.Text;
using System.Windows;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Diagnostics;
using System.Windows.Controls;
using Syncfusion.DocIO.DLS;
using System.Globalization;

namespace WinSearchEngine
{
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        int tarananDosyaAdeti = 0;
        private void btnAra_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (txtAra.Text == string.Empty) //Arama boş iken işleme girmeye çalışmaması için.
                {
                    MessageBox.Show("Arama için uygunsuz metin!", "Hatalı İşlem", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog(); //Klasör seçme dialog'u.
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK) //Eğer sonuş OK ise işleme başla.
                {
                    //Listboxları temizle.
                    lbTamDosyaAdi.Items.Clear();
                    lbTamIcerik.Items.Clear();
                    lbYakinDosyaAdi.Items.Clear();
                    lbYakinIcerik.Items.Clear();
                    //İşleme başlamadan önce işlem süresini ölçmek için saat başlatıyoruz.
                    var watch = new Stopwatch();
                    watch.Start();
                    //KlasorArama'ya seçilen konumu veriyoruz.
                    KlasorArama(dialog.SelectedPath);
                    //İşlem bitince saati durduruyoruz ve ölçüm sonucunu mesaj olarak gösteriyoruz.
                    watch.Stop();
                    this.Title = tarananDosyaAdeti.ToString();
                    MessageBox.Show(((double)watch.ElapsedMilliseconds / 1000).ToString() + " Saniyede Bulundu.");
                    foreach (Process wordProcess in Process.GetProcessesByName("winword")) //Açık kalan tüm Wordleri kapatıyoruz.
                        wordProcess.Kill();
                }
            }
            catch { };
        }

        //Bu method kaynak klasörden başlayıp tüm alt klasörlere inerek recursive bir şekilde dosyalara ulaşır.
        private void KlasorArama(string kaynakKlasor)
        {
            // Kaynak klasörü al.
            DirectoryInfo klasor = new DirectoryInfo(kaynakKlasor);

            if (!klasor.Exists) //Kaynak klasör yoksa hata ver.
                throw new DirectoryNotFoundException("Klasör Bulunamadı : " + kaynakKlasor);
            
            // Kaynak klasördeki alt klasörleri al.
            DirectoryInfo[] klasorler = klasor.GetDirectories();

            // Kaynak klasördeki dosyaları al.
            FileInfo[] dosyalar = klasor.GetFiles();
            foreach (FileInfo dosya in dosyalar) //Her bir dosya için işlem.
            {
                string geciciYol = System.IO.Path.Combine(kaynakKlasor, dosya.Name); //Dosyanın tam yolunu oluştur.
                string[] dosyaAdiUzantisi = dosya.Name.Split('.'); //Dosyanın adını ve uzantısını böl.

                //Eğer docx, txt, pdf veya html ise dosya adı için eşleşme veya yakınlık kontrol et.
                if(dosyaAdiUzantisi[1] == "cs")
                {
                    tarananDosyaAdeti++;
                    string[] dosyaAdiKelimeler = dosyaAdiUzantisi[0].Split(' ');
                    if (RabinKarpMatching(dosyaAdiUzantisi[0].ToLower(), txtAra.Text.ToLower()))
                        lbTamDosyaAdi.Items.Add(geciciYol);
                    else
                        foreach (string kelime in dosyaAdiKelimeler)
                            if(LevenshteinDistance(kelime.ToLower(), txtAra.Text.ToLower()) < 3)
                                lbYakinDosyaAdi.Items.Add(geciciYol);
                }

                int sayac = 1; //Satır numarasını sayacak sayaç.
                //İçerik okuma kısmı.
                switch (dosyaAdiUzantisi[1])
                {
                    case "docx": //WORD DOSYASI OKUMA

                        try
                        {
                            WordDocument document = new WordDocument(geciciYol.ToLower(new CultureInfo("en-US", false)));
                            string anaMetin = document.GetText();
                            string[] satirlar = anaMetin.Split('\n');
                            for (int i = 1; i < satirlar.Length; i++)
                                if (RabinKarpMatching(satirlar[i].ToLower(), txtAra.Text.ToLower()))
                                    lbTamIcerik.Items.Add(geciciYol + "= " + i + ". Satır. Eşleşen metin : " + satirlar[i]);
                                else
                                    foreach (string kelime in satirlar[i].Split(' '))
                                        if (LevenshteinDistance(kelime.ToLower(), txtAra.Text.ToLower()) < 3)
                                            lbYakinIcerik.Items.Add(geciciYol + "= " + i + ". Satır. Kelime : " + kelime);
                        }
                        catch { }

                        break;
                    case "pdf": //PDF OKUMA

                        PdfReader pdfOkuyucu = new PdfReader(geciciYol); //Pdf okuma için kullanılan kütüphaneden nesne ürettik.
                        int sayfaNumarasi = pdfOkuyucu.NumberOfPages; //Sayfa numarasını tutan değişken.
                        string[] kelimeler; //Kelimeleri tutacak dizi.
                        string pdfSatiri; //Okunan satır bilgisini tutacak değişken.

                        for (int i = 1; i <= sayfaNumarasi; i++)
                        {
                            //Tüm metni okuma işlemi.
                            string metin = PdfTextExtractor.GetTextFromPage(pdfOkuyucu, i, new LocationTextExtractionStrategy());
                            //Satırlara indirgemek için \n'e göre ayırma işlemi.
                            kelimeler = metin.Split('\n');
                            for (int j = 0, len = kelimeler.Length; j < len; j++) //Her satıra ulaşım için döngü.
                            {
                                pdfSatiri = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(kelimeler[j])); //Satır bilgisini alma.
                                string[] pdfKelimeler = pdfSatiri.Split(' ');
                                if (RabinKarpMatching(pdfSatiri.ToLower(), txtAra.Text.ToLower()))
                                    lbTamIcerik.Items.Add(geciciYol + "= " + (j + 1) + ". Satır. Eşleşen metin : " + pdfSatiri);
                                else
                                    foreach (string kelime in pdfKelimeler)
                                        if(LevenshteinDistance(kelime.ToLower(), txtAra.Text.ToLower()) < 3)
                                            lbYakinIcerik.Items.Add(geciciYol + "= " + (j + 1) + ". Satır. Kelime : " + kelime);
                            }
                        }

                        break;
                    case "html": //HTML VE TXT OKUMA
                    case "cs":

                        FileStream dosyaStream = File.OpenRead(geciciYol); //txt bazlı dosyaları okuma için temel classtan nesne ürettik.
                        StreamReader streamOkuyucu = new StreamReader(dosyaStream, Encoding.UTF8, true, 128);
                        string satir; //Okunan satir bilgisini tutacak değişken.
                        while ((satir = streamOkuyucu.ReadLine()) != null) //Okuma işlemi bitene kadar dönecek while döngüsü.
                        {
                            string[] txtKelimeler = satir.Split(' ');
                            if (RabinKarpMatching(satir.ToLower(), txtAra.Text.ToLower()))
                                lbTamIcerik.Items.Add(geciciYol + "= " + sayac + ". Satır. Eşleşen metin : " + satir);
                            else
                                foreach (string kelime in txtKelimeler)
                                    if(LevenshteinDistance(kelime.ToLower(), txtAra.Text.ToLower()) < 3)
                                        lbYakinIcerik.Items.Add(geciciYol + "= " + sayac + ". Satır Kelime : " + kelime);
                        }

                        break;
                }
            }

            foreach (DirectoryInfo altKlasor in klasorler) //Tüm alt klasörler için recursive yapıya geçiş.
                KlasorArama(altKlasor.FullName);
        }

        //Levenshtein Distance Algorithm. ÇALIŞMA PRENSİBİ ÖĞRENİLECEK.
        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // Adım 1
            if (n == 0)
                return m;

            if (m == 0)
                return n;

            // Adım 2
            for (int i = 0; i <= n; d[i, 0] = i++) { }

            for (int j = 0; j <= m; d[0, j] = j++) { }

            // Adım 3
            for (int i = 1; i <= n; i++)
            {
                // Adım 4
                for (int j = 1; j <= m; j++)
                {
                    // Adım 5
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    // Adım 6
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            // Adım 7
            return d[n, m];
        }

        //Rabin–Karp String Matching Algorithm. ÇALIŞMA PRENSİBİ ÖĞRENİLECEK.
        public bool RabinKarpMatching(string A, string B)
        {
            if (A.Equals(string.Empty))
                return false;

            if(A.Length < B.Length)
                return false;

            ulong siga = 0;
            ulong sigb = 0;
            ulong Q = 100007;
            ulong D = 256;

            for (int i = 0; i < B.Length; ++i)
            {
                siga = (siga * D + (ulong)A[i]) % Q;
                sigb = (sigb * D + (ulong)B[i]) % Q;
            }

            if (siga == sigb)
                return true;

            ulong pow = 1;

            for (int k = 1; k <= B.Length - 1; ++k)
                pow = (pow * D) % Q;

            for (int j = 1; j <= A.Length - B.Length; ++j)
            {
                siga = (siga + Q - pow * (ulong)A[j - 1] % Q) % Q;
                siga = (siga * D + (ulong)A[j + B.Length - 1]) % Q;

                if (siga == sigb)
                    if (A.Substring(j, B.Length) == B)
                        return true;
            }

            return false;
        }

        private void listbox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ListBox lb = (ListBox)sender;

            if (lb.SelectedItem == null)
                return;

            Process.Start(lb.SelectedItem.ToString().Split('=')[0]); 
            //Çift tıklanan LisboxItem'ı ='e göre bölüp solda kalan kısmı (Dosya Yolu) açıyoruz.
        }
    }
}
