using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Net.Mime;
using NiComment.Properties;
using System.Windows;

namespace NiComment.Keycloak {
    internal class KeycloakClient {
        //private string _AccessToken = "";
        private readonly string _ClientSecret;
        private readonly string _ClientID;
        private readonly string _UserName;

        public KeycloakClient(string clientSecret, string clientID, string userName) {
            _ClientSecret = clientSecret;
            _ClientID = clientID;
            _UserName = userName;
        }
        //ref:https://curl.olsh.me/
        public async Task<string> GetTokenAsync(string password) {
            NiComment.Properties.Settings settings = NiComment.Properties.Settings.Default;
            try {
                using (HttpClient httpClient = new HttpClient()) {
                    using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("POST"), $"http://{settings.KeycloakHost}:{settings.KeycloakPort}/auth/realms/{settings.MasterRealm}/protocol/openid-connect/token")) {
                        List<string> contentList = new List<string>();
                        contentList.Add($"client_secret={_ClientSecret}");
                        contentList.Add($"client_id={_ClientID}");
                        contentList.Add($"username={_UserName}");
                        contentList.Add($"password={password}");
                        contentList.Add("grant_type=password");
                        request.Content = new StringContent(string.Join("&", contentList));
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

                        HttpResponseMessage response = await httpClient.SendAsync(request);
                        AccessTokenInformation accessTokenInformation = JsonSerializer.Deserialize<AccessTokenInformation>(await response.Content.ReadAsStringAsync());
                        return accessTokenInformation.access_token;
                    }
                }
            } catch (Exception exception) {
                MessageBox.Show("トークンの取得に失敗しました");
                MessageBox.Show(exception.StackTrace, exception.Message);
                return string.Empty;
            }
            
        }

        public async Task<bool> BanUser(string password, string userID) {
            string accessToken = await GetTokenAsync(password);
            if (string.IsNullOrWhiteSpace(accessToken)) {
                return false;
            }
            try {
                NiComment.Properties.Settings settings = NiComment.Properties.Settings.Default;
                using (HttpClient httpClient = new HttpClient()) {
                    string uri = $"http://{settings.KeycloakHost}:{settings.KeycloakPort}/auth/admin/realms/{settings.NiCommentRealm}/users/{userID}";
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri);
                    request.Content = new StringContent(JsonSerializer.Serialize(new User() { enabled = false }), Encoding.UTF8, "application/json");
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    request.Headers.Add("Authorization", $"bearer {accessToken}");
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    if (response.StatusCode != HttpStatusCode.NoContent) {
                        MessageBox.Show("ユーザーのブロックに失敗しました");
                        return false;
                    }
                }
                using (HttpClient httpClient = new HttpClient()) {
                    using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("POST"), $"http://{settings.WebSocketHost}:{settings.WebSocketPort}/api/close?ID={userID}")) {

                        HttpResponseMessage response = await httpClient.SendAsync(request);
                        if (response.StatusCode != HttpStatusCode.OK) {
                            MessageBox.Show("Websocketのクローズに失敗しました"); ;
                            return false;
                        }
                        return true;
                    }
                }
            } catch (Exception exception){
                MessageBox.Show(exception.StackTrace, exception.Message);
                return false;
            }
            
        }

    }

    public class User {
        public bool enabled { get; set; }
    }

    public class AccessTokenInformation {
    public string access_token { get; set; }
    public int expires_in { get; set; }
    public int refresh_expires_in { get; set; }
    public string refresh_token { get; set; }
    public string token_type { get; set; }
    public int notbeforepolicy { get; set; }
    public string session_state { get; set; }
    public string scope { get; set; }
    }

}
