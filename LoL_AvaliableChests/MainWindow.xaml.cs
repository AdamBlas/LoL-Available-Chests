using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LoL_AvaliableChests
{
    public partial class MainWindow : Window
    {
        List<ChampionDTO> champsList;
        bool errorOccurred = false;
        public ControlTemplate regionTemplate;
        public ControlTemplate selectedRegionTemplate;
        public string[] regions;
        string pathToConfigFile = "config.txt";
        string region;

        public MainWindow()
        {
            InitializeComponent();
            GenerateComboBoxContent();

            InitInputs();

            try
            {
                DownloadChampionData();
                Thread imgThread = new Thread(DownloadTilesImages);
                imgThread.Start();
            }
            catch (Exception)
            {
                return;
            }

            errorMessage.Foreground = System.Windows.Media.Brushes.White;
            errorMessage.Text = string.Empty;
        }
        void InitInputs()
        {
            bool regionSelected = false;

            if (File.Exists(pathToConfigFile))
            {
                string[] content = File.ReadAllLines(pathToConfigFile);

                if (content.Length == 3)
                {
                    playerNameInput.Text = content[0];
                    int regionIndex = Array.IndexOf(regions, content[1]);
                    if (regionIndex != -1)
                    {
                        regionInput.SelectedIndex = regionIndex;
                        regionSelected = true;
                    }
                    apiKeyInput.Password = content[2];
                }
            }

            if (!regionSelected)
            {
                regionInput.SelectedIndex = 0;
            }
        }
        void GenerateComboBoxContent()
        {
            regionTemplate = (ControlTemplate)this.Resources["regionTemplate"];
            selectedRegionTemplate = (ControlTemplate)this.Resources["selectedRegionTemplate"];

            regions = new string[] {
                "Brazil",
                "Europe North East",
                "Europe West",
                "Japan",
                "Korea",
                "Latin America 1",
                "Latin America 2",
                "North America",
                "Oceania",
                "Turkey",
                "Russia"
            };

            foreach (string region in regions)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Template = regionTemplate;
                item.Content = region;
                regionInput.Items.Add(item);
            }
        }
        void DownloadChampionData()
        {
            try
            {
                string champsJsonPath = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-summary.json";
                champsList = GetDTOFromJson<List<ChampionDTO>>(champsJsonPath);
            }
            catch (Exception)
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = "Failed to download champion data.";
                playerNameInput.IsEnabled = false;
                regionInput.IsEnabled = false;
                apiKeyInput.IsEnabled = false;
                generateButtonLabel.FontSize = 13;
                generateButtonLabel.Text = "Unable to generate";
                errorOccurred = true;
                throw;
            }
            champsList.RemoveAt(0);
            champsList = champsList.OrderBy(o => o.name).ToList();
        }
        void GetApiKey(object sender, RoutedEventArgs e)
        {
            if (errorOccurred)
                return;

            System.Diagnostics.Process.Start("https://developer.riotgames.com");
        }
        void GenerateList(object sender, RoutedEventArgs e)
        {
            if (errorOccurred)
                return;

            if (string.IsNullOrEmpty(playerNameInput.Text))
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = "Player name not given.";
                return;
            }

            if (string.IsNullOrEmpty(apiKeyInput.Password))
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = "Api key not given. You can get one on \nhttps://developer.riotgames.com";
                return;
            }

            string playerName = playerNameInput.Text;
            string summonerDataPath = "/lol/summoner/v4/summoners/by-name/" + playerName;

            SummonerDTO summonerInfo = null;
            List<ChampionMasteryDTO> championMasteryDTOs = null;

            try
            {
                summonerInfo = GetDTOFromApi<SummonerDTO>(summonerDataPath);
                string champDetailsPath = "/lol/champion-mastery/v4/champion-masteries/by-summoner/" + summonerInfo.id;
                championMasteryDTOs = GetDTOFromApi<List<ChampionMasteryDTO>>(champDetailsPath);
            }
            catch (Exception)
            {
                return;
            }

            errorMessage.Foreground = System.Windows.Media.Brushes.DarkGreen;
            errorMessage.Text = "Done.";

            string filePath = "chests.txt";
            StreamWriter writer = new StreamWriter(filePath);

            writer.WriteLine("Data generated for player " + playerName);
            writer.WriteLine("Time of generation: " + DateTime.Now + "\n");

            foreach (var champ in champsList)
            {
                bool chestGranted = false;
                foreach (var champData in from champData in championMasteryDTOs
                                          where champData.championId == champ.id
                                          select champData)
                {
                    chestGranted = champData.chestGranted;
                }

                string chestInfo = string.Empty;
                if (chestGranted)
                    chestInfo = "|        GRANTED | ";
                else
                    chestInfo = "| ABLE TO OBTAIN | ";

                chestInfo += champ.name;
                writer.WriteLine(chestInfo);
            }

            writer.Close();
            System.Diagnostics.Process.Start(filePath);
        }
        HttpResponseMessage GET(string URL)
        {
            using (HttpClient client = new HttpClient())
            {
                var result = client.GetAsync(URL);
                result.Wait();
                return result.Result;
            }
        }
        string GetURI(string path)
        {
            string apiKey = apiKeyInput.Password;
            string regionCode;
            switch (region)
            {
                case "Brazil":
                    regionCode = "br1";
                    break;
                case "Europe North East":
                    regionCode = "eun1";
                    break;
                case "Europe West":
                    regionCode = "euw1";
                    break;
                case "Japan":
                    regionCode = "jp1";
                    break;
                case "Korea":
                    regionCode = "k1";
                    break;
                case "Latin America 1":
                    regionCode = "la1";
                    break;
                case "Latin America 2":
                    regionCode = "la2";
                    break;
                case "North America":
                    regionCode = "na1";
                    break;
                case "Oceania":
                    regionCode = "oc1";
                    break;
                case "Turkey":
                    regionCode = "tr1";
                    break;
                case "Russia":
                    regionCode = "ru";
                    break;
                default:
                    throw new NotImplementedException("Region " + region + " not recognised");
            }
            return "https://" + regionCode + ".api.riotgames.com/" + path + "?api_key=" + apiKey;
        }
        T GetDTOFromApi<T>(string path)
        {
            string uri;
            try
            {
                uri = GetURI(path);
            }
            catch (NotImplementedException e)
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = e.Message;
                throw;
            }

            return GetDTOFromJson<T>(uri);
        }
        T GetDTOFromJson<T>(string path)
        {
            string content = GetJson(path);
            return JsonConvert.DeserializeObject<T>(content);
        }
        string GetJson(string uri)
        {
            var response = GET(uri);
            string content = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = "Error occured while connecting to Riot API.";
                throw new HttpRequestException();
            }

            return content;
        }
        void OnRegionChange(object sender, SelectionChangedEventArgs e)
        {
            for (int i = 0; i < regionInput.Items.Count; i++)
            {
                var item = ((ComboBoxItem)regionInput.Items.GetItemAt(i));
                item.Template = item == regionInput.SelectedItem ? selectedRegionTemplate : regionTemplate;
                item.ApplyTemplate();
                ((TextBlock)item.Template.FindName("textField", item)).Text = item.Content.ToString();
            }

            region = ((ComboBoxItem)regionInput.SelectedItem).Content.ToString();
            dropdownLabel.Text = region;
        }

        void DownloadTilesImages()
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () {
                errorMessage.Foreground = System.Windows.Media.Brushes.Green;
            });

            if (!Directory.Exists("ChampImages"))
                Directory.CreateDirectory("ChampImages");

            bool updated = false;

            foreach (ChampionDTO champ in champsList)
            {
                string directory = "ChampImages\\";
                string fileName = directory + champ.id + ".jpg";
                string fileNameAvaliable = directory + champ.id + "_aval.jpg";
                string fileNameObtained = directory + champ.id + "_obt.jpg";

                if (!File.Exists(fileName))
                {
                    updated = true;

                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () {
                        errorMessage.Text = "Downloading " + champ.name + " image...";
                    });

                    string url = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-tiles/" + champ.id + "/" + champ.id + "000.jpg";
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(new Uri(url), fileName);
                    }
                }

                if (!File.Exists(fileNameAvaliable))
                {
                    updated = true;

                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () {
                        errorMessage.Text = "Generating " + champ.name + " image...";
                    });

                    Bitmap img = new Bitmap(fileName);

                    for (int i = 0; i < img.Width; i++)
                    {
                        for (int j = 0; j < img.Height; j++)
                        {
                            System.Drawing.Color c = img.GetPixel(i, j);
                            int avg = (c.R + c.G + c.B) / 3;
                            System.Drawing.Color gray = System.Drawing.Color.FromArgb((int)(avg * 0.7), (int)(avg * 1.0), (int)(avg * 0.7));
                            img.SetPixel(i, j, gray);
                        }
                    }

                    using (Graphics g = Graphics.FromImage(img))
                        g.DrawRectangle(new System.Drawing.Pen(System.Drawing.Brushes.Green, 40), new Rectangle(0, 0, img.Width, img.Height));

                    img.Save(fileNameAvaliable);
                }

                if (!File.Exists(fileNameObtained))
                {
                    updated = true;

                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () {
                        errorMessage.Text = "Generating " + champ.name + " image...";
                    });

                    Bitmap img = new Bitmap(fileName);
                    for (int i = 0; i < img.Width; i++)
                    {
                        for (int j = 0; j < img.Height; j++)
                        {
                            System.Drawing.Color c = img.GetPixel(i, j);
                            int avg = (c.R + c.G + c.B) / 3;
                            System.Drawing.Color gray = System.Drawing.Color.FromArgb((int)(avg * 1.0), (int)(avg * 0.5), (int)(avg * 0.5));
                            img.SetPixel(i, j, gray);
                        }
                    }

                    using (Graphics g = Graphics.FromImage(img))
                        g.DrawRectangle(new System.Drawing.Pen(System.Drawing.Brushes.Red, 40), new Rectangle(0, 0, img.Width, img.Height));

                    img.Save(fileNameObtained);
                }
            }

            if (updated)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate () {
                    errorMessage.Text = "Succesfully updated database.";
                });
            }
        }
        void GenerateInfographic(object sender, RoutedEventArgs e)
        {
            if (errorOccurred)
                return;

            if (string.IsNullOrEmpty(playerNameInput.Text))
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = "Player name not given.";
                return;
            }

            if (string.IsNullOrEmpty(apiKeyInput.Password))
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = "Api key not given. You can get one on \nhttps://developer.riotgames.com";
                return;
            }

            string playerName = playerNameInput.Text;
            string summonerDataPath = "/lol/summoner/v4/summoners/by-name/" + playerName;

            SummonerDTO summonerInfo = null;
            List<ChampionMasteryDTO> championMasteryDTOs = null;

            try
            {
                summonerInfo = GetDTOFromApi<SummonerDTO>(summonerDataPath);
                string champDetailsPath = "/lol/champion-mastery/v4/champion-masteries/by-summoner/" + summonerInfo.id;
                championMasteryDTOs = GetDTOFromApi<List<ChampionMasteryDTO>>(champDetailsPath);
            }
            catch (Exception)
            {
                errorMessage.Foreground = System.Windows.Media.Brushes.Red;
                errorMessage.Text = "Unknown error occured while fetching chest data.";
            }

            errorMessage.Foreground = System.Windows.Media.Brushes.Green;
            errorMessage.Text = "Generating...";

            int cols = 15;
            int rows = (int)Math.Ceiling((float)champsList.Count / cols);
            int initGap = 150;
            int gap = 15;
            int imgWidth = 100;
            int imgHeight = 100;

            int width = gap + cols * (imgWidth + gap);
            int height = initGap + gap + rows * (imgHeight + gap);
            Bitmap result = new Bitmap(width, height);
            Graphics gfx = Graphics.FromImage(result);

            gfx.DrawImage(new Bitmap("ChampImages\\backgroundBig.png"), new System.Drawing.Point[] {
                new System.Drawing.Point(0, 0),
                new System.Drawing.Point(width, 0),
                new System.Drawing.Point(0, height)
            });

            RectangleF rect = new RectangleF(0, 0, width, (int)(initGap));
            StringFormat format = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            gfx.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            gfx.DrawString("LoL Avaliable Chests", new Font("Friz Quadrata", 40), System.Drawing.Brushes.DarkGoldenrod, rect, format);

            rect = new RectangleF(gap, (int)(initGap * 0.6), width, (int)(initGap * 0.4));
            format.Alignment = StringAlignment.Near;
            gfx.DrawString("Generated for player: " + playerName + "\nGeneration date: " + DateTime.Now.ToString(), new Font("Friz Quadrata", 20), System.Drawing.Brushes.DarkGoldenrod, rect, format);

            int currCol = 0;
            int currRow = 0;
            foreach (var champ in champsList)
            {
                bool chestGranted = false;

                foreach (var champData in from champData in championMasteryDTOs
                                          where champData.championId == champ.id
                                          select champData)
                {
                    chestGranted = champData.chestGranted;
                    break;
                }

                string filename = "ChampImages\\" + champ.id + "_";
                Bitmap champTile = chestGranted ? new Bitmap(filename + "obt.jpg") : new Bitmap(filename + "aval.jpg");
                gfx.DrawImage(champTile, new System.Drawing.Point[] {
                    new System.Drawing.Point(gap + currCol * (imgWidth + gap), initGap + gap + currRow * (imgHeight + gap)),
                    new System.Drawing.Point((currCol + 1) * (imgWidth + gap), initGap + gap + currRow * (imgHeight + gap)),
                    new System.Drawing.Point(gap + currCol * (imgWidth + gap), initGap + (currRow + 1) * (imgHeight + gap))
                    }); ;

                currCol++;
                if (currCol >= cols)
                {
                    currCol = 0;
                    currRow++;
                }
            }

            errorMessage.Foreground = System.Windows.Media.Brushes.Green;
            errorMessage.Text = "Done.";

            result.Save("chests.png");
            System.Diagnostics.Process.Start("chests.png");
        }
    }
}
