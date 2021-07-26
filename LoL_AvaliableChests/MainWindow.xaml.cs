using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        public MainWindow()
        {
            InitializeComponent();
            GenerateComboBoxContent();
            try
            {
                DownloadChampionData();
            }
            catch (Exception)
            {
                return;
            }

            InitInputs();

            errorMessage.Foreground = Brushes.White;
            errorMessage.Text = string.Empty;
        }
        void InitInputs()
        {
            bool initiated = false;

            if (File.Exists(pathToConfigFile))
            {
                string[] content = File.ReadAllLines(pathToConfigFile);

                if (content.Length == 3)
                {
                    playerNameInput.Text = content[0];
                    regionInput.SelectedIndex = Array.IndexOf(regions, content[1]);
                    apiKeyInput.Password = content[2];

                    initiated = true;
                }
            }

            if (!initiated)
            {
                regionInput.SelectedIndex = 0;
            }
        }
        public void GenerateComboBoxContent()
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
        public void DownloadChampionData()
        {
            try
            {
                string champsJsonPath = "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-summary.json";
                champsList = GetDTOFromJson<List<ChampionDTO>>(champsJsonPath);
            }
            catch (Exception)
            {
                errorMessage.Foreground = Brushes.Red;
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
        public void GetApiKey(object sender, RoutedEventArgs e)
        {
            if (errorOccurred)
                return;

            System.Diagnostics.Process.Start("https://developer.riotgames.com");
        }
        public void GenerateList(object sender, RoutedEventArgs e)
        {
            if (errorOccurred)
                return;

            if (string.IsNullOrEmpty(playerNameInput.Text))
            {
                errorMessage.Foreground = Brushes.Red;
                errorMessage.Text = "Player name not given.";
                return;
            }

            if (string.IsNullOrEmpty(apiKeyInput.Password))
            {
                errorMessage.Foreground = Brushes.Red;
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

            errorMessage.Foreground = Brushes.DarkGreen;
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
        public HttpResponseMessage GET(string URL)
        {
            using (HttpClient client = new HttpClient())
            {
                var result = client.GetAsync(URL);
                result.Wait();
                return result.Result;
            }
        }
        public string GetURI(string path)
        {
            string apiKey = apiKeyInput.Password;
            string selectedRegion = ((ComboBoxItem)regionInput.SelectedItem).Content.ToString();
            string regionCode;
            switch (selectedRegion)
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
                    throw new NotImplementedException("Region " + selectedRegion + " not recognised");
            }
            return "https://" + regionCode + ".api.riotgames.com/" + path + "?api_key=" + apiKey;
        }
        public T GetDTOFromApi<T>(string path)
        {
            string uri;
            try
            {
                uri = GetURI(path);
            }
            catch (NotImplementedException e)
            {
                errorMessage.Foreground = Brushes.Red;
                errorMessage.Text = e.Message;
                throw;
            }

            return GetDTOFromJson<T>(uri);
        }
        public T GetDTOFromJson<T>(string path)
        {
            string content = GetJson(path);
            return JsonConvert.DeserializeObject<T>(content);
        }
        public string GetJson(string uri)
        {
            var response = GET(uri);
            string content = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                errorMessage.Foreground = Brushes.Red;
                errorMessage.Text = "Error occured while connecting to Riot API.";
                throw new HttpRequestException();
            }

            return content;
        }
        private void OnRegionChange(object sender, SelectionChangedEventArgs e)
        {
            for (int i = 0; i < regionInput.Items.Count; i++)
            {
                var item = ((ComboBoxItem)regionInput.Items.GetItemAt(i));
                item.Template = item == regionInput.SelectedItem ? selectedRegionTemplate : regionTemplate;
                item.ApplyTemplate();
                ((TextBlock)item.Template.FindName("textField", item)).Text = item.Content.ToString();
            }

            dropdownLabel.Text = ((ComboBoxItem)regionInput.SelectedItem).Content.ToString();
        }
    }
}
