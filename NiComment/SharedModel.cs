using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Keycloak.Net;
using Keycloak.Net.Models.Users;
using NiComment.Properties;

namespace NiComment
{
    internal class SharedModel
    {
        private readonly ClientWebSocket ws = new ClientWebSocket();
        public SharedModel()
        {

        }
        public KeycloakClient ConnectKeyCloak(string userName, string password) {
            Settings settings = Settings.Default;
            return new KeycloakClient($"http://{settings.KeycloakHost}:{settings.KeycloakPort}", userName, password);
        }
        public async Task<bool> BANUser(KeycloakClient keycloakClient, string userID) {
            Settings settings = Settings.Default;
            User user = await keycloakClient.GetUserAsync(settings.NiCommentRealm, userID);
            user.Enabled = false;
            return await keycloakClient.UpdateUserAsync(settings.NiCommentRealm, userID, user);
        }
        public async Task<bool> LiftUserBan(KeycloakClient keycloakClient, string userID) {
            Settings settings = Settings.Default;
            User user = await keycloakClient.GetUserAsync(settings.NiCommentRealm, userID);
            user.Enabled = true;
            return await keycloakClient.UpdateUserAsync(settings.NiCommentRealm, userID, user);
        }
        public async void ConnectWebSocket(IProgress<Comment> commentProgress)
        {
            //ClientWebSocket ws = new ClientWebSocket();

            Settings settings = Settings.Default;
            //接続先エンドポイントを指定
            var uri = new Uri($"ws://{settings.WebSocketHost}:{settings.WebSocketPort}{settings.WebSocketPath}?ID=master");

            //サーバに対し、接続を開始
            await ws.ConnectAsync(uri, CancellationToken.None);
            var buffer = new byte[1024];

            //情報取得待ちループ
            while (true)
            {
                //所得情報確保用の配列を準備
                var segment = new ArraySegment<byte>(buffer);

                //サーバからのレスポンス情報を取得
                var result = await ws.ReceiveAsync(segment, CancellationToken.None);

                //エンドポイントCloseの場合、処理を中断
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK",
                      CancellationToken.None);
                    return;
                }

                //バイナリの場合は、当処理では扱えないため、処理を中断
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                      "I don't do binary", CancellationToken.None);
                    return;
                }

                //メッセージの最後まで取得
                int count = result.Count;
                while (!result.EndOfMessage)
                {
                    if (count >= buffer.Length)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.InvalidPayloadData,
                          "That's too long", CancellationToken.None);
                        return;
                    }
                    segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                    result = await ws.ReceiveAsync(segment, CancellationToken.None);

                    count += result.Count;
                }

                //メッセージを取得
                Comment record = JsonSerializer.Deserialize<Comment>(Encoding.UTF8.GetString(buffer, 0, count));
                commentProgress.Report(record);
                //Console.WriteLine("> " + message);
            }
        }
        public async void SendMessage(Comment comment) {
            var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(comment));
            var segment = new ArraySegment<byte>(buffer);

            //クライアント側に文字列を送信
            ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}   
